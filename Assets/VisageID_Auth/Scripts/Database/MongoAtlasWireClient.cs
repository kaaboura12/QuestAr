using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using VisageIDAuth.Models;

namespace VisageIDAuth.Database
{
    /// <summary>
    /// Client MongoDB Atlas 100% C# natif (sans aucune DLL externe conflictuelle) compatible PC, Android (IL2CPP) et iOS.
    /// Implémente le protocole binaire MongoDB Wire Protocol (OP_MSG 2013), BSON, TLS et l'authentification SCRAM-SHA-1.
    /// </summary>
    public class MongoAtlasWireClient
    {
        private readonly string[] _shardHosts;
        private readonly int _port;
        private readonly string _username;
        private readonly string _password;
        private readonly string _databaseName;
        private string _cachedPrimaryHost;
        private int _reqId = 100;

        public string PrimaryHost => _cachedPrimaryHost;

        public MongoAtlasWireClient(string[] shardHosts, int port, string username, string password, string databaseName)
        {
            _shardHosts = shardHosts;
            _port = port;
            _username = username;
            _password = password;
            _databaseName = databaseName;
        }

        private static bool ValidateCert(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        private static void AuthenticateTls(SslStream ssl, string targetHost)
        {
            System.Net.ServicePointManager.ServerCertificateValidationCallback = ValidateCert;
            try
            {
                ssl.AuthenticateAsClient(targetHost, new X509CertificateCollection(), SslProtocols.Tls12, false);
            }
            catch
            {
                ssl.AuthenticateAsClient(targetHost);
            }
        }

        /// <summary>
        /// Se connecte au noeud PRIMARY du cluster MongoDB Atlas, s'authentifie en SCRAM-SHA-1,
        /// crée les collections "users" et "login_logs" dans la base "visage_auth_db", et récupère les utilisateurs existants.
        /// </summary>
        public Task<(bool success, string message, List<MongoUserDocument> users)> InitializeAndFetchUsersAsync(string usersCollection, string logsCollection)
        {
            return Task.Run(() =>
            {
                try
                {
                    string primary = DiscoverPrimaryHost();
                    if (string.IsNullOrEmpty(primary))
                        return (false, "Impossible de joindre les shards MongoDB Atlas.", null);

                    _cachedPrimaryHost = primary;

                    using (var tcp = new TcpClient())
                    {
                        var ar = tcp.BeginConnect(primary, _port, null, null);
                        if (!ar.AsyncWaitHandle.WaitOne(5000))
                            return (false, $"Timeout de connexion vers {primary}:{_port}", null);
                        tcp.EndConnect(ar);

                        using (var ssl = new SslStream(tcp.GetStream(), false, ValidateCert))
                        {
                            AuthenticateTls(ssl, primary);

                            if (!AuthenticateScramSha1(ssl, out string authErr))
                                return (false, $"Erreur d'authentification MongoDB Atlas : {authErr}", null);

                            // Créer explicitement les collections dans la nouvelle base sur MongoDB Atlas Online
                            CreateCollectionIfNotExists(ssl, usersCollection);
                            CreateCollectionIfNotExists(ssl, logsCollection);

                            // Lire tous les utilisateurs présents dans MongoDB Atlas Online
                            List<MongoUserDocument> remoteUsers = FindAllUsers(ssl, usersCollection);
                            return (true, $"Connecté en ligne à MongoDB Atlas ({primary} -> {_databaseName})", remoteUsers);
                        }
                    }
                }
                catch (Exception ex)
                {
                    return (false, $"Exception MongoDB Atlas : {ex.Message}", null);
                }
            });
        }

        /// <summary>
        /// Insère ou met à jour un document utilisateur directement sur MongoDB Atlas Online.
        /// </summary>
        public Task<(bool success, string error)> UpsertUserOnlineAsync(string usersCollection, MongoUserDocument user)
        {
            return Task.Run(() =>
            {
                try
                {
                    string primary = !string.IsNullOrEmpty(_cachedPrimaryHost) ? _cachedPrimaryHost : DiscoverPrimaryHost();
                    if (string.IsNullOrEmpty(primary))
                        return (false, "Aucun noeud primaire MongoDB Atlas disponible.");

                    using (var tcp = new TcpClient(primary, _port))
                    using (var ssl = new SslStream(tcp.GetStream(), false, ValidateCert))
                    {
                        AuthenticateTls(ssl, primary);
                        if (!AuthenticateScramSha1(ssl, out string authErr))
                            return (false, authErr);

                        byte[] userBson = BuildUserBsonDocument(user);
                        byte[] insertCmd = new BsonWriter()
                            .WriteString("insert", usersCollection)
                            .WriteDocumentArray("documents", new List<byte[]> { userBson })
                            .WriteBool("ordered", false)
                            .WriteString("$db", _databaseName)
                            .ToBytes();

                        byte[] resp = SendOpMsg(ssl, ++_reqId, insertCmd);
                        string err = ExtractBsonString(resp, "errmsg");
                        if (!string.IsNullOrEmpty(err) && !err.Contains("E11000"))
                        {
                            return (false, err);
                        }

                        return (true, null);
                    }
                }
                catch (Exception ex)
                {
                    return (false, ex.Message);
                }
            });
        }

        /// <summary>
        /// Insère un document d'historique de connexion dans la collection "login_logs" sur MongoDB Atlas Online.
        /// </summary>
        public Task<bool> InsertLoginLogOnlineAsync(string logsCollection, MongoLoginLogDocument log)
        {
            return Task.Run(() =>
            {
                try
                {
                    string primary = !string.IsNullOrEmpty(_cachedPrimaryHost) ? _cachedPrimaryHost : DiscoverPrimaryHost();
                    if (string.IsNullOrEmpty(primary))
                        return false;

                    using (var tcp = new TcpClient(primary, _port))
                    using (var ssl = new SslStream(tcp.GetStream(), false, ValidateCert))
                    {
                        AuthenticateTls(ssl, primary);
                        if (!AuthenticateScramSha1(ssl, out _))
                            return false;

                        byte[] logBson = BuildLoginLogBsonDocument(log);
                        byte[] insertCmd = new BsonWriter()
                            .WriteString("insert", logsCollection)
                            .WriteDocumentArray("documents", new List<byte[]> { logBson })
                            .WriteString("$db", _databaseName)
                            .ToBytes();

                        SendOpMsg(ssl, ++_reqId, insertCmd);
                        return true;
                    }
                }
                catch
                {
                    return false;
                }
            });
        }

        /// <summary>
        /// Récupère la liste à jour des utilisateurs depuis MongoDB Atlas Online.
        /// </summary>
        public Task<List<MongoUserDocument>> FetchUsersOnlineAsync(string usersCollection)
        {
            return Task.Run(() =>
            {
                try
                {
                    string primary = !string.IsNullOrEmpty(_cachedPrimaryHost) ? _cachedPrimaryHost : DiscoverPrimaryHost();
                    if (string.IsNullOrEmpty(primary))
                        return null;

                    using (var tcp = new TcpClient(primary, _port))
                    using (var ssl = new SslStream(tcp.GetStream(), false, ValidateCert))
                    {
                        AuthenticateTls(ssl, primary);
                        if (!AuthenticateScramSha1(ssl, out _))
                            return null;

                        return FindAllUsers(ssl, usersCollection);
                    }
                }
                catch
                {
                    return null;
                }
            });
        }

        /// <summary>
        /// Supprime un utilisateur sur MongoDB Atlas Online par son nom d'utilisateur.
        /// </summary>
        public Task<bool> DeleteUserOnlineAsync(string usersCollection, string username)
        {
            return Task.Run(() =>
            {
                try
                {
                    string primary = !string.IsNullOrEmpty(_cachedPrimaryHost) ? _cachedPrimaryHost : DiscoverPrimaryHost();
                    if (string.IsNullOrEmpty(primary))
                        return false;

                    using (var tcp = new TcpClient(primary, _port))
                    using (var ssl = new SslStream(tcp.GetStream(), false, ValidateCert))
                    {
                        AuthenticateTls(ssl, primary);
                        if (!AuthenticateScramSha1(ssl, out _))
                            return false;

                        byte[] qDoc = new BsonWriter().WriteString("username", username).ToBytes();
                        byte[] delSpec = new BsonWriter()
                            .WriteDocument("q", qDoc)
                            .WriteInt32("limit", 1)
                            .ToBytes();
                        byte[] delCmd = new BsonWriter()
                            .WriteString("delete", usersCollection)
                            .WriteDocumentArray("deletes", new List<byte[]> { delSpec })
                            .WriteString("$db", _databaseName)
                            .ToBytes();

                        SendOpMsg(ssl, ++_reqId, delCmd);
                        return true;
                    }
                }
                catch
                {
                    return false;
                }
            });
        }

        private string DiscoverPrimaryHost()
        {
            if (_shardHosts == null || _shardHosts.Length == 0)
                return null;

            foreach (string host in _shardHosts)
            {
                if (string.IsNullOrWhiteSpace(host)) continue;
                try
                {
                    using (var tcp = new TcpClient())
                    {
                        var ar = tcp.BeginConnect(host, _port, null, null);
                        if (!ar.AsyncWaitHandle.WaitOne(3500)) continue;
                        tcp.EndConnect(ar);

                        using (var ssl = new SslStream(tcp.GetStream(), false, ValidateCert))
                        {
                            AuthenticateTls(ssl, host);
                            byte[] helloDoc = new BsonWriter()
                                .WriteInt32("hello", 1)
                                .WriteString("$db", "admin")
                                .ToBytes();

                            byte[] resp = SendOpMsg(ssl, ++_reqId, helloDoc);
                            string primary = ExtractBsonString(resp, "primary");
                            if (!string.IsNullOrEmpty(primary))
                            {
                                int colon = primary.IndexOf(':');
                                return colon > 0 ? primary.Substring(0, colon) : primary;
                            }

                            if (ExtractBsonBool(resp, "isWritablePrimary"))
                            {
                                return host;
                            }
                        }
                    }
                }
                catch
                {
                    // Essayer le shard suivant
                }
            }

            return _shardHosts[0];
        }

        private bool AuthenticateScramSha1(Stream ssl, out string errorMessage)
        {
            errorMessage = null;
            byte[] nonceBytes = new byte[18];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(nonceBytes);
            }
            string clientNonce = Convert.ToBase64String(nonceBytes);

            string safeUser = _username.Replace("=", "=3D").Replace(",", "=2C");
            string clientFirstBare = "n=" + safeUser + ",r=" + clientNonce;
            string clientFirstFull = "n,," + clientFirstBare;

            byte[] saslStartDoc = new BsonWriter()
                .WriteInt32("saslStart", 1)
                .WriteString("mechanism", "SCRAM-SHA-1")
                .WriteBinary("payload", Encoding.UTF8.GetBytes(clientFirstFull))
                .WriteString("$db", "admin")
                .ToBytes();

            byte[] resp1 = SendOpMsg(ssl, ++_reqId, saslStartDoc);
            int convId = ExtractBsonInt32(resp1, "conversationId");
            byte[] serverFirstBytes = ExtractBsonBinary(resp1, "payload");
            if (serverFirstBytes == null)
            {
                errorMessage = ExtractBsonString(resp1, "errmsg") ?? "saslStart failed";
                return false;
            }

            string serverFirst = Encoding.UTF8.GetString(serverFirstBytes);
            string fullNonce = "";
            string saltB64 = "";
            int iterations = 10000;

            foreach (string part in serverFirst.Split(','))
            {
                if (part.StartsWith("r=")) fullNonce = part.Substring(2);
                else if (part.StartsWith("s=")) saltB64 = part.Substring(2);
                else if (part.StartsWith("i=")) int.TryParse(part.Substring(2), out iterations);
            }

            string md5Hex;
            using (var md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(_username + ":mongo:" + _password));
                var sb = new StringBuilder(32);
                for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("x2"));
                md5Hex = sb.ToString();
            }

            byte[] salt = Convert.FromBase64String(saltB64);
            byte[] saltedPassword;
            using (var pbkdf2 = new Rfc2898DeriveBytes(Encoding.UTF8.GetBytes(md5Hex), salt, iterations))
            {
                saltedPassword = pbkdf2.GetBytes(20);
            }

            byte[] clientKey;
            using (var hmac = new HMACSHA1(saltedPassword))
                clientKey = hmac.ComputeHash(Encoding.UTF8.GetBytes("Client Key"));

            byte[] storedKey;
            using (var sha1 = SHA1.Create())
                storedKey = sha1.ComputeHash(clientKey);

            string clientFinalNoProof = "c=biws,r=" + fullNonce;
            string authMessage = clientFirstBare + "," + serverFirst + "," + clientFinalNoProof;

            byte[] clientSig;
            using (var hmac = new HMACSHA1(storedKey))
                clientSig = hmac.ComputeHash(Encoding.UTF8.GetBytes(authMessage));

            byte[] clientProof = new byte[clientKey.Length];
            for (int i = 0; i < clientKey.Length; i++)
                clientProof[i] = (byte)(clientKey[i] ^ clientSig[i]);

            string clientFinal = clientFinalNoProof + ",p=" + Convert.ToBase64String(clientProof);

            byte[] saslContDoc = new BsonWriter()
                .WriteInt32("saslContinue", 1)
                .WriteInt32("conversationId", convId)
                .WriteBinary("payload", Encoding.UTF8.GetBytes(clientFinal))
                .WriteString("$db", "admin")
                .ToBytes();

            byte[] resp2 = SendOpMsg(ssl, ++_reqId, saslContDoc);
            byte[] serverFinalBytes = ExtractBsonBinary(resp2, "payload");
            if (serverFinalBytes == null)
            {
                errorMessage = ExtractBsonString(resp2, "errmsg") ?? "saslContinue failed";
                return false;
            }

            if (!ExtractBsonBool(resp2, "done"))
            {
                byte[] saslCont2Doc = new BsonWriter()
                    .WriteInt32("saslContinue", 1)
                    .WriteInt32("conversationId", convId)
                    .WriteBinary("payload", Array.Empty<byte>())
                    .WriteString("$db", "admin")
                    .ToBytes();
                SendOpMsg(ssl, ++_reqId, saslCont2Doc);
            }

            return true;
        }

        private void CreateCollectionIfNotExists(Stream ssl, string collectionName)
        {
            byte[] createCmd = new BsonWriter()
                .WriteString("create", collectionName)
                .WriteString("$db", _databaseName)
                .ToBytes();
            SendOpMsg(ssl, ++_reqId, createCmd);
        }

        private List<MongoUserDocument> FindAllUsers(Stream ssl, string usersCollection)
        {
            byte[] findCmd = new BsonWriter()
                .WriteString("find", usersCollection)
                .WriteInt32("batchSize", 500)
                .WriteString("$db", _databaseName)
                .ToBytes();

            byte[] resp = SendOpMsg(ssl, ++_reqId, findCmd);
            List<string> jsonPayloads = ExtractAllBsonStringsByKey(resp, "_unityJson");

            var list = new List<MongoUserDocument>();
            var obsoleteIdsToDelete = new List<string>();

            foreach (string json in jsonPayloads)
            {
                if (string.IsNullOrWhiteSpace(json)) continue;
                try
                {
                    var u = JsonUtility.FromJson<MongoUserDocument>(json);
                    if (u != null && !string.IsNullOrEmpty(u.email))
                    {
                        if (IsObsoleteV1Embedding(u.faceEmbedding))
                        {
                            if (!string.IsNullOrEmpty(u._id))
                                obsoleteIdsToDelete.Add(u._id);
                            continue;
                        }
                        list.Add(u);
                    }
                }
                catch { }
            }

            // Nettoyer automatiquement sur MongoDB Atlas les anciens comptes de test (v1 non-centrés)
            foreach (string oldId in obsoleteIdsToDelete)
            {
                try
                {
                    byte[] qDoc = new BsonWriter().WriteString("_id", oldId).ToBytes();
                    byte[] delSpec = new BsonWriter()
                        .WriteDocument("q", qDoc)
                        .WriteInt32("limit", 1)
                        .ToBytes();
                    byte[] delCmd = new BsonWriter()
                        .WriteString("delete", usersCollection)
                        .WriteDocumentArray("deletes", new List<byte[]> { delSpec })
                        .WriteString("$db", _databaseName)
                        .ToBytes();
                    SendOpMsg(ssl, ++_reqId, delCmd);
                }
                catch { }
            }

            return list;
        }

        private static bool IsObsoleteV1Embedding(float[] emb)
        {
            if (emb == null || emb.Length != 128) return true;
            double sum0_79 = 0.0;
            for (int i = 0; i < 80; i++)
            {
                sum0_79 += emb[i];
            }
            double mean0_79 = sum0_79 / 80.0;
            // Dans la v3 (Grille 10x8 T-Zone Z-Score), la moyenne du bloc [0..79] est exactement 0.0000.
            // Les anciens comptes v1 et v2 avaient une moyenne [0..79] différente de 0.
            return Math.Abs(mean0_79) > 0.003;
        }

        private static byte[] BuildUserBsonDocument(MongoUserDocument user)
        {
            string unityJson = JsonUtility.ToJson(user);
            return new BsonWriter()
                .WriteString("_id", user._id ?? MongoUserDocument.GenerateObjectId())
                .WriteString("username", user.username ?? "")
                .WriteString("email", user.email ?? "")
                .WriteString("passwordHash", user.passwordHash ?? "")
                .WriteBool("hasFaceId", user.hasFaceId)
                .WriteFloatArray("faceEmbedding", user.faceEmbedding ?? Array.Empty<float>())
                .WriteString("createdAt", user.createdAt ?? "")
                .WriteString("lastLoginAt", user.lastLoginAt ?? "")
                .WriteString("lastAuthMethod", user.lastAuthMethod ?? "")
                .WriteString("_unityJson", unityJson)
                .ToBytes();
        }

        private static byte[] BuildLoginLogBsonDocument(MongoLoginLogDocument log)
        {
            return new BsonWriter()
                .WriteString("_id", log._id ?? MongoUserDocument.GenerateObjectId())
                .WriteString("userId", log.userId ?? "")
                .WriteString("username", log.username ?? "")
                .WriteString("email", log.email ?? "")
                .WriteString("authMethod", log.authMethod ?? "")
                .WriteBool("success", log.success)
                .WriteDouble("similarityScore", Math.Round(log.similarityScore, 4))
                .WriteString("deviceModel", log.deviceModel ?? "")
                .WriteString("platform", log.platform ?? "")
                .WriteString("timestamp", log.timestamp ?? "")
                .ToBytes();
        }

        private static byte[] SendOpMsg(Stream stream, int reqId, byte[] bsonDoc)
        {
            int totalLen = 16 + 4 + 1 + bsonDoc.Length;
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(totalLen);
                bw.Write(reqId);
                bw.Write(0);
                bw.Write(2013); // OP_MSG
                bw.Write(0);
                bw.Write((byte)0);
                bw.Write(bsonDoc);
                byte[] packet = ms.ToArray();
                stream.Write(packet, 0, packet.Length);
                stream.Flush();
            }

            byte[] lenBuf = new byte[4];
            ReadExact(stream, lenBuf, 0, 4);
            int respLen = BitConverter.ToInt32(lenBuf, 0);
            byte[] fullResp = new byte[respLen];
            Array.Copy(lenBuf, 0, fullResp, 0, 4);
            ReadExact(stream, fullResp, 4, respLen - 4);

            int bsonLen = respLen - 21;
            byte[] bson = new byte[bsonLen];
            Array.Copy(fullResp, 21, bson, 0, bsonLen);
            return bson;
        }

        private static void ReadExact(Stream s, byte[] buf, int offset, int count)
        {
            int read = 0;
            while (read < count)
            {
                int r = s.Read(buf, offset + read, count - read);
                if (r <= 0) throw new EndOfStreamException("Connexion MongoDB fermée");
                read += r;
            }
        }

        public static string ExtractBsonString(byte[] bson, string key)
        {
            byte[] pattern = Encoding.UTF8.GetBytes(key + "\0");
            for (int i = 4; i < bson.Length - pattern.Length - 5; i++)
            {
                if (bson[i] == 0x02 && MatchAt(bson, i + 1, pattern))
                {
                    int valOffset = i + 1 + pattern.Length;
                    int strLen = BitConverter.ToInt32(bson, valOffset);
                    if (strLen > 0 && valOffset + 4 + strLen <= bson.Length)
                        return Encoding.UTF8.GetString(bson, valOffset + 4, strLen - 1);
                }
            }
            return null;
        }

        public static List<string> ExtractAllBsonStringsByKey(byte[] bson, string key)
        {
            var results = new List<string>();
            byte[] pattern = Encoding.UTF8.GetBytes(key + "\0");
            for (int i = 4; i < bson.Length - pattern.Length - 5; i++)
            {
                if (bson[i] == 0x02 && MatchAt(bson, i + 1, pattern))
                {
                    int valOffset = i + 1 + pattern.Length;
                    int strLen = BitConverter.ToInt32(bson, valOffset);
                    if (strLen > 0 && valOffset + 4 + strLen <= bson.Length)
                    {
                        results.Add(Encoding.UTF8.GetString(bson, valOffset + 4, strLen - 1));
                    }
                }
            }
            return results;
        }

        public static int ExtractBsonInt32(byte[] bson, string key)
        {
            byte[] pattern = Encoding.UTF8.GetBytes(key + "\0");
            for (int i = 4; i < bson.Length - pattern.Length - 4; i++)
            {
                if (bson[i] == 0x10 && MatchAt(bson, i + 1, pattern))
                {
                    int valOffset = i + 1 + pattern.Length;
                    return BitConverter.ToInt32(bson, valOffset);
                }
            }
            return 0;
        }

        public static bool ExtractBsonBool(byte[] bson, string key)
        {
            byte[] pattern = Encoding.UTF8.GetBytes(key + "\0");
            for (int i = 4; i < bson.Length - pattern.Length - 1; i++)
            {
                if (bson[i] == 0x08 && MatchAt(bson, i + 1, pattern))
                {
                    int valOffset = i + 1 + pattern.Length;
                    return bson[valOffset] != 0;
                }
            }
            return false;
        }

        public static byte[] ExtractBsonBinary(byte[] bson, string key)
        {
            byte[] pattern = Encoding.UTF8.GetBytes(key + "\0");
            for (int i = 4; i < bson.Length - pattern.Length - 5; i++)
            {
                if (bson[i] == 0x05 && MatchAt(bson, i + 1, pattern))
                {
                    int valOffset = i + 1 + pattern.Length;
                    int binLen = BitConverter.ToInt32(bson, valOffset);
                    byte[] data = new byte[binLen];
                    Array.Copy(bson, valOffset + 5, data, 0, binLen);
                    return data;
                }
            }
            return null;
        }

        private static bool MatchAt(byte[] src, int offset, byte[] pattern)
        {
            if (offset + pattern.Length > src.Length) return false;
            for (int i = 0; i < pattern.Length; i++)
                if (src[offset + i] != pattern[i]) return false;
            return true;
        }
    }

    internal class BsonWriter
    {
        private readonly MemoryStream _ms = new MemoryStream();
        private readonly BinaryWriter _bw;

        public BsonWriter()
        {
            _bw = new BinaryWriter(_ms);
            _bw.Write(0);
        }

        public BsonWriter WriteString(string key, string val)
        {
            _bw.Write((byte)0x02);
            WriteCString(key);
            byte[] bytes = Encoding.UTF8.GetBytes((val ?? "") + "\0");
            _bw.Write(bytes.Length);
            _bw.Write(bytes);
            return this;
        }

        public BsonWriter WriteInt32(string key, int val)
        {
            _bw.Write((byte)0x10);
            WriteCString(key);
            _bw.Write(val);
            return this;
        }

        public BsonWriter WriteDouble(string key, double val)
        {
            _bw.Write((byte)0x01);
            WriteCString(key);
            _bw.Write(val);
            return this;
        }

        public BsonWriter WriteBool(string key, bool val)
        {
            _bw.Write((byte)0x08);
            WriteCString(key);
            _bw.Write((byte)(val ? 1 : 0));
            return this;
        }

        public BsonWriter WriteBinary(string key, byte[] data)
        {
            _bw.Write((byte)0x05);
            WriteCString(key);
            _bw.Write(data.Length);
            _bw.Write((byte)0x00);
            _bw.Write(data);
            return this;
        }

        public BsonWriter WriteFloatArray(string key, float[] values)
        {
            _bw.Write((byte)0x04); // Array
            WriteCString(key);
            var arrWriter = new BsonWriter();
            if (values != null)
            {
                for (int i = 0; i < values.Length; i++)
                {
                    arrWriter.WriteDouble(i.ToString(), Math.Round(values[i], 5));
                }
            }
            byte[] arrBytes = arrWriter.ToBytes();
            _bw.Write(arrBytes);
            return this;
        }

        public BsonWriter WriteDocument(string key, byte[] docBytes)
        {
            _bw.Write((byte)0x03); // Embedded document
            WriteCString(key);
            _bw.Write(docBytes);
            return this;
        }

        public BsonWriter WriteDocumentArray(string key, List<byte[]> docs)
        {
            _bw.Write((byte)0x04); // Array
            WriteCString(key);
            using (var subMs = new MemoryStream())
            using (var subBw = new BinaryWriter(subMs))
            {
                subBw.Write(0);
                for (int i = 0; i < docs.Count; i++)
                {
                    subBw.Write((byte)0x03); // Embedded document
                    byte[] idxBytes = Encoding.UTF8.GetBytes(i.ToString() + "\0");
                    subBw.Write(idxBytes);
                    subBw.Write(docs[i]);
                }
                subBw.Write((byte)0x00);
                byte[] arrBytes = subMs.ToArray();
                BitConverter.GetBytes(arrBytes.Length).CopyTo(arrBytes, 0);
                _bw.Write(arrBytes);
            }
            return this;
        }

        private void WriteCString(string s)
        {
            _bw.Write(Encoding.UTF8.GetBytes(s + "\0"));
        }

        public byte[] ToBytes()
        {
            _bw.Write((byte)0x00);
            byte[] bytes = _ms.ToArray();
            BitConverter.GetBytes(bytes.Length).CopyTo(bytes, 0);
            return bytes;
        }
    }
}
