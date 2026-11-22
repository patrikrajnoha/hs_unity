using System;
using System.IO;
using System.Reflection;
using HighStakes.City;
using UnityEditor;
using UnityEngine;

public static class GameplayAnalyticsTests
{
    [MenuItem("HighStakes/Tests/Run Gameplay Analytics Regression Tests")]
    public static void Run()
    {
        OldQuarterTools.ValidateStory();
        int fallbackErrors = 0;
        Application.LogCallback capture = (message, stack, type) => { if (type == LogType.Error) fallbackErrors++; };
        Application.logMessageReceived += capture;
        string fallback;
        try { fallback = LocalizationManager.GetOrFallback("verification.optional_missing", "Score {value}", "value", "7"); }
        finally { Application.logMessageReceived -= capture; }
        Require(fallback == "Score 7" && fallbackErrors == 0, "Explicit optional fallback must format replacements without Console errors");
        string folder = Path.Combine(Path.GetTempPath(), "HighStakesAnalyticsTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var data = new GameplayAnalyticsData();
            data.ReadDirectory(folder);
            Require(data.Sessions.Length == 0, "Empty directory");
            var format = typeof(GameLogger).GetMethod("FormatJsonlEntry", BindingFlags.NonPublic | BindingFlags.Static);
            string line = (string)format.Invoke(null, new object[] { new GameLogger.LogEntry {
                EventId = "fixture", SessionId = "fixture-session", TimestampUtc = DateTime.UtcNow.ToString("o"),
                Category = "gameplay", Action = "campaign_complete", Message = "control\u0001quote\"slash\\",
                PayloadJson = JsonUtility.ToJson(new GameplayEventData { score = 123, duration = 12, completed = true })
            }});
            File.WriteAllText(Path.Combine(folder, "fixture.jsonl"), line + "\n" + line + "\n{broken\n" +
                "{\"sessionId\":\"older\",\"action\":\"diagnostic\"}\n");
            data.ReadDirectory(folder);
            Require(data.Events.Count == 1 && data.CompletedSessions == 1, "Structured payload roundtrip and duplicate event IDs");
            Require(data.Events[0].data.score == 123 && data.SkippedLines == 1, "Malformed line and older diagnostics");
            var story = JsonUtility.FromJson<StoryFile>(Resources.Load<TextAsset>("City/old_quarter_story").text);
            var progress = new ContractProgress(story);
            progress.Signal(story.contracts[0].steps[0].signal); progress.Tick(10); progress.RecordNavigation(7); progress.RecordDetection();
            progress.Fail(); progress.Signal(story.contracts[0].steps[0].signal); progress.Tick(20);
            for (int i = 1; i < story.contracts[0].steps.Length; i++) progress.Signal(story.contracts[0].steps[i].signal);
            Require(Mathf.Abs(progress.Results[0].efficiency - 2f / 3) < 0.001f && progress.Results[0].navigationSeconds == 7 &&
                progress.Results[0].detections == 1 && progress.Results[0].penalties == 200, "Process measurements preserve score penalties across retries");
            Debug.Log("GAMEPLAY ANALYTICS REGRESSION PASS: empty, malformed, older and duplicate logs; structured JSON; retry efficiency, navigation and detections.");
        }
        finally { Directory.Delete(folder, true); }
    }
    private static void Require(bool value, string name) { if (!value) throw new InvalidOperationException(name); }
}
