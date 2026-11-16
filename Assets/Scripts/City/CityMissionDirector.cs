using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace HighStakes.City
{
    public sealed class CityMissionDirector : MonoBehaviour
    {
        public const string SaveKey = "HighStakes.OldQuarter.Campaign.v2";
        public const string BestKey = "HighStakes.OldQuarter.BestScore.v2";
        private PlayerController player;
        private Dictionary<string, CityActor> actors;
        private CityHud hud;
        private ContractProgress progress;
        private Conversation conversation;
        private CityActor speakingTo;
        private Transform marker;
        private float safeTime;
        private bool paused, betweenLevels, ended;
        private Vector3 spawn;
        public ContractProgress Progress => progress;
        public bool HasEnded => ended;
        public bool IsBetweenLevels => betweenLevels;

        public void Initialize(PlayerController localPlayer, Dictionary<string, CityActor> districtActors, CityHud cityHud)
        {
            player = localPlayer; actors = districtActors; hud = cityHud; spawn = player.transform.position;
            TextAsset source = Resources.Load<TextAsset>("City/old_quarter_story");
            if (source == null) { Debug.LogError("Old Quarter story JSON is missing."); enabled = false; return; }
            StoryFile story = JsonUtility.FromJson<StoryFile>(source.text);
            CampaignSave saved = null;
            try { if (PlayerPrefs.HasKey(SaveKey)) saved = JsonUtility.FromJson<CampaignSave>(PlayerPrefs.GetString(SaveKey)); }
            catch (Exception) { Debug.LogWarning("Campaign checkpoint could not be read; starting a new campaign."); }
            progress = new ContractProgress(story, saved);
            hud.Choose = Choose;
            marker = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
            marker.name = "Objective beacon"; marker.localScale = Vector3.one * 0.3f;
            var collider = marker.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
            marker.GetComponent<Renderer>().sharedMaterial = actors["safehouse"].GetComponent<Renderer>().sharedMaterial;
            if (progress.Finished) ShowResults();
            else hud.Notify($"Level {progress.ContractIndex + 1}/{story.contracts.Length}. Talk to Mara to begin.");
        }

        private void Update()
        {
            if (progress == null || player == null) return;
            var keyboard = Keyboard.current;
            // Results own input. Escape must never unlock a completed campaign.
            if (ended) return;
            if (betweenLevels)
            {
                if (keyboard != null && keyboard.enterKey.wasPressedThisFrame) ContinueCampaign();
                return;
            }
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                if (conversation != null) CloseConversation(); else OpenPause();
            }
            if (conversation != null)
            {
                if (keyboard != null)
                {
                    if (keyboard.digit1Key.wasPressedThisFrame) Choose(0);
                    else if (keyboard.digit2Key.wasPressedThisFrame) Choose(1);
                    else if (keyboard.digit3Key.wasPressedThisFrame) Choose(2);
                }
                return;
            }
            if (progress.Tick(Time.deltaTime)) { Fail("Time ran out. Retry the briefing with Mara. -200 score."); return; }
            bool pursued = IsPursued();
            foreach (var officer in PoliceHeatRegistry.ActivePolice)
            {
                if (officer == null || !officer.isChasing || !officer.HasVisualContact) continue;
                if (Vector3.Distance(player.transform.position, officer.transform.position) < 1.5f)
                { Fail("Caught. This level restarts; earlier scores stay. -200 score."); return; }
            }
            CityActor target = ObjectiveTarget();
            float distance = target != null ? Vector3.Distance(player.transform.position, target.transform.position) : 0;
            if (marker != null)
            {
                marker.gameObject.SetActive(target != null);
                if (target != null) marker.position = new Vector3(target.transform.position.x,
                    Mathf.Min(target.transform.position.y + 1.8f, 3.1f) + Mathf.Sin(Time.time * 2) * 0.1f, target.transform.position.z);
            }
            ContractStep step = progress.Step;
            if (step != null && step.holdSeconds > 0)
            {
                bool inside = step.target != "safehouse" || CityInterior.RoomAt(player.transform.position).Contains("Workshop");
                safeTime = distance < 1.8f && inside && (!step.requiresClear || !pursued) ? safeTime + Time.deltaTime : 0;
                if (safeTime >= step.holdSeconds) { Signal(step.signal); safeTime = 0; if (betweenLevels || ended) return; }
            }
            CityActor nearby = NearestInteractable();
            string prompt = nearby != null ? "[ E ]  " + nearby.displayName : "";
            if (pursued) prompt = "Break sight around buildings. Officers can open doors during a chase.";
            if (safeTime > 0 && progress.Step != null) prompt = $"Hold position... {Mathf.CeilToInt(progress.Step.holdSeconds - safeTime)}s";
            string heading = target != null ? target.displayName : "";
            if (target != null && player.playerCamera != null)
            {
                float angle = Vector3.SignedAngle(Vector3.ProjectOnPlane(player.playerCamera.forward, Vector3.up),
                    Vector3.ProjectOnPlane(target.transform.position - player.transform.position, Vector3.up), Vector3.up);
                heading = (angle < -20 ? "<  " : angle > 20 ? ">  " : "^  ") + heading;
            }
            hud.Refresh(progress, distance, heading, PoliceStatus(), prompt, CityInterior.RoomAt(player.transform.position));
            hud.UpdateMap(player.transform.position, target != null ? (Vector3?)target.transform.position : null, actors["officer"].transform.position);
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame && nearby != null)
            {
                // Doors remain usable while escaping. Ordinary dialogue does not pause a pursuit.
                if (nearby.GetComponent<CityDoor>() != null || !pursued) Interact(nearby);
            }
        }

        private CityActor ObjectiveTarget()
        {
            string id = progress.Step != null ? progress.Step.target : "";
            return actors.TryGetValue(id, out CityActor actor) ? actor : null;
        }
        private CityActor NearestInteractable()
        {
            CityActor closest = null; float best = 2.7f;
            foreach (CityActor actor in actors.Values)
            {
                if (actor == null || !actor.gameObject.activeInHierarchy) continue;
                if (actor.GetComponent<CityDoor>() == null && string.IsNullOrEmpty(actor.defaultDialogue) && actor != ObjectiveTarget()) continue;
                if (actor.id == "safehouse" || actor.id == "extraction") continue;
                float distance = Vector3.Distance(player.transform.position, actor.transform.position);
                if (distance >= best) continue;
                Vector3 from = player.transform.position + Vector3.up * 1.3f;
                Vector3 to = actor.transform.position + Vector3.up * 1.3f;
                bool blocked = false;
                foreach (var hit in Physics.RaycastAll(from, (to - from).normalized, Vector3.Distance(from, to), ~0, QueryTriggerInteraction.Ignore))
                    if (!hit.transform.IsChildOf(player.transform) && !hit.transform.IsChildOf(actor.transform)) { blocked = true; break; }
                if (blocked) continue;
                best = distance; closest = actor;
            }
            return closest;
        }
        private void Interact(CityActor actor)
        {
            var door = actor.GetComponent<CityDoor>();
            if (door != null)
            {
                if (door.minimumLevel == 5 && progress.ContractIndex == 4 && progress.StepIndex < 2)
                { hud.Notify("Ada has the radio house key. Complete the current objective first."); return; }
                if (!door.Toggle(progress.ContractIndex + 1)) hud.Notify($"This room opens in level {door.minimumLevel}.");
                return;
            }
            if (actor == ObjectiveTarget() && progress.Step != null)
            {
                if (progress.Step.holdSeconds > 0) return;
                if (string.IsNullOrEmpty(progress.Step.dialogue)) Signal(progress.Step.signal);
                else OpenConversation(progress.Step.dialogue, actor);
            }
            else if (!string.IsNullOrEmpty(actor.defaultDialogue)) OpenConversation(actor.defaultDialogue, actor);
        }
        private void OpenConversation(string id, CityActor actor)
        {
            Conversation next = progress.FindConversation(id);
            if (next == null) { Debug.LogError("Missing conversation: " + id); return; }
            speakingTo = actor;
            var wander = actor != null ? actor.GetComponent<NPCWanderRoadOnly>() : null;
            if (wander != null) { wander.canMove = false; wander.StopAgent(); }
            if (actor != null && actor.GetComponentInChildren<StreetPuppet>() != null)
            {
                Vector3 look = Vector3.ProjectOnPlane(player.transform.position - actor.transform.position, Vector3.up);
                if (look.sqrMagnitude > 0.01f) actor.transform.rotation = Quaternion.LookRotation(look);
            }
            conversation = next; player.SetDialogueInputLocked(true); player.UnlockMouse(); hud.Show(next);
        }
        private void Choose(int index)
        {
            if (conversation == null || conversation.choices == null || index < 0 || index >= conversation.choices.Length) return;
            StoryChoice choice = conversation.choices[index];
            if (choice.signal == "resume") { CloseConversation(); return; }
            if (choice.signal == "restart") { Fail("Level restarted. -200 score. Earlier results remain saved."); return; }
            if (choice.signal == "new_campaign") { NewCampaign(); return; }
            if (choice.signal == "learn_route")
            { progress.KnowsRoute = true; hud.Notify("Locker code learned. The ledger can be taken without an alarm."); }
            else if (choice.signal == "mistake")
            { progress.RecordMistake(); SaveCheckpoint(); hud.Notify("Wrong answer. -75 score. Check the clue and try again."); }
            else if (choice.signal != "puzzle_click" && !string.IsNullOrEmpty(choice.signal)) Signal(choice.signal);
            if (betweenLevels || ended) return;
            if (string.IsNullOrEmpty(choice.next)) CloseConversation();
            else
            {
                Conversation next = progress.FindConversation(choice.next);
                if (next == null) { Debug.LogError("Missing conversation: " + choice.next); CloseConversation(); }
                else { conversation = next; hud.Show(next); }
            }
        }
        private void Signal(string signal)
        {
            int completed = progress.ContractIndex;
            bool quiet = progress.KnowsRoute;
            CityActor oldTarget = ObjectiveTarget();
            if (!progress.Signal(signal)) return;
            if (signal.StartsWith("collect_") && oldTarget != null) oldTarget.gameObject.SetActive(false);
            if ((signal == "collect_ledger" && !quiet) || signal == "broadcast")
            {
                progress.RaiseAlarm();
                GameMaster.Instance.ReportCrime(player.gameObject);
                actors["officer"].GetComponent<PoliceBehavior>().isCalled = true;
                hud.Notify(signal == "broadcast" ? "Evidence is on air! Lose the officer, then reach extraction." : "Alarm! Lose the officer before entering the safehouse.");
            }
            else if (signal == "collect_ledger") hud.Notify("Code accepted. Ledger secured without an alarm.");
            else if (signal == "restore_power") hud.Notify("Power restored. Tell Mara the workshop is working again.");
            SaveCheckpoint();
            if (completed != progress.ContractIndex) ShowResults();
        }
        private void ShowResults()
        {
            CloseConversation(); safeTime = 0;
            ended = progress.Finished; betweenLevels = !ended;
            if (ended)
            {
                PlayerPrefs.SetInt(BestKey, Mathf.Max(PlayerPrefs.GetInt(BestKey), progress.Score));
                PlayerPrefs.Save();
            }
            if (marker != null) marker.gameObject.SetActive(false);
            Time.timeScale = 0; player.SetDialogueInputLocked(true); player.UnlockMouse();
            hud.ShowResults(progress, PlayerPrefs.GetInt(BestKey), ContinueCampaign, NewCampaign, QuitGame);
        }
        public void ContinueCampaign()
        {
            if (!betweenLevels || ended) return;
            betweenLevels = false; hud.HideResults(); Time.timeScale = 1;
            player.SetDialogueInputLocked(false); player.LockMouse();
            hud.Notify($"Level {progress.ContractIndex + 1}/{progress.Story.contracts.Length} unlocked. Return to Mara.");
        }
        public void NewCampaign()
        {
            PlayerPrefs.DeleteKey(SaveKey); PlayerPrefs.Save();
            Time.timeScale = 1; SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
        public void QuitGame()
        {
            Time.timeScale = 1;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        private void Fail(string reason)
        {
            if (ended || betweenLevels) return;
            CloseConversation(); progress.Fail(); safeTime = 0;
            GameMaster gm = GameMaster.Instance;
            if (gm != null) { gm.arrest = false; gm.ClearPoliceChase(player.gameObject); }
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false; player.transform.position = spawn; controller.enabled = true; player.ResetMotion();
            foreach (string id in new[] { "parcel", "fuse", "ledger" }) actors[id].gameObject.SetActive(true);
            SaveCheckpoint(); hud.Notify(reason);
        }
        private bool IsPursued()
        {
            foreach (var officer in PoliceHeatRegistry.ActivePolice)
                if (officer != null && (officer.isChasing || officer.isCalled)) return true;
            return false;
        }
        private string PoliceStatus()
        {
            foreach (var officer in PoliceHeatRegistry.ActivePolice)
                if (officer != null && (officer.isChasing || officer.isCalled))
                    return officer.State == PoliceBehavior.PursuitState.Searching ? "POLICE / Searching" : "POLICE / Pursuit";
            return "DISTRICT / Clear";
        }
        private void SaveCheckpoint() { PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(progress.Capture())); PlayerPrefs.Save(); }
        private void OpenPause() { paused = true; Time.timeScale = 0; OpenConversation("menu.pause", null); }
        private void CloseConversation()
        {
            if (paused) { Time.timeScale = 1; paused = false; }
            var wander = speakingTo != null ? speakingTo.GetComponent<NPCWanderRoadOnly>() : null;
            if (wander != null) wander.ResumeMovement();
            speakingTo = null; conversation = null;
            if (hud != null) hud.Show(null);
            if (player != null) { player.SetDialogueInputLocked(false); player.LockMouse(); }
        }
        private void OnDestroy()
        {
            Time.timeScale = 1;
            if (marker != null) Destroy(marker.gameObject);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
    }
}
