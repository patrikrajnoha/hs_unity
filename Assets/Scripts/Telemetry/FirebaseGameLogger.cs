using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

/// <summary>
/// Small, dependency-free Firebase logger for gameplay events.
///
/// Define GAMEPLAY_LOGGING for local event logging and add FIREBASE_LOGGING
/// when the same events should be uploaded to Firestore. The remote path uses
/// Firebase Anonymous Auth + the Firestore REST API, so this project does not
/// need the Firebase Unity SDK just to collect action logs.
/// </summary>
public sealed class FirebaseGameLogger : MonoBehaviour
{
    private const string ConfigResourceName = "FirebaseLoggingConfig";
    private const int MaxPendingEntries = 256;
    private const string DefaultCollection = "gameLogs";

    [Serializable]
    private sealed class FirebaseLoggingConfig
    {
        public string apiKey;
        public string projectId;
        public string collection = DefaultCollection;
        public string playerId;
    }

    [Serializable]
    private sealed class AuthResponse
    {
        public string idToken;
        public string localId;
    }

    [Serializable]
    private sealed class LogEntry
    {
        public string eventId;
        public string timestamp;
        public string level;
        public int severity;
        public string category;
        public string action;
        public string message;
        public string playerId;
        public string sessionId;
        public string scene;
        public string build;
        public string gameId;
        public string metadataJson;
    }

    private static FirebaseGameLogger instance;
    private readonly Queue<LogEntry> pending = new Queue<LogEntry>();
    private string firebaseApiKey;
    private string firebaseProjectId;
    private string collectionName = DefaultCollection;
    private string playerId;
    private string sessionId;
    private string idToken;
    private bool authRequestRunning;
    private bool uploadRequestRunning;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
#if GAMEPLAY_LOGGING || FIREBASE_LOGGING
        EnsureInstance();
#endif
    }

    public static void LogDebug(string category, string action, string message = "")
    {
        Log(GameLogLevel.Debug, category, action, message);
    }

    public static void LogInfo(string category, string action, string message = "")
    {
        Log(GameLogLevel.Info, category, action, message);
    }

    public static void LogWarning(string category, string action, string message = "")
    {
        Log(GameLogLevel.Warning, category, action, message);
    }

    public static void LogError(string category, string action, string message = "")
    {
        Log(GameLogLevel.Error, category, action, message);
    }

    public static void Log(GameLogLevel level, string category, string action, string message = "")
    {
#if GAMEPLAY_LOGGING || FIREBASE_LOGGING
        FirebaseGameLogger logger = EnsureInstance();
        logger.Enqueue(level, category, action, message);
#else
        // Logging is intentionally compiled out in builds without a logging
        // define. This keeps production builds quiet and avoids network work.
#endif
    }

    private static FirebaseGameLogger EnsureInstance()
    {
        if (instance != null)
            return instance;

        GameObject root = new GameObject("FirebaseGameLogger");
        instance = root.AddComponent<FirebaseGameLogger>();
        DontDestroyOnLoad(root);
        instance.LoadConfiguration();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        LoadConfiguration();
    }

    private void LoadConfiguration()
    {
        FirebaseLoggingConfig config = null;
        TextAsset configAsset = Resources.Load<TextAsset>(ConfigResourceName);
        if (configAsset != null)
        {
            try
            {
                config = JsonUtility.FromJson<FirebaseLoggingConfig>(configAsset.text);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[FirebaseGameLogger] Invalid Resources/FirebaseLoggingConfig.json: " + exception.Message);
            }
        }

        firebaseApiKey = config != null ? config.apiKey : string.Empty;
        firebaseProjectId = config != null ? config.projectId : string.Empty;
        collectionName = config != null && !string.IsNullOrWhiteSpace(config.collection)
            ? config.collection
            : DefaultCollection;
        // Keep the default anonymous. A stable player identifier can be
        // supplied from the config when the game has a consented account ID.
        playerId = config != null && !string.IsNullOrWhiteSpace(config.playerId)
            ? config.playerId
            : "anonymous";

        sessionId = Guid.NewGuid().ToString("N");

#if FIREBASE_LOGGING
        if (!IsFirebaseConfigured())
            Debug.LogWarning("[FirebaseGameLogger] FIREBASE_LOGGING is enabled but FirebaseLoggingConfig.json still has placeholders.");
#endif
    }

    private void Enqueue(GameLogLevel level, string category, string action, string message)
    {
        LogEntry entry = new LogEntry
        {
            eventId = Guid.NewGuid().ToString("N"),
            timestamp = DateTime.UtcNow.ToString("o"),
            level = level.ToString().ToUpperInvariant(),
            severity = (int)level,
            category = string.IsNullOrWhiteSpace(category) ? "gameplay" : category,
            action = string.IsNullOrWhiteSpace(action) ? "event" : action,
            message = message ?? string.Empty,
            playerId = playerId,
            sessionId = sessionId,
            scene = SceneManager.GetActiveScene().name,
            build = Application.version,
            gameId = "highstakes",
            metadataJson = "{}"
        };

        while (pending.Count >= MaxPendingEntries)
            pending.Dequeue();
        pending.Enqueue(entry);

#if GAMEPLAY_LOGGING
        Debug.Log($"[GameLog][{entry.level}] {entry.category}/{entry.action}: {entry.message}");
#endif

#if FIREBASE_LOGGING
        FlushIfNeeded();
#endif
    }

#if FIREBASE_LOGGING
    private bool IsFirebaseConfigured()
    {
        return !string.IsNullOrWhiteSpace(firebaseApiKey) &&
               !string.IsNullOrWhiteSpace(firebaseProjectId) &&
               !firebaseApiKey.Contains("REPLACE") &&
               !firebaseProjectId.Contains("REPLACE");
    }

    private void FlushIfNeeded()
    {
        if (!IsFirebaseConfigured() || uploadRequestRunning || authRequestRunning || pending.Count == 0)
            return;

        if (string.IsNullOrWhiteSpace(idToken))
        {
            StartCoroutine(AuthenticateAndFlush());
            return;
        }

        StartCoroutine(UploadNext());
    }

    private IEnumerator AuthenticateAndFlush()
    {
        authRequestRunning = true;
        string url = "https://identitytoolkit.googleapis.com/v1/accounts:signUp?key=" + Uri.EscapeDataString(firebaseApiKey);
        using (UnityWebRequest request = CreateJsonRequest(url, "{\"returnSecureToken\":true}"))
        {
            yield return request.SendWebRequest();
            authRequestRunning = false;

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("[FirebaseGameLogger] Anonymous Firebase auth failed: " + request.error);
                yield break;
            }

            AuthResponse response = JsonUtility.FromJson<AuthResponse>(request.downloadHandler.text);
            idToken = response != null ? response.idToken : string.Empty;
        }

        FlushIfNeeded();
    }

    private IEnumerator UploadNext()
    {
        if (pending.Count == 0 || string.IsNullOrWhiteSpace(idToken))
        {
            uploadRequestRunning = false;
            yield break;
        }

        uploadRequestRunning = true;
        LogEntry entry = pending.Dequeue();
        string documentPath = $"projects/{firebaseProjectId}/databases/(default)/documents/{collectionName}/{entry.eventId}";
        string url = "https://firestore.googleapis.com/v1/projects/" +
                     Uri.EscapeDataString(firebaseProjectId) +
                     "/databases/(default)/documents:commit";
        string body = BuildCommitPayload(documentPath, entry);

        using (UnityWebRequest request = CreateJsonRequest(url, body))
        {
            request.SetRequestHeader("Authorization", "Bearer " + idToken);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                pending.Enqueue(entry);
                if (request.responseCode == 401)
                    idToken = string.Empty;

                Debug.LogWarning("[FirebaseGameLogger] Firestore upload failed: " + request.error);
            }
        }

        uploadRequestRunning = false;
        FlushIfNeeded();
    }

    private static UnityWebRequest CreateJsonRequest(string url, string json)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        UnityWebRequest request = new UnityWebRequest(url, "POST");
        request.uploadHandler = new UploadHandlerRaw(bytes);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        return request;
    }

    private static string BuildCommitPayload(string documentPath, LogEntry entry)
    {
        StringBuilder json = new StringBuilder();
        json.Append("{\"writes\":[{\"update\":{\"name\":\"")
            .Append(EscapeJson(documentPath))
            .Append("\",\"fields\":{")
            .Append(StringField("eventId", entry.eventId)).Append(',')
            .Append(StringField("timestamp", entry.timestamp)).Append(',')
            .Append(StringField("level", entry.level)).Append(',')
            .Append(IntegerField("severity", entry.severity)).Append(',')
            .Append(StringField("category", entry.category)).Append(',')
            .Append(StringField("action", entry.action)).Append(',')
            .Append(StringField("message", entry.message)).Append(',')
            .Append(StringField("playerId", entry.playerId)).Append(',')
            .Append(StringField("sessionId", entry.sessionId)).Append(',')
            .Append(StringField("scene", entry.scene)).Append(',')
            .Append(StringField("build", entry.build)).Append(',')
            .Append(StringField("gameId", entry.gameId)).Append(',')
            .Append(StringField("metadataJson", entry.metadataJson))
            .Append("}}}]}" );
        return json.ToString();
    }

    private static string StringField(string name, string value)
    {
        return "\"" + EscapeJson(name) + "\":{\"stringValue\":\"" + EscapeJson(value) + "\"}";
    }

    private static string IntegerField(string name, int value)
    {
        return "\"" + EscapeJson(name) + "\":{\"integerValue\":\"" + value + "\"}";
    }

    private static string EscapeJson(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t");
    }
#endif
}
