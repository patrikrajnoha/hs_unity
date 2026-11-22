using System;
using UnityEngine;

/// <summary>
/// Configuration settings for the local file-based game logging system.
/// </summary>
[Serializable]
public sealed class GameLoggingConfig
{
    [Tooltip("Enables writing logs to local physical files on the player's computer.")]
    public bool enableFileLogging = true;

    [Tooltip("Enables echoing GameLogger events to the standard Unity console.")]
    public bool enableUnityConsoleLogging = true;

    [Tooltip("Automatically captures Unity console errors, warnings, asserts and exceptions.")]
    public bool captureUnityErrors = true;

    [Tooltip("Minimum level of logs to record to file and console.")]
    public GameLogLevel minimumLogLevel = GameLogLevel.Info;

    [Tooltip("Maximum size of an active log file in megabytes before rotating to a new file.")]
    public int maxFileSizeMB = 10;

    [Tooltip("Maximum count of historical session log files retained before oldest are cleaned up.")]
    public int maxLogFiles = 10;

    [Tooltip("Maximum total storage in megabytes allowed for all retained logs in the log directory.")]
    public int maxStorageMB = 100;

    [Tooltip("Background flush interval in seconds.")]
    public float flushIntervalSeconds = 2f;

    [Tooltip("Optional remote Firestore upload. Logging is completely offline by default.")]
    public bool enableFirebaseLogging = false;

    [Tooltip("Optional secondary JSON Lines (.jsonl) structured log format output.")]
    public bool enableJsonlLogging = true;

    [Header("Optional Firebase Configuration")]
    public string apiKey = string.Empty;
    public string projectId = string.Empty;
    public string collection = "gameLogs";
    public string playerId = "anonymous";
}
