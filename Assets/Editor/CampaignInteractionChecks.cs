using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HighStakes.City;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Interaction coverage, distinct from CampaignRuntimeChecks' signal simulation.
// Teleports only arrange reachable approaches. Progress is driven by keyboard input,
// the normal interaction/dialogue code, real hold timers, and the results UI buttons.
public static class CampaignInteractionChecks
{
    public static string Status { get; private set; } = "Not run";
    private static CityMissionDirector director;
    private static PlayerController player;
    private static bool failed;
    private static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    [MenuItem("HighStakes/Tests/Run Interactions In Fresh Play Session")]
    public static void Start()
    {
        director = UnityEngine.Object.FindAnyObjectByType<CityMissionDirector>();
        player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
        Require(Application.isPlaying && director != null && director.Progress.ContractIndex == 0 && !director.Progress.Active,
            "Start a fresh OldQuarter campaign in Play Mode before running interaction checks.");
        failed = false; Status = "Running";
        GameLogger.GameplaySource = "interaction_validation";
        GameLogger.Track("validation_run", new GameplayEventData { reason = "interaction_validation" });
        director.StartCoroutine(Guard(Run(), true));
    }
    private static IEnumerator Guard(IEnumerator routine, bool root = false)
    {
        while (!failed)
        {
            bool next = false; object value = null;
            try { next = routine.MoveNext(); if (next) value = routine.Current; }
            catch (Exception error) { failed = true; Status = "FAILED: " + error.Message; Debug.LogException(error); }
            if (!next || failed) break;
            if (value is IEnumerator nested) yield return Guard(nested);
            else yield return value;
        }
        if (root)
        {
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            if (!failed) { Status = "PASS"; Debug.Log("CAMPAIGN INTERACTION PASS: movement, look, E interactions, dialogue puzzle/mistake, both ledger routes, five missions, actual police catch/retry, hold timers, results buttons, Free Roam NPCs and inventory."); }
        }
    }
    private static IEnumerator Run()
    {
        yield return new WaitForSecondsRealtime(1);
        Vector3 before = player.transform.position;
        yield return Tap(Key.W, 0.4f);
        Require(Vector3.Distance(before, player.transform.position) > 0.4f, "Keyboard movement did not move the player");
        Quaternion look = player.playerCamera.rotation;
        InputSystem.QueueDeltaStateEvent(Mouse.current.delta, new Vector2(150, 40));
        yield return new WaitForSecondsRealtime(0.3f);
        Require(Quaternion.Angle(look, player.playerCamera.rotation) > 0.1f, "Mouse look did not rotate the camera");

        yield return Talk("mara", 0, 0);
        yield return Use("parcel");
        yield return Door("Cafe");
        yield return Talk("tomas", 0);
        yield return Talk("mara", 0);
        yield return Continue(1);

        yield return Talk("mara", 0);
        yield return Door("Workshop");
        yield return Use("fuse");
        yield return Talk("wiring", 0);
        yield return Talk("switchboard", 1, 0, 1, 0);
        Require(director.Progress.CurrentMistakes == 1, "Wrong switch did not record exactly one mistake");
        yield return Talk("mara", 0);
        yield return Continue(2);

        yield return Talk("mara", 0, 0);
        yield return Door("Warehouse");
        yield return Talk("ledger", 0);
        Require(director.IsConversationActive && director.Progress.Step.signal == "collect_ledger", "Unknown code was accepted");
        yield return Tap(Key.Digit2);
        Require(director.Progress.AlarmRaised && director.Progress.Strategy == "forced_locker", "Forced locker did not raise an alarm");
        yield return new WaitForSecondsRealtime(0.5f);
        var ledgerOfficer = UnityEngine.Object.FindAnyObjectByType<PoliceBehavior>();
        Place(ledgerOfficer.transform.position + ledgerOfficer.transform.forward * 0.9f);
        yield return new WaitForSecondsRealtime(0.8f);
        Require(director.Progress.CurrentFailures == 1 && !director.Progress.Active, "Risky ledger route did not allow a police catch/retry");
        yield return Talk("mara", 0, 0);
        yield return Talk("ada", 0);
        Require(director.Progress.KnowsRoute, "Ada code was not learned");
        yield return Door("Warehouse");
        yield return Talk("ledger", 0);
        Require(director.Progress.Strategy == "locker_code" && !ledgerOfficer.isChasing, "Code route started a new pursuit");
        yield return Hold("safehouse", 5.5f);
        yield return Talk("mara", 0);
        yield return Continue(3);

        yield return Talk("mara", 0);
        yield return Talk("manifest", 0);
        yield return Talk("invoice", 0);
        yield return Talk("ada", 1);
        yield return Talk("mara", 0);
        yield return Continue(4);

        yield return FinalBroadcast();
        var officer = UnityEngine.Object.FindAnyObjectByType<PoliceBehavior>();
        yield return new WaitForSecondsRealtime(0.5f);
        Require(officer.isChasing, "Broadcast did not start actual police pursuit");
        Place(officer.transform.position + officer.transform.forward * 0.9f);
        yield return new WaitForSecondsRealtime(0.8f);
        Require(director.Progress.CurrentFailures == 1 && !director.Progress.Active, "Physical police catch did not restart the mission");
        yield return FinalBroadcast();
        // Hide across solid buildings and let the officer's real search timer expire.
        Approach(Actor("safehouse"), true);
        float deadline = Time.realtimeSinceStartup + 40;
        while ((officer.isChasing || officer.isCalled) && Time.realtimeSinceStartup < deadline) yield return new WaitForSecondsRealtime(0.25f);
        Require(!officer.isChasing && !officer.isCalled, "Police search did not end after hiding inside the workshop");
        yield return Hold("extraction", 6.5f);
        Require(director.HasEnded && director.Progress.Results.Count == 5, "Extraction did not finish after real pursuit/search");
        int score = director.Progress.Score;
        string freeRoamLabel = LocalizationManager.GetOrFallback("ui.city.continue_free_roam", "CONTINUE FREE ROAM");
        var freeButton = UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>().First(b => b.gameObject.activeInHierarchy &&
            b.transform.parent.name == "Score card" && b.gameObject.name == freeRoamLabel);
        freeButton.onClick.Invoke();
        yield return new WaitForSecondsRealtime(0.3f);
        Require(!director.HasEnded && Time.timeScale == 1 && !player.IsDialogueInputLocked, "Free Roam results button did not resume controls");
        yield return Talk("mara", 0);
        yield return Talk("ada", 0);
        yield return Talk("tomas", 0);
        Require(director.Progress.Score == score && director.Progress.Finished, "Free Roam changed campaign rewards");
        Require(player.GetComponent<Inventory>() != null && player.GetComponent<PlantSpawner>() != null &&
            player.GetComponent<PackingSys>() != null && player.GetComponent<ProductMixingSystem>() != null,
            "Free Roam lost inventory or production components");
        GameLogger.Flush();
    }
    private static IEnumerator FinalBroadcast()
    {
        yield return Talk("mara", 0);
        yield return Talk("ada", 0);
        yield return Door("Radio");
        yield return Talk("radio", 1);
    }
    private static IEnumerator Continue(int completed)
    {
        Require(director.Progress.Results.Count == completed && director.IsBetweenLevels && Time.timeScale == 0,
            "Missing level results " + completed);
        yield return Tap(Key.Enter);
        Require(!director.IsBetweenLevels && Time.timeScale == 1, "Enter did not continue from level results");
    }
    private static IEnumerator Use(string id)
    {
        Approach(Actor(id)); yield return new WaitForSecondsRealtime(0.15f);
        yield return Tap(Key.E);
    }
    private static IEnumerator Talk(string id, params int[] choices)
    {
        yield return Use(id);
        foreach (int index in choices)
        {
            Require(director.IsConversationActive, "Expected dialogue at " + id + " choice " + index);
            yield return Tap(new[] { Key.Digit1, Key.Digit2, Key.Digit3 }[index]);
        }
    }
    private static IEnumerator Door(string room)
    {
        var door = UnityEngine.Object.FindObjectsByType<CityDoor>().First(d => d.roomName.Contains(room));
        if (!door.IsOpen) yield return Use(door.GetComponent<CityActor>().id);
    }
    private static IEnumerator Hold(string id, float seconds)
    {
        Approach(Actor(id), true); yield return new WaitForSecondsRealtime(seconds);
        Require(director.Progress.Step?.target != id, "Hold objective did not complete at " + id);
    }
    private static CityActor Actor(string id) => UnityEngine.Object.FindObjectsByType<CityActor>().First(a => a.id == id);
    private static void Approach(CityActor actor, bool hold = false)
    {
        var nearest = typeof(CityMissionDirector).GetMethod("NearestInteractable", Private);
        for (float radius = 0.7f; radius <= (hold ? 1.4f : 2.5f); radius += 0.3f)
            for (int i = 0; i < 24; i++)
            {
                float angle = i * Mathf.PI / 12;
                var probe = actor.transform.position + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                probe.y = 0.3f;
                if (!NavMesh.SamplePosition(probe, out NavMeshHit hit, 0.3f, NavMesh.AllAreas)) continue;
                if (Vector3.Distance(hit.position, actor.transform.position) > (hold ? 1.6f : 2.6f)) continue;
                Place(hit.position + Vector3.up * 0.04f);
                if (hold || (CityActor)nearest.Invoke(director, null) == actor) return;
            }
        throw new InvalidOperationException("No visible, unobstructed interaction approach for " + actor.id);
    }
    private static void Place(Vector3 point)
    {
        var controller = player.GetComponent<CharacterController>();
        controller.enabled = false; player.transform.position = point; controller.enabled = true;
        player.ResetMotion(); Physics.SyncTransforms();
    }
    private static IEnumerator Tap(Key key, float seconds = 0.12f)
    {
        // Floating Editor tools can take Game view focus during a domain reload.
        // Respect the project's normal keyboard routing rather than changing input settings.
        EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
        yield return null;
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(key));
        yield return new WaitForSecondsRealtime(seconds);
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
        yield return new WaitForSecondsRealtime(0.15f);
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
