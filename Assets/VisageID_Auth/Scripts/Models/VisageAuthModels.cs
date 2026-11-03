using System;
using System.Collections.Generic;
using UnityEngine;

namespace VisageIDAuth.Models
{
    /// <summary>
    /// Représente un document utilisateur dans la collection MongoDB "users" (base: visage_auth_db).
    /// </summary>
    [Serializable]
    public class MongoUserDocument
    {
        public string _id;
        public string username;
        public string email;
        public string passwordHash;
        public bool hasFaceId;
        public float[] faceEmbedding;
        public string createdAt;
        public string lastLoginAt;
        public string lastAuthMethod;

        public static string GenerateObjectId()
        {
            // Génère un ObjectId MongoDB valide de 24 caractères hexadécimaux (timestamp 4 octets + 8 octets aléatoires)
            long unixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string timeHex = ((int)unixSeconds).ToString("x8");
            byte[] randBytes = new byte[8];
            new System.Random(Guid.NewGuid().GetHashCode()).NextBytes(randBytes);
            string randHex = BitConverter.ToString(randBytes).Replace("-", "").ToLowerInvariant();
            return timeHex + randHex;
        }
    }

    /// <summary>
    /// Représente un document d'historique dans la collection MongoDB "login_logs" (base: visage_auth_db).
    /// </summary>
    [Serializable]
    public class MongoLoginLogDocument
    {
        public string _id;
        public string userId;
        public string username;
        public string email;
        public string authMethod; // "FACE_ID" ou "PASSWORD"
        public bool success;
        public float similarityScore;
        public string deviceModel;
        public string platform;
        public string timestamp;
    }

    /// <summary>
    /// Enveloppe de sérialisation pour le cache/miroir local des collections MongoDB.
    /// </summary>
    [Serializable]
    public class LocalDatabaseSnapshot
    {
        public string databaseName = "visage_auth_db";
        public string lastSyncedAt = "";
        public List<MongoUserDocument> users = new List<MongoUserDocument>();
        public List<MongoLoginLogDocument> login_logs = new List<MongoLoginLogDocument>();
    }

    /// <summary>
    /// Résultat d'une opération d'authentification ou d'inscription.
    /// </summary>
    public class AuthResult
    {
        public bool Success;
        public string Message;
        public string AuthMethod;
        public float SimilarityScore;
        public MongoUserDocument User;

        public static AuthResult Ok(MongoUserDocument user, string method, string message, float score = 1f)
        {
            return new AuthResult
            {
                Success = true,
                User = user,
                AuthMethod = method,
                Message = message,
                SimilarityScore = score
            };
        }

        public static AuthResult Fail(string message, float bestScore = 0f)
        {
            return new AuthResult
            {
                Success = false,
                Message = message,
                SimilarityScore = bestScore
            };
        }
    }

    /// <summary>
    /// Session statique accessible depuis n'importe quel script du projet officiel de ton ami.
    /// Exemple : VisageAuthSession.CurrentUser.username
    /// </summary>
    public static class VisageAuthSession
    {
        public static MongoUserDocument CurrentUser { get; private set; }
        public static bool IsLoggedIn => CurrentUser != null;
        public static string LastAuthMethod { get; private set; }
        public static float LastSimilarityScore { get; private set; }

        public static event Action<MongoUserDocument, string> OnUserLoggedIn;
        public static event Action OnUserLoggedOut;

        public static void SetSession(MongoUserDocument user, string authMethod, float score = 1f)
        {
            CurrentUser = user;
            LastAuthMethod = authMethod;
            LastSimilarityScore = score;
            OnUserLoggedIn?.Invoke(user, authMethod);
        }

        public static void ClearSession()
        {
            CurrentUser = null;
            LastAuthMethod = null;
            LastSimilarityScore = 0f;
            OnUserLoggedOut?.Invoke();
        }
    }
}
