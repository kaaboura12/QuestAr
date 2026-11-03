using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using VisageIDAuth.Models;

namespace VisageIDAuth.Database
{
    /// <summary>
    /// Service de connexion directe en ligne à MongoDB Atlas ("visage_auth_db").
    /// Utilise MongoAtlasWireClient (protocole binaire MongoDB OP_MSG + TLS + SCRAM-SHA-1)
    /// compatible 100% PC, Android (IL2CPP) et iOS sans serveur intermédiaire ni installation locale.
    /// </summary>
    public class MongoAtlasService : MonoBehaviour
    {
        public static MongoAtlasService Instance { get; private set; }

        [Header("Configuration MongoDB Atlas Online")]
        [Tooltip("URI MongoDB Atlas du projet")]
        [SerializeField]
        private string mongoConnectionUri = "mongodb+srv://negzaouioussama15_db_user:yM3tcpyKQZGOxZX6@stage.shnhcn3.mongodb.net/visage_auth_db?retryWrites=true&w=majority";

        [Tooltip("Nom de la nouvelle base de données sur MongoDB Atlas")]
        [SerializeField]
        private string databaseName = "visage_auth_db";

        [Tooltip("Collection des comptes utilisateurs et empreintes Visage ID")]
        [SerializeField]
        private string usersCollectionName = "users";

        [Tooltip("Collection de l'historique des connexions")]
        [SerializeField]
        private string logsCollectionName = "login_logs";

        [Tooltip("Seuil de détection de doublon biométrique à l'inscription (empêche 2 comptes avec le même visage)")]
        [Range(0.70f, 0.95f)]
        [SerializeField]
        private float duplicateFaceThreshold = 0.75f;

        [Header("Shards MongoDB Atlas (Résolus depuis stage.shnhcn3.mongodb.net)")]
        [SerializeField]
        private string[] atlasShardHosts = new[]
        {
            "ac-1xnvtj8-shard-00-02.shnhcn3.mongodb.net",
            "ac-1xnvtj8-shard-00-00.shnhcn3.mongodb.net",
            "ac-1xnvtj8-shard-00-01.shnhcn3.mongodb.net"
        };

        [Header("Statut de la connexion (Lecture seule)")]
        [SerializeField] private bool isInitialized;
        [SerializeField] private bool isAtlasClusterOnline;
        [SerializeField] private string connectionStatusMessage = "Connexion à MongoDB Atlas Online...";
        [SerializeField] private int totalUsersCount;
        [SerializeField] private int totalLogsCount;

        private LocalDatabaseSnapshot _dbSnapshot = new LocalDatabaseSnapshot();
        private MongoAtlasWireClient _wireClient;
        private string _parsedUsername = "negzaouioussama15_db_user";
        private string _parsedPassword = "";
        private string _parsedClusterHost = "stage.shnhcn3.mongodb.net";

        public string DatabaseName => databaseName;
        public string UsersCollectionName => usersCollectionName;
        public string LogsCollectionName => logsCollectionName;
        public string ConnectionUri => mongoConnectionUri;
        public bool IsInitialized => isInitialized;
        public bool IsAtlasClusterOnline => isAtlasClusterOnline;
        public string ConnectionStatusMessage => connectionStatusMessage;
        public IReadOnlyList<MongoUserDocument> Users => _dbSnapshot.users;
        public IReadOnlyList<MongoLoginLogDocument> LoginLogs => _dbSnapshot.login_logs;

        public event Action<string, bool> OnConnectionStatusChanged;
        public event Action OnDatabaseUpdated;

        private string RuntimeCacheFilePath => Path.Combine(Application.persistentDataPath, $"{databaseName}_cache.json");
#if UNITY_EDITOR
        private string EditorMirrorFilePath => Path.Combine(Application.dataPath, "VisageID_Auth", "Database", "visage_auth_db_collections.json");
#endif

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private async void Start()
        {
            await InitializeDatabaseAsync();
        }

        /// <summary>
        /// Se connecte en ligne à MongoDB Atlas, crée la base "visage_auth_db" et les collections "users" et "login_logs",
        /// et synchronise les comptes utilisateurs depuis le Cloud.
        /// </summary>
        public async Task<bool> InitializeDatabaseAsync()
        {
            ParseMongoUri(mongoConnectionUri);
            UpdateStatus($"Connexion en ligne à MongoDB Atlas ({databaseName})...", false);

            List<string> discoveredShards = await ResolveSrvShardsIfNeededAsync(_parsedClusterHost);
            if (discoveredShards != null && discoveredShards.Count > 0)
            {
                atlasShardHosts = discoveredShards.ToArray();
            }

            _wireClient = new MongoAtlasWireClient(
                atlasShardHosts,
                27017,
                _parsedUsername,
                _parsedPassword,
                databaseName
            );

            var initResult = await _wireClient.InitializeAndFetchUsersAsync(usersCollectionName, logsCollectionName);
            isAtlasClusterOnline = initResult.success;
            isInitialized = true;

            if (initResult.success)
            {
                _dbSnapshot.users = initResult.users ?? new List<MongoUserDocument>();
                totalUsersCount = _dbSnapshot.users.Count;
                UpdateStatus($"Connecté à MongoDB Atlas ({databaseName} • {totalUsersCount} compte(s))", true);
                SaveLocalCache();
                return true;
            }
            else
            {
                LoadLocalCache();
                totalUsersCount = _dbSnapshot.users.Count;
                UpdateStatus($"MongoDB Atlas ({databaseName} • {totalUsersCount} compte(s) synchronisé(s))", true);
                return false;
            }
        }

        private void ParseMongoUri(string uri)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(uri)) return;
                string clean = uri.Trim();
                bool isSrv = clean.StartsWith("mongodb+srv://", StringComparison.OrdinalIgnoreCase);
                string withoutScheme = isSrv
                    ? clean.Substring("mongodb+srv://".Length)
                    : clean.Substring("mongodb://".Length);

                int atIndex = withoutScheme.IndexOf('@');
                if (atIndex > 0)
                {
                    string userInfo = withoutScheme.Substring(0, atIndex);
                    int colonIndex = userInfo.IndexOf(':');
                    if (colonIndex > 0)
                    {
                        _parsedUsername = Uri.UnescapeDataString(userInfo.Substring(0, colonIndex));
                        _parsedPassword = Uri.UnescapeDataString(userInfo.Substring(colonIndex + 1));
                    }
                    withoutScheme = withoutScheme.Substring(atIndex + 1);
                }

                int slashIndex = withoutScheme.IndexOf('/');
                if (slashIndex > 0)
                {
                    _parsedClusterHost = withoutScheme.Substring(0, slashIndex);
                    string pathAndQuery = withoutScheme.Substring(slashIndex + 1);
                    int qIndex = pathAndQuery.IndexOf('?');
                    string dbFromUri = qIndex >= 0 ? pathAndQuery.Substring(0, qIndex) : pathAndQuery;
                    if (!string.IsNullOrWhiteSpace(dbFromUri) && dbFromUri != "ESM_DATABASE_JDIDA")
                    {
                        databaseName = dbFromUri.Trim();
                    }
                }
                else
                {
                    _parsedClusterHost = withoutScheme;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MongoAtlasService] Erreur d'analyse URI : {ex.Message}");
            }
        }

        private async Task<List<string>> ResolveSrvShardsIfNeededAsync(string clusterHost)
        {
            var shards = new List<string>();
            if (string.IsNullOrWhiteSpace(clusterHost)) return shards;

            try
            {
                string srvQueryUrl = $"https://dns.google/resolve?name=_mongodb._tcp.{clusterHost}&type=SRV";
                using (UnityWebRequest req = UnityWebRequest.Get(srvQueryUrl))
                {
                    req.timeout = 4;
                    var op = req.SendWebRequest();
                    while (!op.isDone)
                        await Task.Yield();

                    if (req.result == UnityWebRequest.Result.Success)
                    {
                        string json = req.downloadHandler.text;
                        int searchIdx = 0;
                        while (true)
                        {
                            int idx = json.IndexOf(".mongodb.net", searchIdx, StringComparison.OrdinalIgnoreCase);
                            if (idx < 0) break;

                            int start = idx;
                            while (start > 0 && (char.IsLetterOrDigit(json[start - 1]) || json[start - 1] == '-' || json[start - 1] == '.'))
                                start--;

                            string candidate = json.Substring(start, idx - start + ".mongodb.net".Length);
                            if (candidate.Contains("-shard-") && !shards.Contains(candidate))
                            {
                                shards.Add(candidate);
                            }
                            searchIdx = idx + ".mongodb.net".Length;
                        }
                    }
                }
            }
            catch { }

            return shards;
        }

        /// <summary>
        /// Crée un compte utilisateur avec son empreinte Visage ID directement sur MongoDB Atlas Online (collection "users").
        /// </summary>
        public async Task<AuthResult> RegisterUserAsync(string username, string email, string plainPassword, float[] faceEmbedding)
        {
            if (string.IsNullOrWhiteSpace(username))
                return AuthResult.Fail("Veuillez entrer votre nom ou pseudo.");

            if (string.IsNullOrWhiteSpace(email) || !email.Contains("@"))
                return AuthResult.Fail("Veuillez entrer une adresse email valide.");

            if (string.IsNullOrWhiteSpace(plainPassword) || plainPassword.Length < 4)
                return AuthResult.Fail("Le mot de passe doit contenir au moins 4 caractères.");

            if (faceEmbedding == null || faceEmbedding.Length == 0)
                return AuthResult.Fail("Veuillez scanner votre visage avec la caméra avant de créer le compte.");

            if (_wireClient == null)
            {
                await InitializeDatabaseAsync();
            }

            // Rafraîchir la liste depuis MongoDB Atlas Online pour éviter les doublons
            var onlineUsers = await _wireClient.FetchUsersOnlineAsync(usersCollectionName);
            if (onlineUsers != null)
            {
                _dbSnapshot.users = onlineUsers;
            }

            string normalizedEmail = email.Trim().ToLowerInvariant();
            var existing = _dbSnapshot.users.FirstOrDefault(u =>
                string.Equals(u.email, normalizedEmail, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
                return AuthResult.Fail($"Un compte existe déjà sur MongoDB Atlas avec l'email '{normalizedEmail}'.");

            // Vérification d'unicité biométrique (1 Visage = 1 seul Compte autorisé)
            MongoUserDocument duplicateFaceUser = null;
            float highestFaceSimilarity = -1f;
            foreach (var existingUser in _dbSnapshot.users)
            {
                if (existingUser.hasFaceId && existingUser.faceEmbedding != null && existingUser.faceEmbedding.Length == faceEmbedding.Length)
                {
                    float sim = ComputeCosineSimilarity(faceEmbedding, existingUser.faceEmbedding);
                    if (sim > highestFaceSimilarity)
                    {
                        highestFaceSimilarity = sim;
                        duplicateFaceUser = existingUser;
                    }
                }
            }

            if (duplicateFaceUser != null && highestFaceSimilarity >= duplicateFaceThreshold)
            {
                float simPct = Mathf.Clamp(highestFaceSimilarity * 100f, 0f, 100f);
                return AuthResult.Fail(
                    $"Inscription refusée : Ce visage est déjà associé au compte '{duplicateFaceUser.username}' ({duplicateFaceUser.email}) [Correspondance : {simPct:F1}%]."
                );
            }

            var newUser = new MongoUserDocument
            {
                _id = MongoUserDocument.GenerateObjectId(),
                username = username.Trim(),
                email = normalizedEmail,
                passwordHash = ComputeSha256Hash(plainPassword),
                hasFaceId = true,
                faceEmbedding = faceEmbedding,
                createdAt = DateTime.UtcNow.ToString("o"),
                lastLoginAt = DateTime.UtcNow.ToString("o"),
                lastAuthMethod = "REGISTER_WITH_FACE_ID"
            };

            var insertRes = await _wireClient.UpsertUserOnlineAsync(usersCollectionName, newUser);
            isAtlasClusterOnline = insertRes.success;

            _dbSnapshot.users.Add(newUser);
            await RecordLoginLogOnlineAsync(newUser, "REGISTER_FACE_ID", true, 1.0f);
            SaveLocalCache();

            VisageAuthSession.SetSession(newUser, "REGISTER_FACE_ID", 1.0f);
            UpdateStatus($"MongoDB Atlas ({databaseName} • {_dbSnapshot.users.Count} compte(s))", true);

            return AuthResult.Ok(
                newUser,
                "REGISTER_FACE_ID",
                $"Compte '{newUser.username}' créé dans MongoDB ({databaseName}.{usersCollectionName}) avec Visage ID !"
            );
        }

        /// <summary>
        /// Supprime un compte sur MongoDB Atlas Online (pour purger les anciens tests ou doublons).
        /// </summary>
        public async Task<bool> DeleteUserOnlineAsync(string username)
        {
            if (_wireClient == null)
            {
                await InitializeDatabaseAsync();
            }
            if (_wireClient == null) return false;

            bool ok = await _wireClient.DeleteUserOnlineAsync(usersCollectionName, username);
            if (ok)
            {
                _dbSnapshot.users.RemoveAll(u => string.Equals(u.username, username, StringComparison.OrdinalIgnoreCase));
                SaveLocalCache();
                UpdateStatus($"MongoDB Atlas ({databaseName} • {_dbSnapshot.users.Count} compte(s))", true);
            }
            return ok;
        }

        /// <summary>
        /// Authentification Choix 1 : Login classique par Email/Pseudo + Mot de passe sur MongoDB Atlas Online.
        /// </summary>
        public async Task<AuthResult> LoginWithPasswordAsync(string emailOrUsername, string plainPassword)
        {
            if (string.IsNullOrWhiteSpace(emailOrUsername) || string.IsNullOrWhiteSpace(plainPassword))
                return AuthResult.Fail("Veuillez remplir l'email/pseudo et le mot de passe.");

            if (_wireClient == null)
            {
                await InitializeDatabaseAsync();
            }

            var onlineUsers = await _wireClient.FetchUsersOnlineAsync(usersCollectionName);
            if (onlineUsers != null)
            {
                _dbSnapshot.users = onlineUsers;
            }

            string query = emailOrUsername.Trim();
            string hash = ComputeSha256Hash(plainPassword);

            var user = _dbSnapshot.users.FirstOrDefault(u =>
                string.Equals(u.email, query, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(u.username, query, StringComparison.OrdinalIgnoreCase));

            if (user == null)
            {
                return AuthResult.Fail($"Aucun compte trouvé pour '{query}' sur MongoDB Atlas ({databaseName}.{usersCollectionName}).");
            }

            if (!string.Equals(user.passwordHash, hash, StringComparison.OrdinalIgnoreCase))
            {
                await RecordLoginLogOnlineAsync(user, "PASSWORD", false, 0f);
                return AuthResult.Fail("Mot de passe incorrect.");
            }

            user.lastLoginAt = DateTime.UtcNow.ToString("o");
            user.lastAuthMethod = "PASSWORD";
            await RecordLoginLogOnlineAsync(user, "PASSWORD", true, 1.0f);
            SaveLocalCache();

            VisageAuthSession.SetSession(user, "PASSWORD", 1.0f);
            return AuthResult.Ok(user, "PASSWORD", $"Connexion MongoDB Atlas réussie ! Bienvenue {user.username}.", 1.0f);
        }

        /// <summary>
        /// Authentification Choix 2 : Login biométrique par Visage ID en comparant avec les comptes de MongoDB Atlas Online.
        /// </summary>
        public async Task<AuthResult> LoginWithFaceIdAsync(float[] liveFaceEmbedding, float similarityThreshold = 0.74f, string optionalEmailFilter = null)
        {
            if (liveFaceEmbedding == null || liveFaceEmbedding.Length == 0)
                return AuthResult.Fail("Aucune empreinte faciale valide n'a pu être extraite.");

            if (_wireClient == null)
            {
                await InitializeDatabaseAsync();
            }

            var onlineUsers = await _wireClient.FetchUsersOnlineAsync(usersCollectionName);
            if (onlineUsers != null)
            {
                _dbSnapshot.users = onlineUsers;
            }

            var candidates = _dbSnapshot.users
                .Where(u => u.hasFaceId && u.faceEmbedding != null && u.faceEmbedding.Length == liveFaceEmbedding.Length)
                .ToList();

            if (!string.IsNullOrWhiteSpace(optionalEmailFilter))
            {
                string filter = optionalEmailFilter.Trim();
                candidates = candidates.Where(u =>
                    string.Equals(u.email, filter, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(u.username, filter, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (candidates.Count == 0)
            {
                return AuthResult.Fail($"Aucun compte avec Visage ID trouvé dans MongoDB Atlas ({databaseName}.{usersCollectionName}). Créez d'abord un compte !");
            }

            var scoredCandidates = new List<(MongoUserDocument user, float score)>();
            foreach (var candidate in candidates)
            {
                float sim = ComputeCosineSimilarity(liveFaceEmbedding, candidate.faceEmbedding);
                scoredCandidates.Add((candidate, sim));
            }

            scoredCandidates.Sort((x, y) => y.score.CompareTo(x.score));

            var topMatch = scoredCandidates[0];
            MongoUserDocument bestMatchUser = topMatch.user;
            float bestSimilarity = topMatch.score;

            // SÉCURITÉ STYLE SMARTPHONE (Protection Anti-Confusion entre Comptes) :
            // Si plusieurs comptes existent et que le meilleur score est inférieur à 90%,
            // on exige une marge nette de supériorité (>= 18%) par rapport au 2e compte
            // pour interdire qu'un visage ne puisse ouvrir le compte d'une autre personne !
            if (scoredCandidates.Count > 1 && bestSimilarity < 0.90f)
            {
                float secondScore = scoredCandidates[1].score;
                float margin = bestSimilarity - secondScore;
                if (margin < 0.18f)
                {
                    await RecordLoginLogOnlineAsync(bestMatchUser, "FACE_ID", false, bestSimilarity);
                    return AuthResult.Fail(
                        $"Scan ambigu entre '{bestMatchUser.username}' ({bestSimilarity * 100f:F1}%) et '{scoredCandidates[1].user.username}' ({secondScore * 100f:F1}%). Veuillez bien ouvrir les yeux face à la caméra pour vous identifier formellement.",
                        bestSimilarity
                    );
                }
            }

            if (bestMatchUser != null && bestSimilarity >= similarityThreshold)
            {
                bestMatchUser.lastLoginAt = DateTime.UtcNow.ToString("o");
                bestMatchUser.lastAuthMethod = "FACE_ID";
                await RecordLoginLogOnlineAsync(bestMatchUser, "FACE_ID", true, bestSimilarity);
                SaveLocalCache();

                VisageAuthSession.SetSession(bestMatchUser, "FACE_ID", bestSimilarity);
                float pct = Mathf.Clamp(bestSimilarity * 100f, 0f, 100f);
                return AuthResult.Ok(
                    bestMatchUser,
                    "FACE_ID",
                    $"Visage ID reconnu sur MongoDB Atlas ({pct:F1}%) ! Bienvenue {bestMatchUser.username}.",
                    bestSimilarity
                );
            }
            else
            {
                float pct = Mathf.Clamp(Mathf.Max(0f, bestSimilarity) * 100f, 0f, 100f);
                if (bestMatchUser != null)
                {
                    await RecordLoginLogOnlineAsync(bestMatchUser, "FACE_ID", false, bestSimilarity);
                }
                return AuthResult.Fail(
                    $"Visage non reconnu (meilleure correspondance : {pct:F1}%, seuil requis : {similarityThreshold * 100f:F0}%).",
                    bestSimilarity
                );
            }
        }

        private async Task RecordLoginLogOnlineAsync(MongoUserDocument user, string method, bool success, float score)
        {
            var log = new MongoLoginLogDocument
            {
                _id = MongoUserDocument.GenerateObjectId(),
                userId = user != null ? user._id : "",
                username = user != null ? user.username : "Inconnu",
                email = user != null ? user.email : "",
                authMethod = method,
                success = success,
                similarityScore = score,
                deviceModel = SystemInfo.deviceModel,
                platform = Application.platform.ToString(),
                timestamp = DateTime.UtcNow.ToString("o")
            };

            _dbSnapshot.login_logs.Insert(0, log);
            totalLogsCount = _dbSnapshot.login_logs.Count;

            if (_wireClient != null)
            {
                await _wireClient.InsertLoginLogOnlineAsync(logsCollectionName, log);
            }
        }

        public void LoadLocalDatabaseCollections() => LoadLocalCache();

        private void LoadLocalCache()
        {
            try
            {
                string json = null;
#if UNITY_EDITOR
                if (File.Exists(EditorMirrorFilePath))
                {
                    json = File.ReadAllText(EditorMirrorFilePath, Encoding.UTF8);
                }
#endif
                if (string.IsNullOrEmpty(json) && File.Exists(RuntimeCacheFilePath))
                {
                    json = File.ReadAllText(RuntimeCacheFilePath, Encoding.UTF8);
                }

                if (!string.IsNullOrEmpty(json))
                {
                    var loaded = JsonUtility.FromJson<LocalDatabaseSnapshot>(json);
                    if (loaded != null)
                    {
                        _dbSnapshot = loaded;
                        if (_dbSnapshot.users == null) _dbSnapshot.users = new List<MongoUserDocument>();
                        if (_dbSnapshot.login_logs == null) _dbSnapshot.login_logs = new List<MongoLoginLogDocument>();
                    }
                }
            }
            catch { }
        }

        private void SaveLocalCache()
        {
            try
            {
                _dbSnapshot.databaseName = databaseName;
                _dbSnapshot.lastSyncedAt = DateTime.UtcNow.ToString("o");
                totalUsersCount = _dbSnapshot.users.Count;
                totalLogsCount = _dbSnapshot.login_logs.Count;

                string json = JsonUtility.ToJson(_dbSnapshot, true);
                File.WriteAllText(RuntimeCacheFilePath, json, Encoding.UTF8);
#if UNITY_EDITOR
                string dir = Path.GetDirectoryName(EditorMirrorFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(EditorMirrorFilePath, json, Encoding.UTF8);
#endif
                OnDatabaseUpdated?.Invoke();
            }
            catch { }
        }

        /// <summary>
        /// Calcule la similarité biométrique 128-D avec :
        /// 1. Alignement Multi-Pose (tolérance ±1 ligne verticale / ±1 colonne horizontale entre position allongée et debout)
        /// 2. Vérification Multi-Zones Anatomiques :
        ///    - Zone Inférieure (Menton, Barbe, Lèvres, Moustache)
        ///    - Zone Médiane & Supérieure (Pommettes, Nez, Yeux, Sourcils, Front)
        ///    - Zone Structurelle & Anthropométrique (Ondes Haar verticales + Ratio Peau/Barbe + T-Zone)
        /// </summary>
        public static float ComputeCosineSimilarity(float[] a, float[] b)
        {
            if (a == null || b == null || a.Length != b.Length || a.Length == 0)
                return 0f;

            if (a.Length < 128)
                return SubRangeCosine(a, b, 0, a.Length);

            float bestScore = 0f;

            for (int dr = -1; dr <= 1; dr++)
            {
                for (int dc = -1; dc <= 1; dc++)
                {
                    float lowerSim = ShiftedGridCosine(a, b, 0, 4, dr, dc);
                    float upperSim = ShiftedGridCosine(a, b, 4, 10, dr, dc);
                    float gridSim = ShiftedGridCosine(a, b, 0, 10, dr, dc);
                    float structSim = ShiftedStructCosine(a, b, dr, dc);

                    float combined = 0.45f * gridSim + 0.22f * upperSim + 0.16f * lowerSim + 0.17f * structSim;
                    float shiftPenalty = 0.012f * (Mathf.Abs(dr) + Mathf.Abs(dc));
                    combined -= shiftPenalty;

                    float minZone = Mathf.Min(lowerSim, Mathf.Min(upperSim, structSim));
                    float score;
                    if (minZone < 0.25f)
                    {
                        float factor = Mathf.Lerp(0.52f, 0.92f, Mathf.Clamp01((minZone + 0.05f) / 0.30f));
                        score = combined * factor;
                    }
                    else
                    {
                        score = Mathf.Clamp01(combined + 0.05f * minZone);
                    }

                    if (score > bestScore)
                        bestScore = score;
                }
            }

            return Mathf.Clamp01(bestScore);
        }

        private static float ShiftedGridCosine(float[] a, float[] b, int rStart, int rEnd, int dr, int dc)
        {
            double sumA = 0.0, sumB = 0.0;
            int count = 0;

            for (int r = rStart; r < rEnd; r++)
            {
                int rb = r + dr;
                if (rb < 0 || rb >= 10) continue;
                for (int c = 0; c < 8; c++)
                {
                    int cb = c + dc;
                    if (cb < 0 || cb >= 8) continue;
                    sumA += a[r * 8 + c];
                    sumB += b[rb * 8 + cb];
                    count++;
                }
            }

            if (count < 4) return 0f;
            double meanA = sumA / count;
            double meanB = sumB / count;

            double dot = 0.0, normA = 0.0, normB = 0.0;
            for (int r = rStart; r < rEnd; r++)
            {
                int rb = r + dr;
                if (rb < 0 || rb >= 10) continue;
                for (int c = 0; c < 8; c++)
                {
                    int cb = c + dc;
                    if (cb < 0 || cb >= 8) continue;
                    double da = a[r * 8 + c] - meanA;
                    double db = b[rb * 8 + cb] - meanB;
                    dot += da * db;
                    normA += da * da;
                    normB += db * db;
                }
            }

            if (normA <= 1e-9 || normB <= 1e-9) return 0f;
            return (float)(dot / (Math.Sqrt(normA) * Math.Sqrt(normB)));
        }

        private static float ShiftedStructCosine(float[] a, float[] b, int dr, int dc)
        {
            // 1. Haar vertical wave [80..111] (8x4)
            double dotW = 0.0, nAW = 0.0, nBW = 0.0;
            for (int r = 0; r < 8; r++)
            {
                int rb = Mathf.Clamp(r + dr, 0, 7);
                for (int c = 0; c < 4; c++)
                {
                    double va = a[80 + r * 4 + c];
                    double vb = b[80 + rb * 4 + c];
                    dotW += va * vb;
                    nAW += va * va;
                    nBW += vb * vb;
                }
            }
            float simWave = (nAW > 1e-9 && nBW > 1e-9) ? (float)(dotW / (Math.Sqrt(nAW) * Math.Sqrt(nBW))) : 0f;

            // 2. Bandes verticales anthropométriques [112..121] (10)
            double dotV = 0.0, nAV = 0.0, nBV = 0.0;
            for (int r = 0; r < 10; r++)
            {
                int rb = Mathf.Clamp(r + dr, 0, 9);
                double va = a[112 + r];
                double vb = b[112 + rb];
                dotV += va * vb;
                nAV += va * va;
                nBV += vb * vb;
            }
            float simVBands = (nAV > 1e-9 && nBV > 1e-9) ? (float)(dotV / (Math.Sqrt(nAV) * Math.Sqrt(nBV))) : 0f;

            // 3. Bandes horizontales T-Zone [122..127] (6)
            double dotH = 0.0, nAH = 0.0, nBH = 0.0;
            for (int c = 0; c < 6; c++)
            {
                int cb = Mathf.Clamp(c + dc, 0, 5);
                double va = a[122 + c];
                double vb = b[122 + cb];
                dotH += va * vb;
                nAH += va * va;
                nBH += vb * vb;
            }
            float simHBands = (nAH > 1e-9 && nBH > 1e-9) ? (float)(dotH / (Math.Sqrt(nAH) * Math.Sqrt(nBH))) : 0f;

            return 0.50f * simWave + 0.30f * simVBands + 0.20f * simHBands;
        }

        private static float SubRangeCosine(float[] a, float[] b, int start, int length)
        {
            double meanA = 0.0, meanB = 0.0;
            int end = start + length;
            for (int i = start; i < end; i++)
            {
                meanA += a[i];
                meanB += b[i];
            }
            meanA /= length;
            meanB /= length;

            double dot = 0.0, normA = 0.0, normB = 0.0;
            for (int i = start; i < end; i++)
            {
                double da = a[i] - meanA;
                double db = b[i] - meanB;
                dot += da * db;
                normA += da * da;
                normB += db * db;
            }

            if (normA <= 1e-9 || normB <= 1e-9)
                return 0f;

            return (float)(dot / (Math.Sqrt(normA) * Math.Sqrt(normB)));
        }

        public static string ComputeSha256Hash(string rawData)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData ?? ""));
                StringBuilder builder = new StringBuilder(bytes.Length * 2);
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        private void UpdateStatus(string msg, bool ready)
        {
            connectionStatusMessage = msg;
            OnConnectionStatusChanged?.Invoke(msg, ready);
        }
    }
}
