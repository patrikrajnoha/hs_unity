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
        private const float InteractionRange = 2.7f;
        private const int PoliceReportDelay = 8;
        private const float SaleOfferCooldown = 25f;
        private PlayerController player;
        private Inventory inventory;
        private PlayerProductEffects productEffects;
        private Dictionary<string, CityActor> actors;
        private CityHud hud;
        private ContractProgress progress;
        private Conversation conversation;
        private CityActor speakingTo;
        private CityStreetTrade streetTrade;
        private float policeReportAt = -1f;
        private Vector3 policeReportPosition;
        private GameObject objectiveGroundGlow;
        private MeshRenderer objectiveGroundGlowRenderer;
        private Material objectiveGroundGlowMaterial;
        private Texture2D objectiveGroundGlowTexture;
        private float safeTime;
        private bool betweenLevels, ended;
        private Vector3 spawn;
        private float gameplayElapsed, sampleTime, objectiveElapsed;
        public static CityMissionDirector TelemetryOwner { get; private set; }
        public ContractProgress Progress => progress;
        public bool HasEnded => ended;
        public bool IsBetweenLevels => betweenLevels;
        // The inventory should hide only behind a visible conversation panel.
        // The data object can remain populated after a prompt has closed.
        public bool IsConversationActive => hud != null && hud.IsDialogueVisible;

        /// <summary>
        /// Story interactions use the same E key as world pickups. Expose the
        /// priority decision so a nearby pickup cannot consume the keypress
        /// intended for Mara, a door, or the current mission target.
        /// </summary>
        public bool HasPriorityInteraction(GameObject candidate)
        {
            if (candidate == null || player == null || candidate != player.gameObject || progress == null ||
                conversation != null || betweenLevels || ended)
                return false;

            return NearestInteractable() != null;
        }

        public void Initialize(PlayerController localPlayer, Dictionary<string, CityActor> districtActors, CityHud cityHud)
        {
            player = localPlayer; actors = districtActors; hud = cityHud; spawn = player.transform.position;
            inventory = player.GetComponent<Inventory>();
            productEffects = player.GetComponent<PlayerProductEffects>();
            hud.TrackProductEffects(productEffects);
            TextAsset source = Resources.Load<TextAsset>("City/old_quarter_story");
            if (source == null)
            {
                GameLogger.LogError("story", "story_missing", "Old Quarter story JSON is missing.");
                Debug.LogError("Old Quarter story JSON is missing.");
                enabled = false;
                return;
            }
            StoryFile story = JsonUtility.FromJson<StoryFile>(source.text);
            LocalizationManager.LocalizeStory(story);
            CampaignSave saved = null;
            try
            {
                if (PlayerPrefs.HasKey(SaveKey))
                    saved = JsonUtility.FromJson<CampaignSave>(PlayerPrefs.GetString(SaveKey));
            }
            catch (Exception ex)
            {
                GameLogger.LogWarning("save_system", "checkpoint_load_failed", "Campaign checkpoint could not be read: " + ex.Message);
                Debug.LogWarning("Campaign checkpoint could not be read; starting a new campaign.");
            }
            progress = new ContractProgress(story, saved);
            TelemetryOwner = this;
            GameLogger.CampaignCompleted |= progress.Finished;
            hud.Choose = Choose;
            CreateObjectiveGroundGlow();
            GameLogger.LogInfo("campaign", progress.Finished ? "campaign_loaded" : "campaign_started", progress.Finished ? "Completed campaign loaded."
                : $"Campaign initialized at contract {progress.ContractIndex + 1}/{story.contracts.Length}");
            if (progress.Finished) ShowResults();
        }

        private void OnEnable()
        {
            LocalizationManager.LanguageChanged += RefreshLocalization;
        }

        private void RefreshLocalization()
        {
            if (progress == null || hud == null)
                return;

            LocalizationManager.LocalizeStory(progress.Story);
            hud.Refresh(progress, PoliceStatus(), ReportSecondsLeft());
            if (streetTrade != null)
                ShowTradeOffer();
            else if (conversation != null)
                hud.Show(conversation);
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
            if (Time.timeScale <= 0 || GameMaster.GameOverActive) return;
            DispatchPoliceReport();
            if (conversation != null)
            {
                if (policeReportAt >= 0)
                    hud.Refresh(progress, PoliceStatus(), ReportSecondsLeft());
                if (streetTrade != null && PoliceStatus() != CityPoliceStatus.Clear)
                {
                    CloseConversation();
                    return;
                }
                if (keyboard != null)
                {
                    if (keyboard.digit1Key.wasPressedThisFrame) Choose(0);
                    else if (keyboard.digit2Key.wasPressedThisFrame) Choose(1);
                    else if (keyboard.digit3Key.wasPressedThisFrame) Choose(2);
                }
                return;
            }
            gameplayElapsed += Time.deltaTime;
            sampleTime += Time.deltaTime;
            if (progress.Active) objectiveElapsed += Time.deltaTime;
            if (sampleTime >= 1f)
            {
                sampleTime %= 1f;
                var data = EventData(); data.elapsed = gameplayElapsed;
                GameLogger.Track("player_position", data);
            }
            if (progress.Tick(Time.deltaTime))
            {
                Fail(LocalizationManager.GetOrFallback("ui.notice.time_ran_out",
                    "Time ran out. Retry the briefing with Mara. -200 score."), "timeout");
                return;
            }
            CityPoliceStatus police = PoliceStatus();
            bool pursued = police != CityPoliceStatus.Clear;
            foreach (var officer in PoliceHeatRegistry.ActivePolice)
            {
                if (officer == null || !officer.isChasing || !officer.HasVisualContact) continue;
                if (Vector3.Distance(player.transform.position, officer.transform.position) >= 1.5f) continue;
                if (progress.Active)
                    Fail(LocalizationManager.GetOrFallback("ui.notice.caught",
                        "Caught. This level restarts; earlier scores stay. -200 score."), "caught");
                else
                {
                    ClearPoliceAndRespawn();
                    hud.Notify(LocalizationManager.Get("ui.street_trade.caught"));
                }
                return;
            }
            CityActor target = ObjectiveTarget();
            float distance = target != null ? Vector3.Distance(player.transform.position, target.transform.position) : 0;
            if (target != null && distance > 2.7f) progress.RecordNavigation(Time.deltaTime);
            bool objectiveVisible = target != null && target.gameObject.activeInHierarchy;
            UpdateObjectiveGroundGlow(target, objectiveVisible);
            ContractStep step = progress.Step;
            if (step != null && step.holdSeconds > 0)
            {
                bool inside = step.target != "safehouse" || CityInterior.RoomAt(player.transform.position).Contains("Workshop");
                safeTime = distance < 1.8f && inside && (!step.requiresClear || !pursued) ? safeTime + Time.deltaTime : 0;
                if (safeTime >= step.holdSeconds) { Signal(step.signal); safeTime = 0; if (betweenLevels || ended) return; }
            }
            CityActor nearby = NearestInteractable();
            var door = nearby != null ? nearby.GetComponent<CityDoor>() : null;
            // Doors remain usable while escaping. Ordinary dialogue does not pause a pursuit.
            bool canInteract = nearby != null && (door != null || !pursued);
            string nearbyName = canInteract ? LocalizationManager.GetCityActorName(nearby.id, nearby.displayName) : "";
            CityActor customer = !pursued && policeReportAt < 0 && inventory != null && safeTime <= 0
                ? (nearby != null && nearby.IsPerson ? nearby : NearestActor(peopleOnly: true))
                : null;
            string customerName = customer != null ? LocalizationManager.GetCityActorName(customer.id, customer.displayName) : "";
            string heading = target != null
                ? LocalizationManager.GetCityActorName(target.id, target.displayName)
                : "";
            Vector3 forward = player.playerCamera != null ? player.playerCamera.forward : player.transform.forward;
            Vector3 facing = Vector3.ProjectOnPlane(forward, Vector3.up);
            if (facing.sqrMagnitude < 0.001f) facing = player.transform.forward;
            float angle = target == null ? 0 : Vector3.SignedAngle(facing,
                Vector3.ProjectOnPlane(target.transform.position - player.transform.position, Vector3.up), Vector3.up);

            hud.Refresh(progress, police, ReportSecondsLeft());
            hud.UpdateNavigation(heading, distance, angle);
            hud.UpdateInteraction(canInteract ? InteractionAction(nearby, door) : "", nearbyName, safeTime, step?.holdSeconds ?? 0, customerName);
            actors.TryGetValue("officer", out CityActor mapOfficer);
            hud.UpdateMap(player.transform.position, facing, target != null ? (Vector3?)target.transform.position : null,
                mapOfficer != null ? (Vector3?)mapOfficer.transform.position : null);
            if (WarehouseGuideUI.IsOpen) return;
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame && canInteract) Interact(nearby);
            else if (keyboard != null && keyboard.gKey.wasPressedThisFrame && customer != null) OfferProduct(customer);
            else if (keyboard != null && keyboard.xKey.wasPressedThisFrame && productEffects != null && inventory != null)
                ConsumeSelectedProduct();
        }

        private void ConsumeSelectedProduct()
        {
            Item selected = inventory.GetSelectedItem();
            if (productEffects.TryConsume(inventory, selected))
            {
                hud.Notify(LocalizationManager.Get("ui.product_effects.consumed", "item", LocalizationManager.GetItemName(selected)));
                GameLogger.LogInfo("inventory", "product_consumed", $"Consumed {selected.name}");
            }
            else hud.Notify(LocalizationManager.Get("ui.product_effects.select_product"));
        }

        private static string InteractionAction(CityActor actor, CityDoor door)
        {
            if (door != null)
                return door.IsOpen
                    ? LocalizationManager.GetOrFallback("ui.city.close_door", "Close door")
                    : LocalizationManager.GetOrFallback("ui.city.open_door", "Open door");
            if (!string.IsNullOrEmpty(actor.defaultDialogue))
                return LocalizationManager.GetOrFallback("ui.city.talk", "Talk");
            return LocalizationManager.GetOrFallback("ui.city.inspect", "Inspect");
        }

        private CityActor ObjectiveTarget()
        {
            string id = progress.Step != null ? progress.Step.target : "";
            return actors.TryGetValue(id, out CityActor actor) ? actor : null;
        }
        private CityActor NearestInteractable() => NearestActor(peopleOnly: false);

        private CityActor NearestActor(bool peopleOnly)
        {
            CityActor closest = null; float best = InteractionRange;
            CityActor objective = ObjectiveTarget();
            foreach (CityActor actor in actors.Values)
            {
                if (actor == null || !actor.gameObject.activeInHierarchy) continue;
                if (peopleOnly)
                {
                    if (!actor.IsPerson) continue;
                }
                else if ((actor.GetComponent<CityDoor>() == null && string.IsNullOrEmpty(actor.defaultDialogue) && actor != objective) ||
                    actor.id == "safehouse" || actor.id == "extraction") continue;
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
                { hud.Notify(LocalizationManager.GetOrFallback("ui.notice.radio_key", "Ada has the radio house key. Complete the current objective first.")); return; }
                if (!door.Toggle(progress.ContractIndex + 1))
                    hud.Notify(LocalizationManager.GetOrFallback("ui.notice.room_locked", "This room opens in level {level}.",
                        "level", door.minimumLevel.ToString()));
                return;
            }
            if (actor.id == "mara" && !progress.Active && !progress.Finished)
            {
                // Mara offers the next contract from Free Roam; declining simply closes the conversation.
                OpenConversation(progress.Step.dialogue, actor);
            }
            else if (actor == ObjectiveTarget() && progress.Step != null)
            {
                if (progress.Step.holdSeconds > 0) return;
                if (actor.id == "ledger") { OpenConversation("ledger.open", actor); return; }
                if (string.IsNullOrEmpty(progress.Step.dialogue)) Signal(progress.Step.signal);
                else OpenConversation(progress.Step.dialogue, actor);
            }
            else if (!string.IsNullOrEmpty(actor.defaultDialogue)) OpenConversation(actor.defaultDialogue, actor);
        }
        private void OpenConversation(string id, CityActor actor)
        {
            if (progress.Finished && actor != null &&
                (actor.id == "mara" || actor.id == "ada" || actor.id == "tomas")) id = actor.id + ".after_campaign";
            Conversation next = progress.FindConversation(id);
            if (next == null)
            {
                GameLogger.LogError("story", "missing_conversation", "Missing conversation: " + id);
                Debug.LogError("Missing conversation: " + id);
                return;
            }
            BeginConversation(actor, next);
        }

        private void BeginConversation(CityActor actor, Conversation next)
        {
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

        private void OfferProduct(CityActor actor)
        {
            Item product = inventory.GetSelectedItem();
            if (!CityStreetTrade.IsSaleProduct(product))
            {
                hud.Notify(LocalizationManager.Get("ui.street_trade.select_product"));
                return;
            }
            if (Time.time < actor.NextSaleOfferTime)
            {
                hud.Notify(LocalizationManager.Get("ui.street_trade.cooldown"));
                return;
            }

            actor.NextSaleOfferTime = Time.time + SaleOfferCooldown;
            CityStreetTrade.Reaction reaction = CityStreetTrade.RollReaction(actor);
            if (reaction == CityStreetTrade.Reaction.Negotiate)
            {
                streetTrade = new CityStreetTrade(product, inventory.GetItemAmount(product));
                BeginConversation(actor, streetTrade.BuildConversation(actor));
                return;
            }

            string name = LocalizationManager.GetCityActorName(actor.id, actor.displayName);
            if (reaction == CityStreetTrade.Reaction.Report)
            {
                policeReportPosition = actor.transform.position;
                policeReportAt = Time.time + PoliceReportDelay;
                hud.Notify(LocalizationManager.Get("ui.street_trade.reported", "npc", name));
            }
            else hud.Notify(LocalizationManager.Get("ui.street_trade.ignored", "npc", name));
        }

        private int ReportSecondsLeft() => Mathf.CeilToInt(Mathf.Max(0, policeReportAt - Time.time));

        private void DispatchPoliceReport()
        {
            if (policeReportAt < 0 || Time.time < policeReportAt) return;
            policeReportAt = -1f;

            PoliceBehavior closest = null;
            float closestDistance = float.PositiveInfinity;
            foreach (var officer in PoliceHeatRegistry.ActivePolice)
            {
                if (officer == null || officer.gameObject.scene != gameObject.scene) continue;
                float distance = (officer.transform.position - policeReportPosition).sqrMagnitude;
                if (distance >= closestDistance) continue;
                closestDistance = distance;
                closest = officer;
            }

            GameMaster gm = GameMaster.Instance;
            if (closest == null || gm == null || !gm.BeginPoliceChase(player.gameObject))
            {
                hud.Notify(LocalizationManager.Get("ui.street_trade.no_response"));
                return;
            }

            if (conversation != null) CloseConversation();
            if (!closest.isChasing) closest.isCalled = true;
            if (progress.Active)
            {
                progress.RaiseAlarm();
                SaveCheckpoint();
            }
            hud.Notify(LocalizationManager.Get("ui.street_trade.pursuit"));
        }

        private void ShowTradeOffer()
        {
            conversation = streetTrade.BuildConversation(speakingTo);
            hud.Show(conversation);
        }

        private void ChooseTrade(int index)
        {
            switch (streetTrade.CurrentStage)
            {
                case CityStreetTrade.Stage.Quantity when index < 2:
                    streetTrade.SelectQuantity(wholeStack: index == 1);
                    ShowTradeOffer();
                    return;
                case CityStreetTrade.Stage.InitialOffer when index == 1:
                    if (streetTrade.MakeCounteroffer()) CompleteStreetSale();
                    else ShowTradeOffer();
                    return;
                case CityStreetTrade.Stage.InitialOffer when index == 0:
                case CityStreetTrade.Stage.FinalOffer when index == 0:
                    CompleteStreetSale();
                    return;
                default:
                    CloseConversation();
                    return;
            }
        }

        private void CompleteStreetSale()
        {
            CityStreetTrade sale = streetTrade;
            if (speakingTo == null || !speakingTo.gameObject.activeInHierarchy ||
                Vector3.Distance(player.transform.position, speakingTo.transform.position) >= InteractionRange ||
                PoliceStatus() != CityPoliceStatus.Clear)
            {
                CloseConversation();
                hud.Notify(LocalizationManager.Get("ui.street_trade.interrupted"));
                return;
            }
            if (inventory.GetItemAmount(sale.Product) < sale.Quantity || !progress.CanCreditStreetSale(sale.Total) ||
                !inventory.RemoveItem(sale.Product, sale.Quantity))
            {
                CloseConversation();
                hud.Notify(LocalizationManager.Get("ui.street_trade.unavailable"));
                return;
            }

            progress.CreditStreetSale(sale.Total);
            SaveCheckpoint();
            GameLogger.LogInfo("economy", "street_sale_completed", $"Sold {sale.Quantity} x {sale.Product.name} to {speakingTo.id} for ${sale.Total}");
            CloseConversation();
            hud.Notify(LocalizationManager.Get("ui.street_trade.sold", "count", sale.Quantity.ToString(),
                "item", LocalizationManager.GetItemName(sale.Product), "price", sale.Total.ToString("N0")));
        }

        private void Choose(int index)
        {
            if (conversation == null || conversation.choices == null || index < 0 || index >= conversation.choices.Length) return;
            if (streetTrade != null) { ChooseTrade(index); return; }
            StoryChoice choice = conversation.choices[index];
            var decision = EventData(); decision.dialogueId = conversation.id;
            decision.choice = index.ToString(System.Globalization.CultureInfo.InvariantCulture); decision.signal = choice.signal;
            GameLogger.Track("choice_selected", decision);
            if (choice.signal == "resume") { CloseConversation(); return; }
            if (choice.signal == "restart") { Fail("Level restarted. -200 score. Earlier results remain saved."); return; }
            if (choice.signal == "new_campaign") { NewCampaign(); return; }
            if (choice.signal == "use_locker_code" || choice.signal == "force_locker")
            {
                if (progress.Step?.signal != "collect_ledger") { CloseConversation(); return; }
                if (choice.signal == "use_locker_code" && !progress.KnowsRoute)
                { hud.Notify(LocalizationManager.GetOrFallback("ui.notice.ask_ada", "Ask Ada at the docks for the code, or force the locker and trigger an alarm.")); return; }
                if (choice.signal == "force_locker") progress.KnowsRoute = false;
                Signal("collect_ledger"); CloseConversation(); return;
            }
            if (choice.signal == "learn_route")
            { if (progress.Current != null && progress.Current.id == "paper_trail" && progress.Active)
                { progress.KnowsRoute = true; progress.Strategy = "locker_code"; }
              hud.Notify(LocalizationManager.GetOrFallback("ui.notice.locker_code", "Locker code learned. The ledger can be taken without an alarm.")); }
            else if (choice.signal == "mistake")
            { GameAudio.Play(AudioCue.Failure); progress.RecordMistake(); SaveCheckpoint(); hud.Notify(LocalizationManager.GetOrFallback("ui.notice.wrong_answer", "Wrong answer. -75 score. Check the clue and try again.")); }
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
            var data = EventData();
            bool starting = !progress.Active && !progress.Finished;
            data.objectiveSeconds = objectiveElapsed;
            if (signal == "collect_ledger" && progress.Step?.signal == signal) progress.Strategy = quiet ? "locker_code" : "forced_locker";
            data.strategy = progress.Strategy;
            if (!progress.Signal(signal)) return;
            GameAudio.Play(completed != progress.ContractIndex ? AudioCue.Success : signal.StartsWith("collect_") ? AudioCue.Pickup : AudioCue.Objective);
            GameLogger.Track("objective_complete", data); objectiveElapsed = 0;
            if (starting) { data.mode = "mission"; GameLogger.Track("mission_start", data); }
            if (signal.StartsWith("collect_"))
            {
                data.itemId = signal.Substring("collect_".Length);
                GameLogger.Track("item_collected", data);
            }
            GameLogger.LogInfo("campaign", "objective_signaled", $"Signal '{signal}' completed in contract {completed + 1}");
            if (signal.StartsWith("collect_") && oldTarget != null) oldTarget.gameObject.SetActive(false);
            if ((signal == "collect_ledger" && !quiet) || signal == "broadcast")
            {
                progress.RaiseAlarm();
                if (GameMaster.Instance != null) GameMaster.Instance.ReportCrime(player.gameObject);
                if (actors.TryGetValue("officer", out CityActor officer) && officer.TryGetComponent(out PoliceBehavior police)) police.isCalled = true;
                hud.Notify(signal == "broadcast"
                    ? LocalizationManager.GetOrFallback("ui.notice.evidence_on_air", "Evidence is on air! Lose the officer, then reach extraction.")
                    : LocalizationManager.GetOrFallback("ui.notice.alarm", "Alarm! Lose the officer before entering the safehouse."));
            }
            else if (signal == "collect_ledger") hud.Notify(LocalizationManager.GetOrFallback("ui.notice.code_accepted", "Code accepted. Ledger secured without an alarm."));
            else if (signal == "restore_power") hud.Notify(LocalizationManager.GetOrFallback("ui.notice.power_restored", "Power restored. Tell Mara the workshop is working again."));
            SaveCheckpoint();
            if (completed != progress.ContractIndex)
            {
                LevelResult result = progress.Results[completed];
                data.duration = result.seconds; data.score = result.score; data.totalScore = progress.Score;
                data.penalties = result.penalties; data.failures = result.failures; data.mistakes = result.mistakes;
                data.navigationSeconds = result.navigationSeconds; data.detections = result.detections;
                data.efficiency = result.efficiency; data.strategy = result.strategy;
                GameLogger.Track("mission_complete", data);
                GameLogger.LogInfo("campaign", "contract_completed", $"Contract {completed + 1} ({result.contractId}) completed. Mission score: {result.score}; campaign score: {progress.Score}; duration: {result.seconds:0.0}s; failures: {result.failures}; mistakes: {result.mistakes}");
                if (progress.Finished)
                {
                    data.duration = progress.TotalTime; data.rank = progress.Rank; data.retries = progress.Failures;
                    data.failures = progress.Failures; data.mistakes = progress.Mistakes; data.completed = true;
                    GameLogger.Track("campaign_complete", data); GameLogger.CampaignCompleted = true;
                    GameLogger.LogInfo("campaign", "campaign_finished", $"Campaign finished with total score {progress.Score}; rank {progress.Rank}; duration {progress.TotalTime:0.0}s; retries {progress.Failures}");
                }
                ShowResults();
            }
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
            if (objectiveGroundGlow != null) objectiveGroundGlow.SetActive(false);
            Time.timeScale = 0; player.SetDialogueInputLocked(true); player.UnlockMouse();
            hud.ShowResults(progress, PlayerPrefs.GetInt(BestKey), ContinueCampaign, ContinueFreeRoam, NewCampaign, QuitGame);
        }
        public void ContinueCampaign()
        {
            if (!betweenLevels || ended) return;
            betweenLevels = false; hud.HideResults(); Time.timeScale = 1;
            player.SetDialogueInputLocked(false); player.LockMouse();
            GameLogger.Track("free_roam_start", EventData());
            hud.Notify(LocalizationManager.GetOrFallback("ui.notice.level_unlocked",
                "Level {level}/{total} unlocked. Return to Mara.",
                "level", (progress.ContractIndex + 1).ToString(), "total", progress.Story.contracts.Length.ToString()));
        }
        public void ContinueFreeRoam()
        {
            if (!ended) return;
            ended = false; betweenLevels = false; hud.HideResults(); Time.timeScale = 1;
            player.SetDialogueInputLocked(false); player.LockMouse();
            GameLogger.Track("free_roam_start", EventData());
            hud.Notify(LocalizationManager.GetOrFallback("ui.notice.campaign_complete",
                "Campaign complete. Free Roam remains open; explore the Old Quarter."));
        }
        public void NewCampaign()
        {
            GameLogger.Track("campaign_restart", EventData());
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
        private void Fail(string reason, string reasonId = "restart")
        {
            if (ended || betweenLevels || progress.Finished) return;
            var data = EventData(); data.reason = reasonId; data.objectiveSeconds = objectiveElapsed;
            GameLogger.Track("mission_failed", data); objectiveElapsed = 0;
            GameAudio.Play(AudioCue.Failure);
            CloseConversation(); progress.Fail(); safeTime = 0;
            GameLogger.LogWarning("campaign", "contract_failed", $"Contract {progress.ContractIndex + 1} failed: {reason}");
            ClearPoliceAndRespawn();
            foreach (string id in new[] { "parcel", "fuse", "ledger" }) actors[id].gameObject.SetActive(true);
            SaveCheckpoint(); hud.Notify(reason);
        }

        private void ClearPoliceAndRespawn()
        {
            policeReportAt = -1f;
            GameMaster gm = GameMaster.Instance;
            if (gm != null) { gm.arrest = false; gm.ClearPoliceChase(player.gameObject); }
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false; player.transform.position = spawn; controller.enabled = true; player.ResetMotion();
        }
        private GameplayEventData EventData()
        {
            return new GameplayEventData {
                missionId = progress?.Current?.id, objectiveId = progress?.Step?.signal,
                missionNumber = progress != null && !progress.Finished ? progress.ContractIndex + 1 : 0,
                attempt = (progress?.CurrentFailures ?? 0) + 1, elapsed = progress?.Elapsed ?? 0,
                mode = progress != null && progress.Active ? "mission" : "free_roam",
                hasPosition = player != null, position = player != null ? player.transform.position : Vector3.zero,
                rotation = player != null ? player.transform.eulerAngles : Vector3.zero
            };
        }
        public void RecordPoliceDetection(string reason, Vector3 position)
        {
            progress?.RecordDetection();
            var data = EventData(); data.reason = reason; data.position = position; data.hasPosition = true;
            GameLogger.Track("police_alert", data);
        }
        public void RecordItem(string itemId, Vector3 position)
        {
            var data = EventData(); data.itemId = itemId; data.position = position; data.hasPosition = true;
            GameLogger.Track("item_collected", data);
        }
        private static CityPoliceStatus PoliceStatus()
        {
            var status = CityPoliceStatus.Clear;
            foreach (var officer in PoliceHeatRegistry.ActivePolice)
            {
                if (officer == null || (!officer.isChasing && !officer.isCalled)) continue;
                if (officer.State != PoliceBehavior.PursuitState.Searching) return CityPoliceStatus.Pursuit;
                status = CityPoliceStatus.Searching;
            }
            return status;
        }

        private void CreateObjectiveGroundGlow()
        {
            objectiveGroundGlow = new GameObject("Objective ground glow", typeof(MeshFilter), typeof(MeshRenderer));
            objectiveGroundGlow.transform.SetParent(transform, false);
            objectiveGroundGlow.transform.localScale = new Vector3(4.2f, 1f, 4.2f);

            var mesh = new Mesh { name = "Objective ground glow plane" };
            mesh.vertices = new[] {
                new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f),
                new Vector3(0.5f, 0f, 0.5f), new Vector3(-0.5f, 0f, 0.5f)
            };
            mesh.uv = new[] {
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(1f, 1f), new Vector2(0f, 1f)
            };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            objectiveGroundGlow.GetComponent<MeshFilter>().sharedMesh = mesh;

            objectiveGroundGlowTexture = CreateRadialGlowTexture(64);
            objectiveGroundGlowMaterial = CreateGroundGlowMaterial(objectiveGroundGlowTexture);
            objectiveGroundGlowRenderer = objectiveGroundGlow.GetComponent<MeshRenderer>();
            objectiveGroundGlowRenderer.sharedMaterial = objectiveGroundGlowMaterial;
            objectiveGroundGlowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            objectiveGroundGlowRenderer.receiveShadows = false;
            objectiveGroundGlowRenderer.enabled = false;
        }

        private void UpdateObjectiveGroundGlow(CityActor target, bool visible)
        {
            if (objectiveGroundGlow == null || objectiveGroundGlowMaterial == null)
                return;

            objectiveGroundGlow.SetActive(visible);
            if (!visible)
                return;

            objectiveGroundGlow.transform.position = FindGroundPoint(target) + Vector3.up * 0.025f;
            objectiveGroundGlowRenderer.enabled = true;

            // Breathe only through opacity; the footprint never changes size.
            float pulse = 0.78f + Mathf.Sin(Time.time * 1.35f) * 0.10f;
            SetGlowOpacity(pulse);
        }

        private Vector3 FindGroundPoint(CityActor target)
        {
            Vector3 fallback = target.transform.position;
            RaycastHit[] hits = Physics.RaycastAll(
                target.transform.position + Vector3.up * 2f, Vector3.down, 8f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float closest = float.MaxValue;
            foreach (RaycastHit hit in hits)
            {
                if (hit.transform == target.transform || hit.transform.IsChildOf(target.transform))
                    continue;
                if (hit.distance < closest)
                {
                    closest = hit.distance;
                    fallback = hit.point;
                }
            }
            return fallback;
        }

        private Texture2D CreateRadialGlowTexture(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            texture.name = "Objective radial glow";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float distance = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                    float falloff = 1f - Mathf.SmoothStep(0f, 1f, distance);
                    float alpha = Mathf.Pow(falloff, 2.2f) * 0.16f;
                    texture.SetPixel(x, y, new Color(1f, 0.57f, 0.12f, alpha));
                }
            texture.Apply(false, true);
            return texture;
        }

        private Material CreateGroundGlowMaterial(Texture2D texture)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Transparent");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            var material = new Material(shader) { name = "Objective radial glow material" };
            material.mainTexture = texture;
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", texture);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", Color.white);
            if (material.HasProperty("_Surface"))
                material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend"))
                material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_SrcBlend"))
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend"))
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite"))
                material.SetFloat("_ZWrite", 0f);
            if (material.HasProperty("_Cull"))
                material.SetFloat("_Cull", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = 3000;
            return material;
        }

        private void SetGlowOpacity(float opacity)
        {
            Color color = new Color(1f, 1f, 1f, opacity);
            if (objectiveGroundGlowMaterial.HasProperty("_BaseColor"))
                objectiveGroundGlowMaterial.SetColor("_BaseColor", color);
            if (objectiveGroundGlowMaterial.HasProperty("_Color"))
                objectiveGroundGlowMaterial.SetColor("_Color", color);
        }

        private void SaveCheckpoint()
        {
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(progress.Capture()));
            PlayerPrefs.Save();
            GameLogger.LogDebug("save_system", "checkpoint_saved", $"Checkpoint saved at contract {progress.ContractIndex}");
        }
        public bool HandleEscape()
        {
            // Results and between-level screens own their input. Do not open a
            // second pause panel over those screens.
            if (ended || betweenLevels)
                return true;

            if (conversation == null)
                return false;

            CloseConversation();
            return true;
        }

        private void CloseConversation()
        {
            var wander = speakingTo != null ? speakingTo.GetComponent<NPCWanderRoadOnly>() : null;
            if (wander != null) wander.ResumeMovement();
            speakingTo = null; conversation = null; streetTrade = null;
            if (hud != null) hud.Show(null);
            if (player != null) { player.SetDialogueInputLocked(false); player.LockMouse(); }
        }
        private void OnDestroy()
        {
            if (TelemetryOwner == this) TelemetryOwner = null;
            LocalizationManager.LanguageChanged -= RefreshLocalization;
            Time.timeScale = 1;
            if (objectiveGroundGlow != null) Destroy(objectiveGroundGlow);
            if (objectiveGroundGlowMaterial != null) Destroy(objectiveGroundGlowMaterial);
            if (objectiveGroundGlowTexture != null) Destroy(objectiveGroundGlowTexture);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
    }
}
