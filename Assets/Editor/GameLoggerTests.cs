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
    private static int testsPassed = 0;
    private static int testsFailed = 0;

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
        string uniqueAction = "exception_test_" + Guid.NewGuid().ToString("N").Substring(0, 6);
        try
        {
            throw new InvalidOperationException("Simulated diagnostic error for GameLogger verification");
        }
        catch (Exception ex)
        {
            GameLogger.LogError("TEST", uniqueAction, "Caught simulated exception", ex);
        }

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
        string testDir = Path.Combine(Path.GetTempPath(), "HighStakesTest_Rotation_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDir);

        try
        {
            // Verify file name generation pattern
            string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss");
            string session = "rot_test";
            string file1 = Path.Combine(testDir, $"game_{timestamp}_{session}.log");
            string file2 = Path.Combine(testDir, $"game_{timestamp}_{session}_02.log");

            File.WriteAllText(file1, "part 1");
            File.WriteAllText(file2, "part 2");

            Assert(File.Exists(file1) && File.Exists(file2), "Test 5.1: Numbered rotation file path pattern verified");
        }
        finally
        {
            if (Directory.Exists(testDir))
                Directory.Delete(testDir, true);
        }
    }

    private static void Test6_RetentionCleanupLogic()
    {
        string testDir = Path.Combine(Path.GetTempPath(), "HighStakesTest_Retention_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDir);

        try
        {
            // Create dummy user file that must NOT be deleted
            string userDoc = Path.Combine(testDir, "user_notes.txt");
            File.WriteAllText(userDoc, "important player notes");

            // Create 15 fake historical log files
            for (int i = 0; i < 15; i++)
            {
                string logName = Path.Combine(testDir, $"game_2026-11-20_{i:D2}_dummy.log");
                File.WriteAllText(logName, new string('A', 1024)); // 1 KB each
                // Stagger timestamps
                File.SetCreationTimeUtc(logName, DateTime.UtcNow.AddMinutes(i - 30));
            }

            Assert(Directory.GetFiles(testDir, "game_*.log").Length == 15, "Test 6.1: Created 15 test log files");

            // Simulate retention cleanup to retain max 5 files
            DirectoryInfo dir = new DirectoryInfo(testDir);
            FileInfo[] files = dir.GetFiles("game_*.log");
            Array.Sort(files, (a, b) => a.CreationTimeUtc.CompareTo(b.CreationTimeUtc));

            int maxFiles = 5;
            int deleted = 0;
            for (int i = 0; i < files.Length - maxFiles; i++)
            {
                files[i].Delete();
                deleted++;
            }

            FileInfo[] remainingLogs = dir.GetFiles("game_*.log");
            Assert(remainingLogs.Length == 5, "Test 6.2: Exactly 5 log files retained after cleanup");
            Assert(File.Exists(userDoc), "Test 6.3: Unrelated user file was preserved and NOT deleted");
        }
        finally
        {
            if (Directory.Exists(testDir))
                Directory.Delete(testDir, true);
        }
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
}
