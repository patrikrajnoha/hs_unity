using System;
using System.Collections.Generic;
using System.IO;
using HighStakes.City;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public static class OldQuarterTools
{
    [MenuItem("HighStakes/Old Quarter/Open Official District")]
    public static void OpenDistrict()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene("Assets/Scenes/OldQuarter.unity");
    }

    [MenuItem("HighStakes/Old Quarter/Validate Story And Progression")]
    public static void ValidateStory()
    {
        var asset = Resources.Load<TextAsset>("City/old_quarter_story");
        Require(asset != null, "Story resource is missing");
        var story = JsonUtility.FromJson<StoryFile>(asset.text);
        Require(story.contracts.Length == 5, "Expected five campaign levels");
        var nodes = new HashSet<string>();
        var actors = new HashSet<string> { "mara", "tomas", "ada", "parcel", "ledger", "safehouse", "officer",
            "fuse", "wiring", "switchboard", "manifest", "invoice", "radio", "extraction" };
        var signals = new HashSet<string>();
        foreach (var node in story.conversations)
        {
            Require(nodes.Add(node.id), "Duplicate conversation " + node.id);
            Require(!string.IsNullOrWhiteSpace(node.text), "Empty dialogue " + node.id);
            Require(node.choices != null && node.choices.Length > 0 && node.choices.Length <= 3, "Invalid choices " + node.id);
        }
        foreach (var node in story.conversations)
            foreach (var choice in node.choices)
                Require(string.IsNullOrEmpty(choice.next) || nodes.Contains(choice.next), "Broken dialogue link " + choice.next);
        foreach (var contract in story.contracts)
            foreach (var step in contract.steps)
            {
                Require(actors.Contains(step.target), "Unknown target " + step.target);
                Require(signals.Add(step.signal), "Duplicate progression signal " + step.signal);
                Require(string.IsNullOrEmpty(step.dialogue) || nodes.Contains(step.dialogue), "Unknown dialogue " + step.dialogue);
            }

        var progress = new ContractProgress(story);
        Require(!progress.Signal("finish_delivery") && progress.Score == 0, "Out-of-order completion accepted");
        Require(!progress.Tick(1000), "An unaccepted job must not time out");
        Require(progress.Signal("accept_delivery"), "First level cannot start");
        progress.Tick(10);
        for (int i = 1; i < story.contracts[0].steps.Length; i++)
            Require(progress.Signal(story.contracts[0].steps[i].signal), "Delivery cannot advance");
        int firstScore = story.contracts[0].baseScore + (int)(story.contracts[0].timeLimit - 10) * 2 + 300;
        Require(progress.Score == firstScore && progress.Money == 150, "First score calculation incorrect");
        Require(!progress.Signal("finish_delivery") && progress.Score == firstScore, "Duplicate payout accepted");
        Require(progress.Signal("accept_power"), "Power-cut level did not unlock");
        Require(progress.Tick(story.contracts[1].timeLimit + 1), "Expired level did not report timeout");
        progress.Fail();
        Require(progress.CurrentFailures == 1 && progress.StepIndex == 0 && progress.Score == firstScore, "Failure lost results or counted twice");
        progress.RecordMistake();
        Require(progress.Signal("accept_power"), "Retry cannot start");
        progress.Tick(20);
        for (int i = 1; i < story.contracts[1].steps.Length; i++)
            Require(progress.Signal(story.contracts[1].steps[i].signal), "Power-cut level cannot advance");
        Require(progress.Results[1].penalties == 275, "Retry/mistake penalties incorrect");
        Require(progress.Results[1].timeBonus == (int)(story.contracts[1].timeLimit - 20) * 2, "Time bonus incorrect");
        var restored = new ContractProgress(story, progress.Capture());
        Require(restored.ContractIndex == 2 && restored.Score == progress.Score && restored.Failures == 1, "Checkpoint restore incorrect");
        restored.RaiseAlarm(); restored.Fail();
        restored = new ContractProgress(story, restored.Capture());
        Require(restored.AlarmRaised, "Alarm penalty lost across checkpoint restore");
        while (!restored.Finished)
        {
            Contract current = restored.Current;
            foreach (var step in current.steps) Require(restored.Signal(step.signal), "Campaign cannot advance: " + step.signal);
        }
        Require(restored.Results.Count == 5 && restored.Results[2].stealthBonus == 0, "Campaign completion or stealth score incorrect");
        int finalScore = restored.Score;
        Require(!restored.Signal("finish_campaign") && !restored.Tick(100), "Completed campaign accepted more progress");
        restored.Fail(); restored.RecordMistake();
        Require(restored.Score == finalScore && restored.CurrentFailures == 0, "Finished score changed");
        var completedSave = new ContractProgress(story, restored.Capture());
        Require(completedSave.Finished && completedSave.Score == finalScore, "Completed checkpoint did not restore");
        Require(new ContractProgress(story, new CampaignSave { version = 1 }).ContractIndex == 0, "Incompatible save accepted");
        Debug.Log("CAMPAIGN MODEL PASS: 5 levels, score, time/stealth bonuses, penalties, duplicate rewards, retry, checkpoint and terminal-state guards.");
    }

    [MenuItem("HighStakes/NPC/Apply Selected Humanoid Asset To Scene NPCs")]
    public static void ApplySelectedHumanoid()
    {
        GameObject asset = Selection.activeObject as GameObject;
        Require(asset != null && AssetDatabase.Contains(asset), "Select an imported humanoid FBX or prefab in the Project panel first.");
        Animator source = asset.GetComponentInChildren<Animator>(true);
        Require(source != null && source.avatar != null && source.avatar.isValid && source.avatar.isHuman,
            "The selected asset must have a valid Humanoid avatar. Set Rig > Animation Type to Humanoid first.");
        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/NPC/NPC_Locomotion.controller");
        Require(controller != null && controller.animationClips.Length >= 3, "Restore the LFS animation clips before applying a humanoid pack.");
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Replace NPC humanoid visuals");
        int count = 0;
        foreach (var agent in UnityEngine.Object.FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None))
        {
            if (agent.GetComponent<PlayerController>() != null) continue;
            foreach (Renderer renderer in agent.GetComponentsInChildren<Renderer>(true))
            {
                Undo.RecordObject(renderer, "Hide previous NPC visual");
                renderer.enabled = false;
            }
            Transform previous = agent.transform.Find("ImportedNpcVisual");
            if (previous != null) Undo.DestroyObjectImmediate(previous.gameObject);
            var visual = PrefabUtility.InstantiatePrefab(asset, agent.transform) as GameObject;
            Require(visual != null, "Could not instantiate selected asset");
            Undo.RegisterCreatedObjectUndo(visual, "Install NPC visual");
            visual.name = "ImportedNpcVisual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            foreach (var behaviour in visual.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
            foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var body in visual.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic = true; body.detectCollisions = false; }
            foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true)) renderer.enabled = true;
            var animator = visual.GetComponentInChildren<Animator>(true);
            animator.enabled = true; animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
            var driver = animator.GetComponent<NPCVisualAnimatorDriver>();
            if (driver == null) driver = Undo.AddComponent<NPCVisualAnimatorDriver>(animator.gameObject);
            driver.enabled = true;
            var grounding = visual.GetComponent<NPCVisualGrounding>();
            if (grounding == null) grounding = Undo.AddComponent<NPCVisualGrounding>(visual);
            grounding.enabled = true;
            count++;
        }
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"Applied humanoid visual to {count} NPCs. Scene is unsaved; review it and save when ready.");
    }

    [MenuItem("HighStakes/Old Quarter/Report Missing LFS Assets")]
    public static void ReportMissingAssets()
    {
        int count = 0;
        foreach (string path in Directory.EnumerateFiles("Assets", "*", SearchOption.AllDirectories))
        {
            var info = new FileInfo(path);
            if (info.Length > 512 || info.Length == 0) continue;
            using (var stream = new StreamReader(path))
                if (stream.ReadLine() == "version https://git-lfs.github.com/spec/v1") count++;
        }
        Debug.Log($"Unresolved LFS pointers: {count}. Restore with an authenticated Git clone and git lfs pull. OldQuarter uses original procedural geometry and does not need those models.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Old Quarter validation: " + message);
    }
}
