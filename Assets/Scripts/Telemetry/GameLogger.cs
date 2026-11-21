using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

/// <summary>
/// Reliable, production-ready local file logging system for HighStakes.
///
/// Features:
/// - Offline, zero-dependency physical file logging to disk.
/// - Thread-safe asynchronous writer with bounded queue and immediate error flush.
/// - Automatic resolution of writable log directory with persistentDataPath fallback.
/// - Human-readable session header and formatted log entries with millisecond UTC precision.
/// - Automatic rotation based on file size and historical log retention cleanup.
/// - Thread-safe capture of Unity console warnings, errors, assertions, and exceptions.
/// - Recursion and duplication guards.
/// - Optional secondary JSONL output and optional secondary Firebase Firestore upload.
/// </summary>
public sealed class GameLogger : MonoBehaviour
{
    private const string ConfigResourceName = "GameLoggingConfig";
    private const string LegacyConfigResourceName = "FirebaseLoggingConfig";
    private const int MaxQueueCapacity = 4096;

    public struct LogEntry
    {
        public string EventId;
        public string TimestampUtc;
        public GameLogLevel Level;
        public string Category;
        public string Action;
        public string Message;
        public string StackTrace;
        public string Scene;
        public string SessionId;
        public string PlayerId;
        public string BuildVersion;
    }

    private static GameLogger instance;
    private static readonly object initLock = new object();

    [ThreadStatic]
    private static bool isInternalLogging;

    // Configuration and system state
    private GameLoggingConfig config = new GameLoggingConfig();
    private string resolvedLogDirectory = string.Empty;
    private string activeLogFilePath = string.Empty;
    private string activeJsonlFilePath = string.Empty;
    private string sessionId = string.Empty;
    private string cachedPlayerId = "anonymous";
    private string cachedAppVersion = "1.0.0";
    private string cachedUnityVersion = "";
    private string cachedPlatform = "";
    private volatile string cachedCurrentScene = "Bootstrap";
    private string sessionTimestampString = "";
    private int currentPartNumber = 1;

    // Thread-safe event queue and background worker
    private readonly ConcurrentQueue<LogEntry> eventQueue = new ConcurrentQueue<LogEntry>();
    // Synchronization objects
    private readonly object fileLock = new object();
    private readonly object queueLock = new object();
    private Thread writerThread;
    private readonly AutoResetEvent wakeEvent = new AutoResetEvent(false);
    private volatile bool isWorkerRunning;
    private int droppedEventCount;
    private int shutdownExecuted;

    // File I/O
    private FileStream activeLogStream;
    private StreamWriter activeLogWriter;
    private FileStream activeJsonlStream;
    private StreamWriter activeJsonlWriter;
    private long currentLogFileSizeBytes;
    private readonly UTF8Encoding utf8Encoding = new UTF8Encoding(false);
    private bool fileLoggingUnavailable;

    // Optional Firebase state
    private string firebaseIdToken = string.Empty;
    private bool firebaseAuthRunning;
    private bool firebaseUploadRunning;
    private readonly Queue<LogEntry> firebasePendingQueue = new Queue<LogEntry>();

    // Public API properties
    public static GameLogger Instance => EnsureInstance();
    public static string LogDirectory => instance != null ? instance.resolvedLogDirectory : string.Empty;
    public static string CurrentLogFilePath => instance != null ? instance.activeLogFilePath : string.Empty;
    public static string CurrentJsonlFilePath => instance != null ? instance.activeJsonlFilePath : string.Empty;
    public static string SessionId => instance != null ? instance.sessionId : string.Empty;
    public static GameLoggingConfig Config => instance != null ? instance.config : null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    public static GameLogger EnsureInstance()
    {
        if (instance != null)
            return instance;

        lock (initLock)
        {
            if (instance != null)
                return instance;

            GameObject root = new GameObject("GameLogger");
            instance = root.AddComponent<GameLogger>();
            if (Application.isPlaying)
                DontDestroyOnLoad(root);
            return instance;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        if (Application.isPlaying)
            DontDestroyOnLoad(gameObject);
        InitializeSystem();
    }

    private void InitializeSystem()
    {
        LoadConfiguration();

        // Cache main-thread metadata
        cachedAppVersion = Application.version;
        cachedUnityVersion = Application.unityVersion;
        cachedPlatform = Application.platform.ToString();
        try
        {
            cachedCurrentScene = SceneManager.GetActiveScene().name;
            if (string.IsNullOrEmpty(cachedCurrentScene))
                cachedCurrentScene = "Initial";
        }
        catch
        {
            cachedCurrentScene = "Initial";
        }

        SceneManager.sceneLoaded += OnSceneLoaded;

        // Create session ID
        sessionId = Guid.NewGuid().ToString("N").Substring(0, 8);
        sessionTimestampString = DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss");

        // Resolve log directory
        resolvedLogDirectory = ResolveLogDirectory();
        Debug.Log($"[GameLogger] Resolved log directory: {resolvedLogDirectory}");

        if (config.enableFileLogging)
        {
            EnforceRetentionCleanup();
            OpenNewSessionFiles();
            StartWriterWorker();
        }

        if (config.captureUnityErrors)
        {
            Application.logMessageReceivedThreaded -= OnUnityLogMessageReceived;
            Application.logMessageReceivedThreaded += OnUnityLogMessageReceived;
        }

        AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

        LogInfo("GAME", "game_started", $"Game initialized - Version: {cachedAppVersion}, Unity: {cachedUnityVersion}, Platform: {cachedPlatform}");
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        cachedCurrentScene = scene.name;
        LogInfo("GAME", "scene_loaded", $"Loaded scene: {scene.name} (BuildIndex: {scene.buildIndex}, Mode: {mode})");
    }

    #region Configuration

    private void LoadConfiguration()
    {
        TextAsset configAsset = Resources.Load<TextAsset>(ConfigResourceName);
        if (configAsset == null)
            configAsset = Resources.Load<TextAsset>(LegacyConfigResourceName);

        if (configAsset != null && !string.IsNullOrWhiteSpace(configAsset.text))
        {
            try
            {
                config = JsonUtility.FromJson<GameLoggingConfig>(configAsset.text);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GameLogger] Could not parse {configAsset.name}.json: {ex.Message}. Using default settings.");
                config = new GameLoggingConfig();
            }
        }
        else
        {
            config = new GameLoggingConfig();
        }

        if (config == null)
            config = new GameLoggingConfig();

        cachedPlayerId = !string.IsNullOrWhiteSpace(config.playerId) ? config.playerId : "anonymous";
    }

    #endregion

    #region Directory Resolution & File Management

    /// <summary>
    /// Resolves the log directory using a safe multi-tier strategy:
    /// 1. In Unity Editor: Application.persistentDataPath/Logs (avoid dirtying version control).
    /// 2. In Standalone: Try &lt;GameName&gt;_Data/Logs/, then beside executable, verify writable with probe.
    /// 3. Fallback: Application.persistentDataPath/Logs.
    /// 4. Never crash if write is restricted.
    /// </summary>
    private string ResolveLogDirectory()
    {
        if (Application.isEditor)
        {
            string editorLogs = Path.Combine(Application.persistentDataPath, "Logs");
            if (TryCreateAndVerifyWritable(editorLogs))
                return editorLogs;
        }

        // Standalone Player: Tier 1 - Inside <GameName>_Data/Logs
        try
        {
            string dataLogs = Path.Combine(Application.dataPath, "Logs");
            if (TryCreateAndVerifyWritable(dataLogs))
                return dataLogs;
        }
        catch { }

        // Tier 2 - Beside game executable: <BaseDirectory>/Logs
        try
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            if (!string.IsNullOrWhiteSpace(baseDirectory))
            {
                string baseLogs = Path.Combine(baseDirectory, "Logs");
                if (TryCreateAndVerifyWritable(baseLogs))
                    return baseLogs;
            }
        }
        catch { }

        // Tier 3 - Persistent data path
        try
        {
            string persistentLogs = Path.Combine(Application.persistentDataPath, "Logs");
            if (TryCreateAndVerifyWritable(persistentLogs))
                return persistentLogs;
        }
        catch { }

        // Tier 4 - System temporary directory fallback
        try
        {
            string tempLogs = Path.Combine(Path.GetTempPath(), "HighStakes", "Logs");
            if (TryCreateAndVerifyWritable(tempLogs))
                return tempLogs;
        }
        catch { }

        // If all fail, return persistentDataPath without throwing
        fileLoggingUnavailable = true;
        return Path.Combine(Application.persistentDataPath, "Logs");
    }

    private static bool TryCreateAndVerifyWritable(string dirPath)
    {
        if (string.IsNullOrWhiteSpace(dirPath))
            return false;

        try
        {
            if (!Directory.Exists(dirPath))
                Directory.CreateDirectory(dirPath);

            string probeFile = Path.Combine(dirPath, $".probe_{Guid.NewGuid():N}.tmp");
            using (FileStream fs = new FileStream(probeFile, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                byte[] testBytes = Encoding.UTF8.GetBytes("probe");
                fs.Write(testBytes, 0, testBytes.Length);
            }
            File.Delete(probeFile);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void OpenNewSessionFiles()
    {
        if (fileLoggingUnavailable || string.IsNullOrWhiteSpace(resolvedLogDirectory))
            return;

        try
        {
            if (!Directory.Exists(resolvedLogDirectory))
                Directory.CreateDirectory(resolvedLogDirectory);

            string partSuffix = currentPartNumber > 1 ? $"_{currentPartNumber:D2}" : string.Empty;
            string baseFileName = $"game_{sessionTimestampString}_{sessionId}{partSuffix}";

            activeLogFilePath = Path.Combine(resolvedLogDirectory, $"{baseFileName}.log");
            activeLogStream = new FileStream(activeLogFilePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
            activeLogWriter = new StreamWriter(activeLogStream, utf8Encoding) { AutoFlush = false };

            WriteHeader(activeLogWriter);
            activeLogWriter.Flush();
            activeLogStream.Flush();
            currentLogFileSizeBytes = activeLogStream.Length;

            if (config.enableJsonlLogging)
            {
                activeJsonlFilePath = Path.Combine(resolvedLogDirectory, $"{baseFileName}.jsonl");
                activeJsonlStream = new FileStream(activeJsonlFilePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                activeJsonlWriter = new StreamWriter(activeJsonlStream, utf8Encoding) { AutoFlush = false };
            }
        }
        catch (Exception ex)
        {
            fileLoggingUnavailable = true;
            Debug.LogError($"[GameLogger] Failed to open log file at '{activeLogFilePath}': {ex.Message}");
        }
    }

    private void WriteHeader(StreamWriter writer)
    {
        writer.WriteLine("==================================================");
        writer.WriteLine("HIGHSTAKES GAME LOG");
        writer.WriteLine("==================================================");
        writer.WriteLine($"Started UTC: {DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}");
        writer.WriteLine($"Game version: {cachedAppVersion}");
        writer.WriteLine($"Unity version: {cachedUnityVersion}");
        writer.WriteLine($"Platform: {cachedPlatform}");
        writer.WriteLine($"Session ID: {sessionId}");
        if (currentPartNumber > 1)
            writer.WriteLine($"Log part: {currentPartNumber}");
        writer.WriteLine($"Log directory: {resolvedLogDirectory}");
        writer.WriteLine("==================================================");
    }

    private void CloseCurrentFiles()
    {
        try
        {
            if (activeLogWriter != null)
            {
                activeLogWriter.Flush();
                activeLogWriter.Dispose();
                activeLogWriter = null;
            }
            if (activeLogStream != null)
            {
                activeLogStream.Dispose();
                activeLogStream = null;
            }

            if (activeJsonlWriter != null)
            {
                activeJsonlWriter.Flush();
                activeJsonlWriter.Dispose();
                activeJsonlWriter = null;
            }
            if (activeJsonlStream != null)
            {
                activeJsonlStream.Dispose();
                activeJsonlStream = null;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[GameLogger] Error closing log files: {ex.Message}");
        }
    }

    private void RotateLogFile()
    {
        CloseCurrentFiles();
        currentPartNumber++;
        OpenNewSessionFiles();
        EnforceRetentionCleanup();
    }

    /// <summary>
    /// Enforces maximum log retention count and disk storage limits.
    /// Only removes matching historical session log files (game_*.log / game_*.jsonl).
    /// </summary>
    private void EnforceRetentionCleanup()
    {
        if (string.IsNullOrWhiteSpace(resolvedLogDirectory) || !Directory.Exists(resolvedLogDirectory))
            return;

        try
        {
            DirectoryInfo dir = new DirectoryInfo(resolvedLogDirectory);
            List<FileInfo> files = new List<FileInfo>();
            files.AddRange(dir.GetFiles("game_*.log"));
            files.AddRange(dir.GetFiles("game_*.jsonl"));

            // Exclude currently active files
            files.RemoveAll(f => string.Equals(f.FullName, activeLogFilePath, StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(f.FullName, activeJsonlFilePath, StringComparison.OrdinalIgnoreCase));

            // Sort oldest first
            files.Sort((a, b) => a.CreationTimeUtc.CompareTo(b.CreationTimeUtc));

            long maxStorageBytes = (long)Math.Max(5, config.maxStorageMB) * 1024L * 1024L;
            int maxFiles = Math.Max(1, config.maxLogFiles);

            long totalBytes = 0;
            foreach (FileInfo f in files)
                totalBytes += f.Length;

            int index = 0;
            while (index < files.Count && (files.Count - index > maxFiles || totalBytes > maxStorageBytes))
            {
                FileInfo victim = files[index];
                long size = victim.Length;
                try
                {
                    victim.Delete();
                    totalBytes -= size;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[GameLogger] Could not delete old log file '{victim.Name}': {ex.Message}");
                }
                index++;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[GameLogger] Retention cleanup error: {ex.Message}");
        }
    }

    #endregion

    #region Asynchronous Writer Worker

    private void StartWriterWorker()
    {
        isWorkerRunning = true;
        writerThread = new Thread(WriterWorkerLoop)
        {
            Name = "GameLogger-Writer",
            IsBackground = true,
            Priority = System.Threading.ThreadPriority.BelowNormal
        };
        writerThread.Start();
    }

    private void WriterWorkerLoop()
    {
        int intervalMs = Mathf.Clamp(Mathf.RoundToInt(config.flushIntervalSeconds * 1000f), 200, 10000);

        while (isWorkerRunning)
        {
            wakeEvent.WaitOne(intervalMs);
            ProcessEventQueue();
        }

        // Final drain on exit
        ProcessEventQueue();
    }

    private void ProcessEventQueue()
    {
        // Ensure exclusive access to file I/O and queue processing
        lock (fileLock)
        {
            if (fileLoggingUnavailable || activeLogWriter == null)
                return;

            // Reset dropped count atomically
            int dropped = Interlocked.Exchange(ref droppedEventCount, 0);
            if (dropped > 0)
            {
                WriteDirectToFile(new LogEntry
                {
                    TimestampUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    Level = GameLogLevel.Warning,
                    Category = "LOG_SYSTEM",
                    Action = "queue_overflow",
                    Message = $"Dropped {dropped} log events due to full queue buffer.",
                    Scene = cachedCurrentScene,
                    SessionId = sessionId,
                    PlayerId = cachedPlayerId,
                    BuildVersion = cachedAppVersion
                });
            }

            while (eventQueue.TryDequeue(out LogEntry entry))
            {
                WriteDirectToFile(entry);
            }

            try
            {
                if (activeLogWriter != null)
                    activeLogWriter.Flush();
                if (activeLogStream != null)
                    activeLogStream.Flush();

                if (activeJsonlWriter != null)
                    activeJsonlWriter.Flush();
                if (activeJsonlStream != null)
                    activeJsonlStream.Flush();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GameLogger] Disk flush warning: {ex.Message}");
            }
        }
    }

    private void WriteDirectToFile(LogEntry entry)
    {
        if (activeLogWriter == null)
            return;

        try
        {
            long maxBytes = (long)Math.Max(1, config.maxFileSizeMB) * 1024L * 1024L;

            // Formatted line: [2026-11-21 21:30:18.145 UTC] [INFO] [GAME] game_started | Game initialized
            string levelString = entry.Level.ToString().ToUpperInvariant();
            string categoryString = entry.Category.ToUpperInvariant();

            string line = $"[{entry.TimestampUtc} UTC] [{levelString}] [{categoryString}] {entry.Action} | {entry.Message}";

            // Estimate bytes
            long estimatedBytes = (long)Encoding.UTF8.GetByteCount(line) + 2L;
            if (!string.IsNullOrEmpty(entry.StackTrace))
                estimatedBytes += (long)Encoding.UTF8.GetByteCount(entry.StackTrace) + 30L;

            if (currentLogFileSizeBytes + estimatedBytes > maxBytes)
            {
                RotateLogFile();
                if (activeLogWriter == null)
                    return;
            }

            activeLogWriter.WriteLine(line);
            currentLogFileSizeBytes += (long)Encoding.UTF8.GetByteCount(line) + 2L;

            if (!string.IsNullOrEmpty(entry.StackTrace))
            {
                string stackHeader = "Stack trace:";
                activeLogWriter.WriteLine(stackHeader);
                currentLogFileSizeBytes += (long)Encoding.UTF8.GetByteCount(stackHeader) + 2L;

                using (StringReader reader = new StringReader(entry.StackTrace))
                {
                    string stackLine;
                    while ((stackLine = reader.ReadLine()) != null)
                    {
                        string indented = "  " + stackLine;
                        activeLogWriter.WriteLine(indented);
                        currentLogFileSizeBytes += (long)Encoding.UTF8.GetByteCount(indented) + 2L;
                    }
                }
            }

            if (config.enableJsonlLogging && activeJsonlWriter != null)
            {
                string jsonLine = FormatJsonlEntry(entry);
                activeJsonlWriter.WriteLine(jsonLine);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[GameLogger] File write error: {ex.Message}");
        }
    }

    private static string FormatJsonlEntry(LogEntry entry)
    {
        StringBuilder sb = new StringBuilder(256);
        sb.Append('{');
        sb.Append("\"timestamp\":\"").Append(EscapeJson(entry.TimestampUtc)).Append("\",");
        sb.Append("\"level\":\"").Append(entry.Level.ToString().ToUpperInvariant()).Append("\",");
        sb.Append("\"category\":\"").Append(EscapeJson(entry.Category)).Append("\",");
        sb.Append("\"action\":\"").Append(EscapeJson(entry.Action)).Append("\",");
        sb.Append("\"message\":\"").Append(EscapeJson(entry.Message)).Append("\",");
        if (!string.IsNullOrEmpty(entry.StackTrace))
            sb.Append("\"stackTrace\":\"").Append(EscapeJson(entry.StackTrace)).Append("\",");
        sb.Append("\"scene\":\"").Append(EscapeJson(entry.Scene)).Append("\",");
        sb.Append("\"sessionId\":\"").Append(EscapeJson(entry.SessionId)).Append("\",");
        sb.Append("\"playerId\":\"").Append(EscapeJson(entry.PlayerId)).Append("\",");
        sb.Append("\"build\":\"").Append(EscapeJson(entry.BuildVersion)).Append("\"");
        sb.Append('}');
        return sb.ToString();
    }

    private static string EscapeJson(string s)
    {
        if (string.IsNullOrEmpty(s))
            return string.Empty;

        return s.Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t");
    }

    #endregion

    #region Public Logging API

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

    public static void LogError(string category, string action, string message = "", Exception exception = null)
    {
        Log(GameLogLevel.Error, category, action, message, exception);
    }

    public static void LogCritical(string category, string action, string message = "", Exception exception = null)
    {
        Log(GameLogLevel.Critical, category, action, message, exception);
    }

    public static void LogException(Exception exception, string category = "EXCEPTION", string action = "unhandled", string message = "")
    {
        if (exception == null)
            return;

        string fullMessage = !string.IsNullOrWhiteSpace(message)
            ? $"{message} | {exception.GetType().Name}: {exception.Message}"
            : $"{exception.GetType().Name}: {exception.Message}";

        Log(GameLogLevel.Error, category, action, fullMessage, exception, exception.StackTrace);
    }

    public static void Log(GameLogLevel level, string category, string action, string message = "", Exception exception = null, string stackTrace = null)
    {
        GameLogger logger = EnsureInstance();
        if (logger == null)
            return;

        if (level < logger.config.minimumLogLevel)
            return;

        string finalStackTrace = stackTrace;
        if (string.IsNullOrEmpty(finalStackTrace) && exception != null)
            finalStackTrace = exception.StackTrace;

        logger.EnqueueLog(level, category, action, message, finalStackTrace);
    }

    private void EnqueueLog(GameLogLevel level, string category, string action, string message, string stackTrace)
    {
        string normCategory = string.IsNullOrWhiteSpace(category) ? "GAMEPLAY" : category.Trim();
        string normAction = string.IsNullOrWhiteSpace(action) ? "event" : action.Trim();
        string normMessage = message ?? string.Empty;

        LogEntry entry = new LogEntry
        {
            EventId = Guid.NewGuid().ToString("N"),
            TimestampUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff"),
            Level = level,
            Category = normCategory,
            Action = normAction,
            Message = normMessage,
            StackTrace = stackTrace,
            Scene = cachedCurrentScene,
            SessionId = sessionId,
            PlayerId = cachedPlayerId,
            BuildVersion = cachedAppVersion
        };

        if (config.enableFileLogging)
        {
            if (eventQueue.Count >= MaxQueueCapacity)
            {
                Interlocked.Increment(ref droppedEventCount);
            }
            else
            {
                eventQueue.Enqueue(entry);
            }

            // Immediately wake writer on ERROR and CRITICAL
            if (level >= GameLogLevel.Error)
            {
                wakeEvent.Set();
            }
        }

        // Echo to Unity Console with recursion guard
        if (config.enableUnityConsoleLogging)
        {
            EchoToUnityConsole(entry);
        }

        // Optional secondary Firebase upload
        if (config.enableFirebaseLogging && IsFirebaseConfigured())
        {
            EnqueueFirebaseEntry(entry);
        }
    }

    private void EchoToUnityConsole(LogEntry entry)
    {
        try
        {
            isInternalLogging = true;
            string consoleText = $"[GameLog][{entry.Level.ToString().ToUpperInvariant()}] {entry.Category}/{entry.Action}: {entry.Message}";

            switch (entry.Level)
            {
                case GameLogLevel.Debug:
                case GameLogLevel.Info:
                    Debug.Log(consoleText);
                    break;
                case GameLogLevel.Warning:
                    Debug.LogWarning(consoleText);
                    break;
                case GameLogLevel.Error:
                case GameLogLevel.Critical:
                    if (!string.IsNullOrEmpty(entry.StackTrace))
                        consoleText += "\n" + entry.StackTrace;
                    Debug.LogError(consoleText);
                    break;
            }
        }
        finally
        {
            isInternalLogging = false;
        }
    }

    #endregion

    #region Unity Threaded Log Callback

    /// <summary>
    /// Thread-safe capture of Unity errors, warnings, asserts, and exceptions.
    /// Does not call any main-thread Unity APIs.
    /// </summary>
    private void OnUnityLogMessageReceived(string condition, string stackTrace, LogType type)
    {
        if (isInternalLogging)
            return;

        if (string.IsNullOrEmpty(condition))
            return;

        // Skip internal logs to avoid duplicate entries and recursion
        if (condition.StartsWith("[GameLog]") || condition.StartsWith("[GameLogger]"))
            return;

        GameLogLevel level;
        switch (type)
        {
            case LogType.Assert:
                level = GameLogLevel.Error;
                break;
            case LogType.Error:
                level = GameLogLevel.Error;
                break;
            case LogType.Exception:
                level = GameLogLevel.Critical;
                break;
            case LogType.Warning:
                level = GameLogLevel.Warning;
                break;
            default:
                // Only capture warnings, errors, asserts, and exceptions from Unity console
                return;
        }

        if (level < config.minimumLogLevel)
            return;

        LogEntry entry = new LogEntry
        {
            EventId = Guid.NewGuid().ToString("N"),
            TimestampUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff"),
            Level = level,
            Category = "UNITY",
            Action = type.ToString().ToLowerInvariant(),
            Message = condition,
            StackTrace = stackTrace,
            Scene = cachedCurrentScene, // Read from thread-safe cached string
            SessionId = sessionId,
            PlayerId = cachedPlayerId,
            BuildVersion = cachedAppVersion
        };

        if (config.enableFileLogging)
        {
            if (eventQueue.Count >= MaxQueueCapacity)
            {
                Interlocked.Increment(ref droppedEventCount);
            }
            else
            {
                eventQueue.Enqueue(entry);
            }

            if (level >= GameLogLevel.Error)
            {
                wakeEvent.Set();
            }
        }
    }

    #endregion

    #region Developer Utilities & Shutdown

    public static void Flush()
    {
        if (instance != null)
        {
            instance.wakeEvent.Set();
            instance.ProcessEventQueue();
        }
    }

    public static void OpenLogFolder()
    {
        string path = LogDirectory;
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogWarning("[GameLogger] Log directory is not yet resolved.");
            return;
        }

        if (!Directory.Exists(path))
        {
            try
            {
                Directory.CreateDirectory(path);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameLogger] Could not create log folder '{path}': {ex.Message}");
                return;
            }
        }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        System.Diagnostics.Process.Start("explorer.exe", path.Replace('/', '\\'));
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        System.Diagnostics.Process.Start("open", path);
#else
        System.Diagnostics.Process.Start("xdg-open", path);
#endif
    }

    private void OnApplicationQuit()
    {
        Shutdown();
    }

    private void OnDestroy()
    {
        Shutdown();
    }

    private void OnProcessExit(object sender, EventArgs e)
    {
        Shutdown();
    }

    private void Shutdown()
    {
        if (Interlocked.Exchange(ref shutdownExecuted, 1) != 0)
            return;

        try
        {
            Application.logMessageReceivedThreaded -= OnUnityLogMessageReceived;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
        }
        catch { }

        LogInfo("GAME", "game_shutdown", "Application shutdown requested. Draining log queue.");

        isWorkerRunning = false;
        wakeEvent.Set();

        if (writerThread != null && writerThread.IsAlive)
        {
            try
            {
                // Bounded wait of 2000ms for thread to finish draining
                writerThread.Join(2000);
            }
            catch { }
        }

        // Final direct drain
        ProcessEventQueue();
        CloseCurrentFiles();
    }

    #endregion

    #region Optional Firebase Firestore Forwarding

    private bool IsFirebaseConfigured()
    {
        return !string.IsNullOrWhiteSpace(config.apiKey) &&
               !string.IsNullOrWhiteSpace(config.projectId) &&
               !config.apiKey.Contains("REPLACE") &&
               !config.projectId.Contains("REPLACE");
    }

    private void EnqueueFirebaseEntry(LogEntry entry)
    {
        lock (firebasePendingQueue)
        {
            while (firebasePendingQueue.Count >= 256)
                firebasePendingQueue.Dequeue();

            firebasePendingQueue.Enqueue(entry);
        }

        TriggerFirebaseFlush();
    }

    private void TriggerFirebaseFlush()
    {
        if (!IsFirebaseConfigured() || firebaseUploadRunning || firebaseAuthRunning)
            return;

        lock (firebasePendingQueue)
        {
            if (firebasePendingQueue.Count == 0)
                return;
        }

        if (string.IsNullOrWhiteSpace(firebaseIdToken))
        {
            StartCoroutine(AuthenticateFirebase());
            return;
        }

        StartCoroutine(UploadNextFirebaseEntry());
    }

    private IEnumerator AuthenticateFirebase()
    {
        firebaseAuthRunning = true;
        string url = "https://identitytoolkit.googleapis.com/v1/accounts:signUp?key=" + Uri.EscapeDataString(config.apiKey);
        using (UnityWebRequest request = CreateJsonWebRequest(url, "{\"returnSecureToken\":true}"))
        {
            yield return request.SendWebRequest();
            firebaseAuthRunning = false;

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("[GameLogger] Optional Firebase anonymous auth failed: " + request.error);
                yield break;
            }

            try
            {
                FirebaseGameLogger.AuthResponse response = JsonUtility.FromJson<FirebaseGameLogger.AuthResponse>(request.downloadHandler.text);
                firebaseIdToken = response != null ? response.idToken : string.Empty;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GameLogger] Firebase auth parse error: " + ex.Message);
            }
        }

        TriggerFirebaseFlush();
    }

    private IEnumerator UploadNextFirebaseEntry()
    {
        LogEntry entry;
        lock (firebasePendingQueue)
        {
            if (firebasePendingQueue.Count == 0 || string.IsNullOrWhiteSpace(firebaseIdToken))
            {
                firebaseUploadRunning = false;
                yield break;
            }
            entry = firebasePendingQueue.Dequeue();
        }

        firebaseUploadRunning = true;
        string collectionName = !string.IsNullOrWhiteSpace(config.collection) ? config.collection : "gameLogs";
        string documentPath = $"projects/{config.projectId}/databases/(default)/documents/{collectionName}/{entry.EventId}";
        string url = "https://firestore.googleapis.com/v1/projects/" +
                     Uri.EscapeDataString(config.projectId) +
                     "/databases/(default)/documents:commit";
        string body = BuildFirebaseCommitPayload(documentPath, entry);

        using (UnityWebRequest request = CreateJsonWebRequest(url, body))
        {
            request.SetRequestHeader("Authorization", "Bearer " + firebaseIdToken);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                if (request.responseCode == 401)
                    firebaseIdToken = string.Empty;

                Debug.LogWarning("[GameLogger] Optional Firebase upload failed: " + request.error);
            }
        }

        firebaseUploadRunning = false;
        TriggerFirebaseFlush();
    }

    private static UnityWebRequest CreateJsonWebRequest(string url, string json)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        UnityWebRequest request = new UnityWebRequest(url, "POST");
        request.uploadHandler = new UploadHandlerRaw(bytes);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        return request;
    }

    private static string BuildFirebaseCommitPayload(string documentPath, LogEntry entry)
    {
        StringBuilder json = new StringBuilder();
        json.Append("{\"writes\":[{\"update\":{\"name\":\"")
            .Append(EscapeJson(documentPath))
            .Append("\",\"fields\":{")
            .Append(StringField("eventId", entry.EventId)).Append(',')
            .Append(StringField("timestamp", entry.TimestampUtc)).Append(',')
            .Append(StringField("level", entry.Level.ToString())).Append(',')
            .Append(IntegerField("severity", (int)entry.Level)).Append(',')
            .Append(StringField("category", entry.Category)).Append(',')
            .Append(StringField("action", entry.Action)).Append(',')
            .Append(StringField("message", entry.Message)).Append(',')
            .Append(StringField("playerId", entry.PlayerId)).Append(',')
            .Append(StringField("sessionId", entry.SessionId)).Append(',')
            .Append(StringField("scene", entry.Scene)).Append(',')
            .Append(StringField("build", entry.BuildVersion)).Append(',')
            .Append(StringField("gameId", "highstakes"))
            .Append("}}}]}");
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

    #endregion
}
