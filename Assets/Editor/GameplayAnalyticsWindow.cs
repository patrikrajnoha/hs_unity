using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

[Serializable]
public sealed class GameplayLogRecord
{
    public int schemaVersion;
    public string eventId, timestamp, category, action, sessionId, scene, source;
    public GameplayEventData data;
}

// Kept separate from the GUI so malformed files and aggregation can be regression checked.
public sealed class GameplayAnalyticsData
{
    public readonly List<GameplayLogRecord> Events = new List<GameplayLogRecord>();
    public int SkippedLines, UnreadableFiles;
    public void ReadDirectory(string directory)
    {
        Events.Clear(); SkippedLines = UnreadableFiles = 0;
        if (!Directory.Exists(directory)) return;
        var ids = new HashSet<string>();
        string[] files;
        try { files = Directory.GetFiles(directory, "*.jsonl"); }
        catch (IOException) { UnreadableFiles++; return; }
        catch (UnauthorizedAccessException) { UnreadableFiles++; return; }
        foreach (string file in files.OrderBy(p => p, StringComparer.Ordinal))
        {
            try
            {
                using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        try
                        {
                            var record = JsonUtility.FromJson<GameplayLogRecord>(line);
                            if (record == null || string.IsNullOrEmpty(record.sessionId) || string.IsNullOrEmpty(record.action))
                            { SkippedLines++; continue; }
                            // Older diagnostic JSONL remains readable, but cannot supply gameplay metrics.
                            if (record.category != "gameplay" || record.data == null) continue;
                            if (!string.IsNullOrEmpty(record.eventId) && !ids.Add(record.eventId)) continue;
                            if (!Finite(record.data.elapsed) || !Finite(record.data.duration) ||
                                !Finite(record.data.position.x) || !Finite(record.data.position.z))
                            { SkippedLines++; continue; }
                            Events.Add(record);
                        }
                        catch (ArgumentException) { SkippedLines++; }
                    }
                }
            }
            catch (IOException) { UnreadableFiles++; }
            catch (UnauthorizedAccessException) { UnreadableFiles++; }
        }
        // ISO UTC timestamps sort chronologically. Rotated parts need not be enumerated in time order.
        Events.Sort((a, b) => string.CompareOrdinal(a.timestamp, b.timestamp));
    }
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    public string[] Sessions => Events.Select(e => e.sessionId).Distinct().OrderBy(s => s).ToArray();
    public int CompletedSessions => Events.Where(e => e.action == "campaign_complete" ||
        (e.action == "session_end" && e.data.completed)).Select(e => e.sessionId).Distinct().Count();
    public string SessionSource(string id) => Events.Where(e => e.sessionId == id &&
        !string.IsNullOrEmpty(e.source) && e.source.Contains("validation")).Select(e => e.source).FirstOrDefault()
        ?? (Events.Any(e => e.sessionId == id && e.source == "player") ? "player" : "legacy / unknown");
    public float SessionSeconds(string id)
    {
        var events = Events.Where(e => e.sessionId == id).ToArray();
        var end = events.LastOrDefault(e => e.action == "session_end");
        return end != null ? end.data.duration : events.Where(e => e.action == "player_position").Select(e => e.data.elapsed).DefaultIfEmpty(0).Max();
    }
}

public sealed class GameplayAnalyticsWindow : EditorWindow
{
    private readonly GameplayAnalyticsData log = new GameplayAnalyticsData();
    private string directory;
    private string selectedSession;
    private Vector2 scroll;
    private bool showPaths = true;

    [MenuItem("HighStakes/Analytics/Gameplay Sessions")]
    public static void Open()
    {
        var window = GetWindow<GameplayAnalyticsWindow>("Gameplay analytics");
        window.minSize = new Vector2(900, 650);
    }
    private void OnEnable()
    {
        minSize = new Vector2(900, 650);
        directory = string.IsNullOrEmpty(GameLogger.LogDirectory)
            ? Path.Combine(Application.persistentDataPath, "Logs") : GameLogger.LogDirectory;
        Reload();
    }
    private void Reload()
    {
        log.ReadDirectory(directory);
        if (!log.Sessions.Contains(selectedSession)) selectedSession = log.Sessions.FirstOrDefault();
        Repaint();
    }
    private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();
        directory = EditorGUILayout.TextField("Log directory", directory);
        if (GUILayout.Button("Browse", GUILayout.Width(65)))
        {
            string chosen = EditorUtility.OpenFolderPanel("Gameplay logs", directory, "");
            if (!string.IsNullOrEmpty(chosen)) { directory = chosen; Reload(); }
        }
        if (GUILayout.Button("Reload", GUILayout.Width(65))) { GameLogger.Flush(); Reload(); }
        EditorGUILayout.EndHorizontal();
        scroll = EditorGUILayout.BeginScrollView(scroll);
        int total = log.Sessions.Length;
        EditorGUILayout.LabelField("Session overview", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Sessions: {total}   Completed: {log.CompletedSessions}   Completion rate: {(total > 0 ? 100f * log.CompletedSessions / total : 0):0.0}%");
        EditorGUILayout.LabelField($"Average recorded playtime: {(total > 0 ? log.Sessions.Average(log.SessionSeconds) : 0):0.0}s");
        EditorGUILayout.HelpBox("Local recorded executions, including any validation runs. Session labels distinguish player, interaction validation, signal validation and legacy/unknown sources. Ended sessions use wall duration; unfinished sessions use observed active time. Missing events produce partial metrics. Navigation is active time outside interaction range; efficiency is successful-attempt time / all attempt time.", MessageType.Info);
        if (total == 0)
        {
            EditorGUILayout.HelpBox("No structured gameplay sessions in this folder. Play OldQuarter with JSONL enabled, then Reload.", MessageType.Info);
        }
        else
        {
            var sessions = log.Sessions;
            int selection = Mathf.Max(0, Array.IndexOf(sessions, selectedSession));
            selectedSession = sessions[EditorGUILayout.Popup("Movement session", selection, sessions.Select(s => s + " / " + log.SessionSource(s)).ToArray())];
            showPaths = EditorGUILayout.Toggle("Show movement map", showPaths);
            if (showPaths) DrawMap();
            EditorGUILayout.LabelField("Mission performance — all recorded sessions", EditorStyles.boldLabel);
            foreach (var mission in log.Events.Where(e => !string.IsNullOrEmpty(e.data.missionId)).GroupBy(e => e.data.missionId))
            {
                var complete = mission.Where(e => e.action == "mission_complete").ToArray();
                int failures = mission.Count(e => e.action == "mission_failed");
                int retries = mission.Count(e => e.action == "mission_start" && e.data.attempt > 1);
                int attempts = mission.Count(e => e.action == "mission_start");
                EditorGUILayout.LabelField(mission.Key, $"{complete.Length} clears / {failures} failures / {retries} retries");
                EditorGUILayout.LabelField("   Failure frequency", attempts > 0 ? $"{100f * failures / attempts:0.0}% of recorded starts" : "Missing mission starts");
                if (complete.Length > 0)
                {
                    EditorGUILayout.LabelField("   Mean", $"{complete.Average(e => e.data.duration):0.0}s / {complete.Average(e => e.data.score):0} pts / {complete.Average(e => e.data.navigationSeconds):0.0}s navigation / {complete.Average(e => e.data.detections):0.0} detections / {100 * complete.Average(e => e.data.efficiency):0}% efficiency");
                    EditorGUILayout.LabelField("   Recorded clears", string.Join(" → ", complete.Select(e => e.sessionId + ": " + e.data.score + " pts, " + e.data.duration.ToString("0.0") + "s")));
                }
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Objective analysis — failures first", EditorStyles.boldLabel);
            foreach (var step in log.Events.Where(e => !string.IsNullOrEmpty(e.data.objectiveId) &&
                (e.action == "objective_complete" || e.action == "mission_failed" || (e.action == "choice_selected" && e.data.signal == "mistake"))).GroupBy(e => e.data.missionId + " / " + e.data.objectiveId)
                .OrderByDescending(g => g.Count(e => e.action == "mission_failed" || e.data.signal == "mistake")))
            {
                var clears = step.Where(e => e.action == "objective_complete").ToArray();
                EditorGUILayout.LabelField(step.Key, $"{(clears.Length > 0 ? clears.Average(e => e.data.objectiveSeconds).ToString("0.0") : "—")}s mean / {step.Count(e => e.action == "mission_failed")} failures / {step.Count(e => e.data.signal == "mistake")} mistakes");
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Decisions and measured outcomes", EditorStyles.boldLabel);
            foreach (var choice in log.Events.Where(e => e.action == "choice_selected").GroupBy(e => e.data.dialogueId + " / choice " + e.data.choice + " / " + e.data.signal))
                EditorGUILayout.LabelField(choice.Key, choice.Count().ToString());
            foreach (var strategy in log.Events.Where(e => e.action == "mission_complete" && !string.IsNullOrEmpty(e.data.strategy)).GroupBy(e => e.data.strategy))
                EditorGUILayout.LabelField(strategy.Key, $"{strategy.Count()} clears / {strategy.Average(e => e.data.score):0} pts / {strategy.Average(e => e.data.detections):0.0} detections / {strategy.Average(e => e.data.duration):0.0}s");
        }
        EditorGUILayout.LabelField($"Skipped malformed lines: {log.SkippedLines}; unreadable files: {log.UnreadableFiles}");
        EditorGUILayout.EndScrollView();
    }
    private void DrawMap()
    {
        EditorGUILayout.LabelField("Old Quarter — north up, world X/Z, ±45 m", EditorStyles.boldLabel);
        Rect area = GUILayoutUtility.GetRect(300, 440, GUILayout.ExpandWidth(true));
        float side = Mathf.Min(area.width, area.height);
        var map = new Rect(area.x + (area.width - side) / 2, area.y, side, side);
        EditorGUI.DrawRect(map, new Color(0.12f, 0.16f, 0.18f));
        // The exact building footprints from OldQuarterBootstrap.BuildDistrict (metres).
        foreach (int x in new[] { -1, 1 }) foreach (int z in new[] { -1, 1 })
        {
            Footprint(map, x * 17, z * 19, 10, 12);
            Footprint(map, x * 32, z * 19, 10, 12);
            Footprint(map, x * 17, z * 34, 10, 10);
            Footprint(map, x * 32, z * 34, 10, 10);
        }
        Handles.BeginGUI();
        GameplayLogRecord previous = null;
        foreach (var entry in log.Events.Where(e => e.sessionId == selectedSession && e.data.hasPosition))
        {
            Vector2 point = MapPoint(map, entry.data.position);
            if (entry.action == "player_position")
            {
                if (previous != null && entry.data.elapsed - previous.data.elapsed <= 2.5f &&
                    Vector3.Distance(entry.data.position, previous.data.position) < 15f)
                {
                    Handles.color = Color.cyan;
                    Handles.DrawLine(MapPoint(map, previous.data.position), point);
                }
                previous = entry;
            }
            if (entry.action == "mission_failed" || entry.action == "police_alert")
            {
                Handles.color = entry.action == "mission_failed" ? Color.red : Color.yellow;
                Handles.DrawSolidDisc(point, Vector3.forward, 4);
                GUI.Label(new Rect(point.x + 5, point.y - 8, 140, 20), entry.data.reason ?? entry.action);
                if (entry.action == "mission_failed") previous = null;
            }
        }
        Handles.EndGUI();
        EditorGUILayout.LabelField("Cyan: sampled path   Red: failure   Yellow: police detection");
    }
    private static Vector2 MapPoint(Rect map, Vector3 p) => new Vector2(map.x + (p.x + 45) / 90 * map.width, map.y + (45 - p.z) / 90 * map.height);
    private static void Footprint(Rect map, float x, float z, float width, float depth)
    {
        Vector2 corner = MapPoint(map, new Vector3(x - width / 2, 0, z + depth / 2));
        EditorGUI.DrawRect(new Rect(corner.x, corner.y, width / 90 * map.width, depth / 90 * map.height), new Color(0.35f, 0.39f, 0.36f));
    }
}
