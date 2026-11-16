using System;
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
            EditorApplication.Exit(exit);
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
        var director = UnityEngine.Object.FindAnyObjectByType<CityMissionDirector>();
        var player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
        var hud = UnityEngine.Object.FindAnyObjectByType<CityHud>();
        Require(director != null && player != null && hud != null, "Campaign did not initialise");
        var interiors = UnityEngine.Object.FindObjectsByType<CityInterior>();
        Require(interiors.Length == 4, "Expected four real interiors");
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
                    Vector3 probe = new Vector3(position.x + Mathf.Cos(angle) * 1.5f, 0.3f, position.z + Mathf.Sin(angle) * 1.5f);
                    if (!NavMesh.SamplePosition(probe, out NavMeshHit hit, 0.6f, NavMesh.AllAreas)) continue;
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
        var signal = typeof(CityMissionDirector).GetMethod("Signal", BindingFlags.Instance | BindingFlags.NonPublic);
        Require(signal != null, "Progression bridge missing");
        while (!director.Progress.Finished)
        {
            Contract current = director.Progress.Current;
            foreach (var step in current.steps) signal.Invoke(director, new object[] { step.signal });
            if (!director.Progress.Finished)
            {
                Require(director.IsBetweenLevels && hud.ResultsVisible && Time.timeScale == 0, "Level summary did not pause gameplay");
                director.ContinueCampaign();
                Require(!director.IsBetweenLevels && Time.timeScale == 1, "Continue did not resume gameplay");
            }
        }
        Require(director.HasEnded && hud.ResultsVisible && Time.timeScale == 0 && player.IsDialogueInputLocked, "Final scoreboard did not stop the game");
        int score = director.Progress.Score;
        signal.Invoke(director, new object[] { "finish_campaign" });
        director.ContinueCampaign();
        Require(director.HasEnded && Time.timeScale == 0 && director.Progress.Score == score, "Completed game resumed or paid twice");
        Require(PlayerPrefs.GetInt(CityMissionDirector.BestKey) == score, "Best score did not save");
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
