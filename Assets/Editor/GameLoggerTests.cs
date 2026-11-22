using System.Reflection;
using System.Threading.Tasks;
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Automated verification tests for GameLogger local file-based logging.
/// </summary>
public static class GameLoggerTests
{
    // Batch entry point without -quit: the campaign check exits after Play Mode.
    public static void RunAllAndCampaign()
    {
        RunAllTests();
        CampaignRuntimeChecks.Start();
    }

    private static int testsPassed = 0;
    private static int testsFailed = 0;
    public static bool AllPassed => testsFailed == 0;

    [MenuItem("HighStakes/Tests/Run GameLogger Verification Tests", false, 200)]
    [MenuItem("Tools/HighStakes/Run GameLogger Tests", false, 200)]
    public static void RunAllTests()
    {
        testsPassed = 0;
        testsFailed = 0;

        Debug.Log("==================================================");
        Debug.Log("[GameLoggerTests] Starting GameLogger Verification Tests...");
        Debug.Log("==================================================");

        Test1_LoggerInitializationAndDirectory();
        Test2_WriteBasicEventsAndFlush();
        Test3_ExceptionWithStackTrace();
        Test4_UnityConsoleCapture();
        Test5_LogRotationLogic();
        Test6_RetentionCleanupLogic();
        Test7_CompatibilityFacade();
        Test8_QueueCapacityRecovery();
        Test9_ConcurrentProducers();
        Test11_QueueAccountingAndConcurrency();
        Test10_ShutdownDrainsAndCloses();

        Debug.Log("==================================================");
        if (testsFailed == 0)
        {
            Debug.Log($"[GameLoggerTests] ALL TESTS PASSED! ({testsPassed} passed, 0 failed)");
        }
        else
        {
            Debug.LogError($"[GameLoggerTests] TESTS FINISHED WITH FAILURES: {testsPassed} passed, {testsFailed} failed.");
        }
        Debug.Log("==================================================");
        if (testsFailed > 0) throw new InvalidOperationException("GameLogger verification failed: " + testsFailed);
    }

    private static void Assert(bool condition, string testName, string details = "")
    {
        if (condition)
        {
            testsPassed++;
            Debug.Log($"[PASS] {testName}");
        }
        else
        {
            testsFailed++;
            Debug.LogError($"[FAIL] {testName} - {details}");
        }
    }

    private static void Test1_LoggerInitializationAndDirectory()
    {
        GameLogger logger = GameLogger.EnsureInstance();
        Assert(logger != null, "Test 1.1: GameLogger instance exists");

        string dir = GameLogger.LogDirectory;
        Assert(!string.IsNullOrEmpty(dir), "Test 1.2: LogDirectory is resolved", dir);
        Assert(Directory.Exists(dir), "Test 1.3: LogDirectory physically exists on disk", dir);

        string currentFile = GameLogger.CurrentLogFilePath;
        Assert(!string.IsNullOrEmpty(currentFile), "Test 1.4: CurrentLogFilePath is set", currentFile);
        Assert(File.Exists(currentFile), "Test 1.5: Active session log file physically exists", currentFile);
    }

    private static void Test2_WriteBasicEventsAndFlush()
    {
        string uniqueAction = "test_action_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        string uniqueMessage = "Verification test message " + DateTime.UtcNow.Ticks;

        GameLogger.LogInfo("TEST", uniqueAction, uniqueMessage);
        GameLogger.Flush();

        string currentFile = GameLogger.CurrentLogFilePath;
        Assert(File.Exists(currentFile), "Test 2.1: Log file exists after write");

        string content = ReadFileWithRetry(currentFile);
        Assert(content.Contains("HIGHSTAKES GAME LOG"), "Test 2.2: Log contains readable session header");
        Assert(content.Contains(uniqueAction), "Test 2.3: Log contains recorded action name");
        Assert(content.Contains(uniqueMessage), "Test 2.4: Log contains recorded message text");
        Assert(content.Contains("[INFO] [TEST]"), "Test 2.5: Log contains level and category tag");
    }

    private static void Test3_ExceptionWithStackTrace()
    {
        bool echo = GameLogger.Config.enableUnityConsoleLogging;
        GameLogger.Config.enableUnityConsoleLogging = false;
        string uniqueAction = "exception_test_" + Guid.NewGuid().ToString("N").Substring(0, 6);
        try
        {
            throw new InvalidOperationException("Simulated diagnostic error for GameLogger verification");
        }
        catch (Exception ex)
        {
            GameLogger.LogError("TEST", uniqueAction, "Caught simulated exception", ex);
        }
        finally { GameLogger.Config.enableUnityConsoleLogging = echo; }

        GameLogger.Flush();

        string content = ReadFileWithRetry(GameLogger.CurrentLogFilePath);
        Assert(content.Contains(uniqueAction), "Test 3.1: Log contains exception action");
        Assert(content.Contains("[ERROR] [TEST]"), "Test 3.2: Log contains ERROR level");
        Assert(content.Contains("Stack trace:"), "Test 3.3: Log contains Stack trace header");
        Assert(content.Contains("Test3_ExceptionWithStackTrace"), "Test 3.4: Stack trace contains calling method name");
    }

    private static void Test4_UnityConsoleCapture()
    {
        string uniqueMarker = "UnityConsoleCaptureProbe_" + Guid.NewGuid().ToString("N").Substring(0, 6);
        Debug.LogWarning(uniqueMarker);

        GameLogger.Flush();

        string content = ReadFileWithRetry(GameLogger.CurrentLogFilePath);
        Assert(content.Contains(uniqueMarker), "Test 4.1: Unity Debug.LogWarning was captured by GameLogger");
        Assert(content.Contains("[WARNING] [UNITY]"), "Test 4.2: Unity message tagged as [WARNING] [UNITY]");
    }


    private static void Test5_LogRotationLogic()
    {
        GameLogger.Flush();
        var config = GameLogger.Config;
        int oldSize = config.maxFileSizeMB;
        int oldFiles = config.maxLogFiles;
        bool oldEcho = config.enableUnityConsoleLogging;
        string marker = "rotation_" + Guid.NewGuid().ToString("N");
        string firstPath = GameLogger.CurrentLogFilePath;
        try
        {
            config.enableUnityConsoleLogging = false;
            config.maxFileSizeMB = 1;
            config.maxLogFiles = 100;
            string payload = new string('x', 16384);
            for (int i = 0; i < 80; i++) GameLogger.LogInfo("TEST", marker + "_" + i, payload);
            GameLogger.Flush();
            Assert(firstPath != GameLogger.CurrentLogFilePath, "Test 5.1: Size threshold rotated the active file");
            string content = ReadSessionLogs();
            Assert(content.Contains(marker + "_0 |") && content.Contains(marker + "_79 |"), "Test 5.2: Events survive automatic rotation");
        }
        finally { config.maxFileSizeMB = oldSize; config.maxLogFiles = oldFiles; config.enableUnityConsoleLogging = oldEcho; }
    }

    private static void Test6_RetentionCleanupLogic()
    {
        string testDir = Path.Combine(Path.GetTempPath(), "HighStakesTest_Retention_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDir);
        var logger = GameLogger.EnsureInstance();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var directory = typeof(GameLogger).GetField("resolvedLogDirectory", flags);
        object fileLock = typeof(GameLogger).GetField("fileLock", flags).GetValue(logger);
        int oldLimit = GameLogger.Config.maxLogFiles;
        GameLogger.Flush();
        lock (fileLock)
        {
            string originalDirectory = (string)directory.GetValue(logger);
            try
            {
                File.WriteAllText(Path.Combine(testDir, "user_notes.txt"), "keep me");
                for (int i = 0; i < 15; i++) File.WriteAllText(Path.Combine(testDir, "game_test_" + i + ".log"), "old");
                directory.SetValue(logger, testDir);
                GameLogger.Config.maxLogFiles = 5;
                typeof(GameLogger).GetMethod("EnforceRetentionCleanup", flags).Invoke(logger, null);
                Assert(Directory.GetFiles(testDir, "game_*.log").Length == 5, "Test 6.1: Production retention keeps five historical files");
                Assert(File.Exists(Path.Combine(testDir, "user_notes.txt")), "Test 6.2: Unrelated files survive retention");
            }
            finally
            {
                directory.SetValue(logger, originalDirectory);
                GameLogger.Config.maxLogFiles = oldLimit;
                Directory.Delete(testDir, true);
            }
        }
    }

    private static string ReadSessionLogs()
    {
        var text = new StringBuilder();
        foreach (string path in Directory.GetFiles(GameLogger.LogDirectory, "*" + GameLogger.SessionId + "*.log"))
            text.Append(ReadFileWithRetry(path));
        return text.ToString();
    }

    private static void Test8_QueueCapacityRecovery()
    {
        string marker = "queue_recovery_" + Guid.NewGuid().ToString("N");
        bool echo = GameLogger.Config.enableUnityConsoleLogging;
        GameLogger.Config.enableUnityConsoleLogging = false;
        try
        {
            for (int i = 0; i < 5000; i++)
            {
                GameLogger.LogInfo("TEST", marker + "_" + i, "capacity recovery");
                if (i % 100 == 99) GameLogger.Flush();
            }
            GameLogger.Flush();
            string content = ReadSessionLogs();
            int count = 0;
            for (int i = 0; i < 5000; i++) if (content.Contains(marker + "_" + i + " |")) count++;
            Assert(count == 5000, "Test 8.1: All 5000 events survive repeated drain", "Recorded " + count);
        }
        finally { GameLogger.Config.enableUnityConsoleLogging = echo; }
    }

    private static void Test9_ConcurrentProducers()
    {
        string marker = "parallel_" + Guid.NewGuid().ToString("N");
        bool echo = GameLogger.Config.enableUnityConsoleLogging;
        GameLogger.Config.enableUnityConsoleLogging = false;
        try
        {
            var callback = typeof(GameLogger).GetMethod("OnUnityLogMessageReceived", BindingFlags.Instance | BindingFlags.NonPublic);
            var logger = GameLogger.EnsureInstance();
            Parallel.For(0, 4, worker =>
            {
                for (int i = 0; i < 500; i++)
                {
                    string id = marker + "_" + worker + "_" + i;
                    if (worker % 2 == 0) GameLogger.LogInfo("TEST", id, "parallel producer");
                    else callback.Invoke(logger, new object[] { id + " | callback", "", LogType.Warning });
                    if (i % 100 == 0) GameLogger.Flush();
                }
            });
            GameLogger.Flush();
            string content = ReadSessionLogs();
            int count = 0;
            for (int w = 0; w < 4; w++) for (int i = 0; i < 500; i++)
                if (content.Contains(marker + "_" + w + "_" + i + " |")) count++;
            Assert(count == 2000, "Test 9.1: Concurrent producers and Flush preserve all events", "Recorded " + count);
        }
        finally { GameLogger.Config.enableUnityConsoleLogging = echo; }
    }

    private static void Test10_ShutdownDrainsAndCloses()
    {
        string marker = "shutdown_tail_" + Guid.NewGuid().ToString("N");
        GameLogger.LogInfo("TEST", marker, "must survive shutdown");
        var logger = GameLogger.EnsureInstance();
        var shutdown = typeof(GameLogger).GetMethod("Shutdown", BindingFlags.Instance | BindingFlags.NonPublic);
        shutdown.Invoke(logger, null);
        GameLogger.Flush();
        shutdown.Invoke(logger, null);
        Assert(ReadSessionLogs().Contains(marker + " |"), "Test 10.1: Shutdown drains the last event and is idempotent");
        using (File.Open(GameLogger.CurrentLogFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Assert(true, "Test 10.2: Shutdown releases the physical file handle");
        // Permit another test run from the editor menu without keeping a stopped singleton.
        UnityEngine.Object.DestroyImmediate(logger.gameObject);
    }

    private static void Test7_CompatibilityFacade()
    {
        string uniqueAction = "compat_facade_" + Guid.NewGuid().ToString("N").Substring(0, 6);
        FirebaseGameLogger.LogInfo("COMPAT", uniqueAction, "Message routed through FirebaseGameLogger facade");
        GameLogger.Flush();

        string content = ReadFileWithRetry(GameLogger.CurrentLogFilePath);
        Assert(content.Contains(uniqueAction), "Test 7.1: FirebaseGameLogger call was recorded by GameLogger");
        Assert(content.Contains("[INFO] [COMPAT]"), "Test 7.2: Facade call preserved category and level");
    }

    private static string ReadFileWithRetry(string path)
    {
        for (int i = 0; i < 5; i++)
        {
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (StreamReader sr = new StreamReader(fs, Encoding.UTF8))
                {
                    return sr.ReadToEnd();
                }
            }
            catch (IOException)
            {
                System.Threading.Thread.Sleep(50);
            }
        }
        return string.Empty;
    }
    private static void Test11_QueueAccountingAndConcurrency()
    {
        var logger = GameLogger.EnsureInstance();
        bool echo = GameLogger.Config.enableUnityConsoleLogging;
        string marker = "queue_regression_" + Guid.NewGuid().ToString("N");
        try
        {
            GameLogger.Config.enableUnityConsoleLogging = false;
            // More than twice the capacity over sequential batches must remain admissible.
            for (int batch = 0; batch < 18; batch++)
            {
                for (int i = 0; i < 512; i++) GameLogger.LogInfo("TEST", marker, "sequential");
                GameLogger.Flush();
            }
            Parallel.For(0, 4, worker => {
                for (int i = 0; i < 512; i++) GameLogger.LogInfo("TEST", marker, "concurrent");
            });
            Debug.LogWarning(marker + "_console");
            GameLogger.Flush();
            int queued = (int)typeof(GameLogger).GetField("queueSize", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(logger);
            Assert(queued == 0, "Test 11.1: Both producer paths return queue accounting to zero", queued.ToString());
            int count = 0;
            foreach (string path in Directory.GetFiles(GameLogger.LogDirectory, "game_*.log"))
                foreach (string line in ReadFileWithRetry(path).Split('\n'))
                    if (line.Contains(marker) && line.Contains("[TEST]")) count++;
            Assert(count == 11264, "Test 11.2: All 9,216 sequential and 2,048 concurrent events persisted", count.ToString());
        }
        finally { GameLogger.Config.enableUnityConsoleLogging = echo; }
    }
}
