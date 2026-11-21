using System;
using UnityEngine;

/// <summary>
/// Backward-compatibility wrapper for GameLogger.
/// All telemetry calls are automatically forwarded to the local, offline GameLogger.
/// </summary>
public sealed class FirebaseGameLogger : MonoBehaviour
{
    [Serializable]
    public sealed class AuthResponse
    {
        public string idToken;
        public string localId;
    }

    public static void LogDebug(string category, string action, string message = "")
    {
        GameLogger.LogDebug(category, action, message);
    }

    public static void LogInfo(string category, string action, string message = "")
    {
        GameLogger.LogInfo(category, action, message);
    }

    public static void LogWarning(string category, string action, string message = "")
    {
        GameLogger.LogWarning(category, action, message);
    }

    public static void LogError(string category, string action, string message = "")
    {
        GameLogger.LogError(category, action, message);
    }

    public static void Log(GameLogLevel level, string category, string action, string message = "")
    {
        GameLogger.Log(level, category, action, message);
    }
}
