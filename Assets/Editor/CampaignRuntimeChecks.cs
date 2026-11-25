using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using HighStakes.City;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

// Batch CI entry point; deliberately does not require the missing original FBX assets.
[InitializeOnLoad]
public static class CampaignRuntimeChecks
{
    private const string Key = "HighStakes.Campaign.RuntimeCheck.";
    static CampaignRuntimeChecks() { if (SessionState.GetBool(Key + "active", false)) Hook(); }
    private static void Hook()
    {
        EditorApplication.playModeStateChanged -= StateChanged;
        EditorApplication.playModeStateChanged += StateChanged;
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
        Application.logMessageReceived -= Log;
        Application.logMessageReceived += Log;
    }
    public static void Start()
    {
        SessionState.SetBool(Key + "exitEditor", true);
        Begin();
    }
    [MenuItem("HighStakes/Tests/Run Campaign Runtime Checks")]
    public static void StartInteractive()
    {
        SessionState.SetBool(Key + "exitEditor", false);
        Begin();
    }
    private static void Begin()
    {
        OldQuarterTools.ValidateStory();
        SessionState.SetBool(Key + "hadSave", PlayerPrefs.HasKey(CityMissionDirector.SaveKey));
        SessionState.SetString(Key + "save", PlayerPrefs.GetString(CityMissionDirector.SaveKey));
        SessionState.SetBool(Key + "hadBest", PlayerPrefs.HasKey(CityMissionDirector.BestKey));
        SessionState.SetInt(Key + "best", PlayerPrefs.GetInt(CityMissionDirector.BestKey));
        PlayerPrefs.DeleteKey(CityMissionDirector.SaveKey); PlayerPrefs.DeleteKey(CityMissionDirector.BestKey); PlayerPrefs.Save();
        SessionState.SetBool(Key + "active", true); SessionState.SetBool(Key + "tested", false);
        SessionState.SetInt(Key + "errors", 0); SessionState.SetInt(Key + "exit", 1);
        SessionState.SetFloat(Key + "start", (float)EditorApplication.timeSinceStartup);
        Hook();
        EditorSceneManager.OpenScene("Assets/Scenes/OldQuarter.unity");
        EditorApplication.isPlaying = true;
    }
    private static void StateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key + "active", false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
            SessionState.SetFloat(Key + "start", (float)EditorApplication.timeSinceStartup);
        if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Key + "tested", false))
        {
            if (SessionState.GetBool(Key + "hadSave", false)) PlayerPrefs.SetString(CityMissionDirector.SaveKey, SessionState.GetString(Key + "save", ""));
            else PlayerPrefs.DeleteKey(CityMissionDirector.SaveKey);
            if (SessionState.GetBool(Key + "hadBest", false)) PlayerPrefs.SetInt(CityMissionDirector.BestKey, SessionState.GetInt(Key + "best", 0));
            else PlayerPrefs.DeleteKey(CityMissionDirector.BestKey);
            PlayerPrefs.Save();
            int exit = SessionState.GetInt(Key + "exit", 1);
            SessionState.SetBool(Key + "active", false);
            Debug.Log(exit == 0 ? "CAMPAIGN RUNTIME PASS: interiors, reachable objectives, doors, five-level UI flow and frozen final scoreboard." : "CAMPAIGN RUNTIME FAILED.");
            if (SessionState.GetBool(Key + "exitEditor", true)) EditorApplication.Exit(exit);
        }
    }
    private static void Log(string message, string stack, LogType type)
    {
        if (SessionState.GetBool(Key + "active", false) && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
            SessionState.SetInt(Key + "errors", SessionState.GetInt(Key + "errors", 0) + 1);
    }
    private static void Update()
    {
        if (!SessionState.GetBool(Key + "active", false) || !EditorApplication.isPlaying || SessionState.GetBool(Key + "tested", false)) return;
        if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "start", (float)EditorApplication.timeSinceStartup) < 8) return;
        SessionState.SetBool(Key + "tested", true);
        try
        {
            CheckRuntime();
            Require(SessionState.GetInt(Key + "errors", 0) == 0, "Runtime logged errors before completion");
            SessionState.SetInt(Key + "exit", 0);
        }
        catch (Exception exception) { Debug.LogException(exception); SessionState.SetInt(Key + "exit", 1); }
        EditorApplication.isPlaying = false;
    }
    private static void CheckRuntime()
    {
        AudioRuntimeChecks.Check();
        GameLogger.GameplaySource = "signal_validation";
        GameLogger.Track("validation_run", new GameplayEventData { reason = "signal_validation" });
        var director = UnityEngine.Object.FindAnyObjectByType<CityMissionDirector>();
        var player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
        var hud = UnityEngine.Object.FindAnyObjectByType<CityHud>();
        Require(director != null && player != null && hud != null, "Campaign did not initialise");
        Require(director.HasPriorityInteraction(player.gameObject), "Story interaction did not take priority over nearby pickups");
        var interiors = UnityEngine.Object.FindObjectsByType<CityInterior>();
        Require(interiors.Length == 5, "Expected four mission interiors and a separate production warehouse");
        var farm = UnityEngine.Object.FindAnyObjectByType<SafehouseFarmingSystem>();
        Require(farm != null && farm.GetComponent<CityInterior>().roomName == "Warehouse / Production", "Production is not inside its dedicated warehouse");
        var productionDoor = Array.Find(UnityEngine.Object.FindObjectsByType<CityDoor>(), d => d.roomName == "Warehouse / Production");
        Require(productionDoor != null && productionDoor.minimumLevel == 1, "Production warehouse is locked behind a mission");
        Require(farm.PackagingStation != null && farm.MixingStation == null && farm.PlantPrefab != null,
            "Production warehouse lacks its farming/packing equipment");
        Require(NavMesh.CalculateTriangulation().indices.Length > 0, "Runtime navigation was not baked");
        foreach (var room in interiors)
        {
            Require(NavMesh.SamplePosition(room.transform.position + Vector3.up * 0.3f, out NavMeshHit sample, 1f, NavMesh.AllAreas), "No floor navigation in " + room.roomName);
            var path = new NavMeshPath();
            Require(NavMesh.CalculatePath(player.transform.position, sample.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete,
                "Interior unreachable: " + room.roomName);
        }
        var actors = new Dictionary<string, CityActor>();
        foreach (var actor in UnityEngine.Object.FindObjectsByType<CityActor>()) actors[actor.id] = actor;
        foreach (var level in director.Progress.Story.contracts)
            foreach (var step in level.steps)
            {
                Require(actors.ContainsKey(step.target), "Missing target " + step.target);
                Vector3 position = actors[step.target].transform.position;
                bool reachable = false;
                for (int i = 0; i < 12 && !reachable; i++)
                {
                    float angle = i * Mathf.PI / 6;
                    // Match the real interaction range; furniture can require standing >1.5m away.
                    float radius = step.holdSeconds > 0 ? 1.1f : 2.1f;
                    Vector3 probe = new Vector3(position.x + Mathf.Cos(angle) * radius, 0.3f, position.z + Mathf.Sin(angle) * radius);
                    if (!NavMesh.SamplePosition(probe, out NavMeshHit hit, 0.6f, NavMesh.AllAreas)) continue;
                    if (Vector3.Distance(position, hit.position) >= (step.holdSeconds > 0 ? 1.8f : 2.7f)) continue;
                    var path = new NavMeshPath();
                    reachable = NavMesh.CalculatePath(player.transform.position, hit.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
                }
                Require(reachable, "No reachable interaction approach for " + step.target);
            }
        foreach (var door in UnityEngine.Object.FindObjectsByType<CityDoor>())
        {
            Require(door.minimumLevel == 1 || !door.Toggle(1), "A locked room opened before its level");
            Require(door.Toggle(5), "Door did not unlock");
        }
        foreach (var contract in director.Progress.Story.contracts)
            foreach (var step in contract.steps)
                Require(!farm.GetComponent<CityInterior>().Contains(actors[step.target].transform.position), "A mission target was placed in the production warehouse");
        Require(Vector3.Distance(actors["ledger"].transform.position, new Vector3(35, 1.375f, -22)) < .01f &&
                Vector3.Distance(actors["manifest"].transform.position, new Vector3(29, 1.19f, -20)) < .01f,
            "Original archive mission positions changed");
        WarehouseInventoryChecks.Check();
        WarehouseInventoryChecks.CheckSimplifiedProduction();
        Debug.Log("WAREHOUSE LAYOUT PASS: separate unlocked production, original mission positions and no overlapping targets.");
        var signal = typeof(CityMissionDirector).GetMethod("Signal", BindingFlags.Instance | BindingFlags.NonPublic);
        Require(signal != null, "Progression bridge missing");
        while (!director.Progress.Finished)
        {
            Contract current = director.Progress.Current;
            foreach (var step in current.steps) signal.Invoke(director, new object[] { step.signal });
            if (!director.Progress.Finished)
            {
                Require(director.IsBetweenLevels && hud.ResultsVisible && Time.timeScale == 0, "Level summary did not pause gameplay");
                string nextLabel = LocalizationManager.GetOrFallback("ui.city.next_chapter_enter", "NEXT CHAPTER  [ENTER]");
                var nextButton = Array.Find(hud.GetComponentsInChildren<UnityEngine.UI.Button>(), button => button.name == nextLabel);
                Require(nextButton != null, "Level summary is missing the Next Chapter button");
                nextButton.onClick.Invoke();
                Require(!director.IsBetweenLevels && Time.timeScale == 1, "Continue did not resume gameplay");
            }
        }
        Require(director.HasEnded && hud.ResultsVisible && Time.timeScale == 0 && player.IsDialogueInputLocked, "Final scoreboard did not stop the game");
        int score = director.Progress.Score;
        signal.Invoke(director, new object[] { "finish_campaign" });
        director.ContinueCampaign();
        Require(director.HasEnded && Time.timeScale == 0 && director.Progress.Score == score, "Completed game resumed or paid twice");
        Require(PlayerPrefs.GetInt(CityMissionDirector.BestKey) == score, "Best score did not save");
        GameLogger.Flush();
        Require(File.Exists(GameLogger.CurrentLogFilePath), "Gameplay log file does not exist");
        string content = "";
        foreach (string path in Directory.GetFiles(GameLogger.LogDirectory, "*" + GameLogger.SessionId + "*.log"))
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream)) content += reader.ReadToEnd();
        Require(content.Contains("campaign_started |") && content.Contains("objective_signaled |") &&
                content.Contains("campaign_finished |"), "Campaign events were not physically written to disk");
        Debug.Log("CAMPAIGN FILE LOG PASS: start, objective and completion events found on disk.");
        director.ContinueFreeRoam();
        Require(!director.HasEnded && !hud.ResultsVisible && Time.timeScale == 1 && !player.IsDialogueInputLocked,
            "Continue Free Roam did not release controls");
        // Exercise the exact null-Current HUD path that used to throw on the next frame.
        hud.Refresh(director.Progress, CityPoliceStatus.Clear);
        typeof(CityMissionDirector).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(director, null);
        signal.Invoke(director, new object[] { "finish_campaign" });
        typeof(CityMissionDirector).GetMethod("Fail", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(director, new object[] { "probe", "regression" });
        Require(director.Progress.Score == score && director.Progress.Results.Count == 5, "Free Roam duplicated rewards or restarted a mission");
        var pause = UnityEngine.Object.FindAnyObjectByType<PauseMenuController>();
        pause.TogglePause(); Require(Time.timeScale == 0, "Free Roam pause did not stop time");
        pause.ResumeGame(); Require(Time.timeScale == 1, "Free Roam pause did not resume time");
        var restored = new ContractProgress(director.Progress.Story, JsonUtility.FromJson<CampaignSave>(PlayerPrefs.GetString(CityMissionDirector.SaveKey)));
        Require(restored.Finished && restored.Current == null && restored.Score == score, "Completed campaign persistence failed");
        Debug.Log("FREE ROAM REGRESSION PASS: HUD, controls, pause, persistence, duplicate payouts and failure guards.");
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
