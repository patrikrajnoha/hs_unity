using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace HighStakes.City
{
    public enum CityPoliceStatus { Clear, Searching, Pursuit }

    public sealed class CityHud : MonoBehaviour
    {
        private static readonly Color Ink = new Color32(26, 17, 35, 242);
        private static readonly Color Raised = new Color32(61, 39, 64, 255);
        private static readonly Color White = new Color32(255, 246, 241, 255);
        private static readonly Color Muted = new Color32(223, 207, 222, 255);
        private static readonly Color Peach = new Color32(255, 183, 132, 255);
        private static readonly Color Pink = new Color32(255, 143, 179, 255);
        private static readonly Color Aqua = new Color32(146, 228, 220, 255);
        private static readonly Color Danger = new Color32(255, 115, 132, 255);
        private static readonly Vector2 TopLeft = new Vector2(0, 1);
        private static readonly Vector2 TopRight = Vector2.one;
        private static readonly Vector2 TopCenter = new Vector2(0.5f, 1);
        private static readonly Vector2 Center = Vector2.one * 0.5f;
        private static readonly Vector2 BottomCenter = new Vector2(0.5f, 0);
        private const float MapSize = 196f;
        private const int ProductEffectCount = (int)PlayerProductEffects.Effect.Count;
        private const float ProductEffectRowHeight = 34f;

        private Font font;
        private RectTransform root, clockCard, policeCard, policeHintCard, navigation, interaction, saleInteraction, holdCard, holdFill, toastCard, mapCard;
        private Text chapter, mission, objective, clockCaption, clock, policeCaption, police, policeHint;
        private Text destination, prompt, promptTarget, saleAction, saleTarget, balance, holdText, toast, body, speaker, mapLabel;
        private Image policeIndicator;
        private Transform directionArrow;
        private GameObject dialogue, resultOverlay;
        private readonly Button[] choices = new Button[3];
        private readonly Text[] labels = new Text[3];
        private PlayerProductEffects productEffects;
        private RectTransform productEffectCard;
        private readonly RectTransform[] productEffectRows = new RectTransform[ProductEffectCount];
        private readonly RectTransform[] productEffectMeters = new RectTransform[ProductEffectCount];
        private readonly Text[] productEffectNames = new Text[ProductEffectCount];
        private readonly Text[] productEffectTimes = new Text[ProductEffectCount];
        private float toastUntil;
        private Vector2 mapCenter;
        private float mapScale;
        private RectTransform playerMarker, targetMarker, officerMarker;

        public bool ResultsVisible => resultOverlay != null && resultOverlay.activeSelf;
        public bool IsDialogueVisible => dialogue != null && dialogue.activeSelf;
        public bool IsMapVisible => mapCard != null && mapCard.gameObject.activeSelf;
        public Action<int> Choose;

        internal void TrackProductEffects(PlayerProductEffects effects) => productEffects = effects;

        private void OnEnable() => LocalizationManager.LanguageChanged += RefreshLocalizedLabels;

        private void OnDisable() => LocalizationManager.LanguageChanged -= RefreshLocalizedLabels;

        private void LateUpdate()
        {
            if (toastCard != null && toastCard.gameObject.activeSelf && Time.unscaledTime >= toastUntil)
                toastCard.gameObject.SetActive(false);
            UpdateProductEffects();
        }

        private void RefreshLocalizedLabels()
        {
            if (clockCaption == null) return;

            clockCaption.text = LocalizationManager.GetOrFallback("ui.city.mission_time", "TIME LEFT");
            policeCaption.text = LocalizationManager.GetOrFallback("ui.city.police", "POLICE");
            mapLabel.text = LocalizationManager.GetOrFallback("ui.city.old_quarter", "OLD QUARTER");
            saleAction.text = LocalizationManager.Get("ui.street_trade.offer");
            for (int i = 0; i < ProductEffectCount; i++)
            {
                string key = ((PlayerProductEffects.Effect)i).ToString().ToLowerInvariant();
                productEffectNames[i].text = LocalizationManager.Get("ui.product_effects." + key);
            }
        }

        public void Build(Bounds district, IReadOnlyList<Bounds> sidewalks, IReadOnlyList<Bounds> buildings)
        {
            LocalizationManager.EnsureLoaded();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasObject = new GameObject("Old Quarter HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            root = canvasObject.GetComponent<RectTransform>();
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("City Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));

            BuildObjective();
            BuildProductEffects();
            BuildStatus();
            BuildNavigation();
            BuildInteraction();
            BuildMap(district, sidewalks, buildings);
            BuildDialogue();
            RefreshLocalizedLabels();
        }

        private void BuildObjective()
        {
            var card = Panel("Contract", root, TopLeft, new Vector2(34, -32), new Vector2(418, 176), Ink);
            AccentRule(card, 418);
            chapter = Label(card, "Chapter", "", 15, Peach, new Vector2(20, -16), new Vector2(378, 22), TextAnchor.MiddleLeft, FontStyle.Bold);
            mission = Label(card, "Mission title", "", 29, White, new Vector2(20, -43), new Vector2(378, 36), TextAnchor.MiddleLeft, FontStyle.BoldAndItalic, minSize: 23);
            objective = Label(card, "Objective", "", 19, Muted, new Vector2(20, -84), new Vector2(378, 52), minSize: 16);
            balance = Label(card, "Cash", "", 17, Aqua, new Vector2(20, -141), new Vector2(378, 24), TextAnchor.MiddleLeft, FontStyle.Bold);
        }

        private void BuildProductEffects()
        {
            productEffectCard = Panel("Active product effects", root, TopLeft, new Vector2(34, -220), new Vector2(292, 0), Ink);
            for (int i = 0; i < ProductEffectCount; i++)
            {
                var row = Panel(((PlayerProductEffects.Effect)i).ToString(), productEffectCard, TopLeft,
                    Vector2.zero, new Vector2(260, ProductEffectRowHeight), Color.clear);
                productEffectRows[i] = row;
                productEffectNames[i] = Label(row, "Effect name", "", 16, White, new Vector2(0, -2), new Vector2(204, 23), TextAnchor.MiddleLeft, FontStyle.Bold);
                productEffectTimes[i] = Label(row, "Time remaining", "", 16, Aqua, new Vector2(204, -2), new Vector2(56, 23), TextAnchor.MiddleRight);
                var track = Panel("Effect meter", row, TopLeft, new Vector2(0, -28), new Vector2(260, 3), Raised);
                productEffectMeters[i] = Panel("Remaining", track, TopLeft, Vector2.zero, new Vector2(260, 3), Aqua);
            }
            productEffectCard.gameObject.SetActive(false);
        }

        private void UpdateProductEffects()
        {
            if (productEffectCard == null || productEffects == null) return;

            int visible = 0;
            for (int i = 0; i < ProductEffectCount; i++)
            {
                float remaining = productEffects.SecondsRemaining((PlayerProductEffects.Effect)i);
                var row = productEffectRows[i];
                row.gameObject.SetActive(remaining > 0);
                if (remaining <= 0) continue;

                row.anchoredPosition = new Vector2(16, -8 - visible * ProductEffectRowHeight);
                int seconds = Mathf.CeilToInt(remaining);
                productEffectTimes[i].text = $"{seconds / 60}:{seconds % 60:00}";
                productEffectMeters[i].SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 260 * remaining / PlayerProductEffects.Duration);
                visible++;
            }
            productEffectCard.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 16 + visible * ProductEffectRowHeight);
            productEffectCard.gameObject.SetActive(visible > 0);
        }

        private void BuildStatus()
        {
            clockCard = Panel("Mission clock", root, TopRight, new Vector2(-34, -32), new Vector2(174, 94), Ink);
            Panel("Clock rule", clockCard, TopLeft, Vector2.zero, new Vector2(174, 3), Peach);
            clockCaption = Label(clockCard, "Clock caption", "", 15, Muted, new Vector2(14, -12), new Vector2(146, 23), TextAnchor.MiddleCenter, FontStyle.Bold);
            clock = Label(clockCard, "Time remaining", "", 36, White, new Vector2(14, -36), new Vector2(146, 49), TextAnchor.MiddleCenter, FontStyle.Bold);

            policeCard = Panel("Police status", root, TopRight, new Vector2(-34, -32), new Vector2(174, 74), Ink);
            policeIndicator = Panel("Police indicator", policeCard, TopLeft, Vector2.zero, new Vector2(4, 74), Aqua).GetComponent<Image>();
            policeCaption = Label(policeCard, "Police caption", "", 14, Muted, new Vector2(16, -10), new Vector2(142, 21), TextAnchor.MiddleLeft, FontStyle.Bold);
            police = Label(policeCard, "Police state", "", 22, Aqua, new Vector2(16, -34), new Vector2(142, 29), TextAnchor.MiddleLeft, FontStyle.Bold);

            policeHintCard = Panel("Pursuit hint", root, TopRight, new Vector2(-34, -220), new Vector2(276, 78), Ink);
            policeHint = Label(policeHintCard, "Pursuit advice", "", 17, White, new Vector2(14, -13), new Vector2(248, 52),
                TextAnchor.MiddleLeft, minSize: 15);

            clockCard.gameObject.SetActive(false);
            policeHintCard.gameObject.SetActive(false);
        }

        private void BuildNavigation()
        {
            navigation = Panel("Destination guide", root, TopCenter, new Vector2(0, -32), new Vector2(360, 48), Ink);
            var arrow = Panel("Direction marker", navigation, TopLeft, new Vector2(7, -7), new Vector2(34, 34), Raised);
            directionArrow = Arrow(arrow, "Direction arrow", 19, Peach);
            destination = Label(navigation, "Destination", "", 18, White, new Vector2(52, -4), new Vector2(293, 40),
                TextAnchor.MiddleLeft, FontStyle.Bold, minSize: 15);
            navigation.gameObject.SetActive(false);

            toastCard = Panel("Notification", root, TopCenter, new Vector2(0, -93), new Vector2(566, 56), Ink);
            toast = Label(toastCard, "Notification text", "", 18, Peach, new Vector2(16, -4), new Vector2(534, 48),
                TextAnchor.MiddleCenter, FontStyle.Bold, minSize: 14);
            toastCard.gameObject.SetActive(false);
        }

        private void BuildInteraction()
        {
            interaction = Panel("Interaction", root, BottomCenter, new Vector2(0, 106), new Vector2(356, 68), Ink);
            var key = Panel("Interact key", interaction, TopLeft, new Vector2(12, -11), new Vector2(46, 46), Peach);
            Label(key, "Key binding", "E", 23, Ink, Vector2.zero, new Vector2(46, 46), TextAnchor.MiddleCenter, FontStyle.Bold);
            prompt = Label(interaction, "Interaction action", "", 21, White, new Vector2(72, -7), new Vector2(270, 30), TextAnchor.MiddleLeft, FontStyle.Bold);
            promptTarget = Label(interaction, "Interaction target", "", 15, Muted, new Vector2(72, -37), new Vector2(270, 24),
                TextAnchor.MiddleLeft, minSize: 13);
            interaction.gameObject.SetActive(false);

            saleInteraction = Panel("Street sale interaction", root, BottomCenter, new Vector2(0, 106), new Vector2(356, 68), Ink);
            var saleKey = Panel("Offer key", saleInteraction, TopLeft, new Vector2(12, -11), new Vector2(46, 46), Aqua);
            Label(saleKey, "Key binding", "G", 23, Ink, Vector2.zero, new Vector2(46, 46), TextAnchor.MiddleCenter, FontStyle.Bold);
            saleAction = Label(saleInteraction, "Offer action", "", 21, White, new Vector2(72, -7), new Vector2(270, 30),
                TextAnchor.MiddleLeft, FontStyle.Bold, minSize: 16);
            saleTarget = Label(saleInteraction, "Offer target", "", 15, Muted, new Vector2(72, -37), new Vector2(270, 24),
                TextAnchor.MiddleLeft, minSize: 13);
            saleInteraction.gameObject.SetActive(false);

            holdCard = Panel("Hold position", root, BottomCenter, new Vector2(0, 106), new Vector2(356, 68), Ink);
            holdText = Label(holdCard, "Hold instruction", "", 19, White, new Vector2(16, -7), new Vector2(324, 33), TextAnchor.MiddleLeft, FontStyle.Bold);
            var track = Panel("Hold track", holdCard, TopLeft, new Vector2(16, -47), new Vector2(324, 6), Raised);
            holdFill = Panel("Hold progress", track, TopLeft, Vector2.zero, new Vector2(0, 6), Aqua);
            holdCard.gameObject.SetActive(false);
        }

        private void BuildDialogue()
        {
            var dialogRect = Panel("Conversation", root, BottomCenter, new Vector2(0, 52), new Vector2(890, 323), Ink);
            AccentRule(dialogRect, 890);
            dialogue = dialogRect.gameObject;
            speaker = Label(dialogRect, "Speaker", "", 17, Peach, new Vector2(24, -17), new Vector2(842, 27), TextAnchor.MiddleLeft, FontStyle.Bold);
            body = Label(dialogRect, "Dialogue text", "", 22, White, new Vector2(24, -51), new Vector2(842, 108), minSize: 18);
            for (int i = 0; i < choices.Length; i++)
            {
                int index = i;
                var rect = Panel("Choice " + (i + 1), dialogRect, TopLeft, new Vector2(24, -165 - i * 48), new Vector2(842, 42), Raised);
                choices[i] = AddButton(rect, Raised, () => Choose?.Invoke(index));
                var key = Panel("Choice key", rect, TopLeft, new Vector2(6, -5), new Vector2(32, 32), Ink);
                Label(key, "Key binding", (i + 1).ToString(), 16, Peach, Vector2.zero, new Vector2(32, 32), TextAnchor.MiddleCenter, FontStyle.Bold);
                labels[i] = Label(rect, "Choice text", "", 18, White, new Vector2(52, -3), new Vector2(772, 36),
                    TextAnchor.MiddleLeft, minSize: 15);
            }
            dialogue.SetActive(false);
        }

        private void BuildMap(Bounds district, IReadOnlyList<Bounds> sidewalks, IReadOnlyList<Bounds> buildings)
        {
            mapCenter = new Vector2(district.center.x, district.center.z);
            mapScale = (MapSize - 14) / Mathf.Max(district.size.x, district.size.z);
            mapCard = Panel("District map", root, Vector2.zero, new Vector2(34, 32), new Vector2(224, 244), Ink);
            Panel("Map rule", mapCard, TopLeft, Vector2.zero, new Vector2(224, 3), Pink);
            mapLabel = Label(mapCard, "District name", "", 15, White, new Vector2(14, -8), new Vector2(165, 26), TextAnchor.MiddleLeft, FontStyle.Bold);
            Label(mapCard, "North", "N", 15, Aqua, new Vector2(182, -8), new Vector2(28, 26), TextAnchor.MiddleRight, FontStyle.Bold);

            var mapArea = Panel("District footprint", mapCard, TopLeft, new Vector2(14, -36), new Vector2(MapSize, MapSize), new Color32(31, 23, 44, 255));
            mapArea.gameObject.AddComponent<RectMask2D>();
            MapFootprint(mapArea, "Road network", district, new Color32(104, 83, 112, 255));
            foreach (Bounds sidewalk in sidewalks)
                MapFootprint(mapArea, "Sidewalk", sidewalk, new Color32(49, 40, 65, 255));
            foreach (Bounds building in buildings)
                MapFootprint(mapArea, "Building", building, new Color32(81, 65, 96, 255));

            officerMarker = Panel("Officer marker", mapArea, Center, Vector2.zero, new Vector2(9, 9), Danger);
            targetMarker = Panel("Objective marker", mapArea, Center, Vector2.zero, new Vector2(15, 15), Peach);
            targetMarker.localRotation = Quaternion.Euler(0, 0, 45);
            Panel("Objective marker center", targetMarker, Center, Vector2.zero, new Vector2(9, 9), Ink);
            playerMarker = Panel("Player marker", mapArea, Center, Vector2.zero, new Vector2(22, 22), Color.clear);
            Arrow(playerMarker, "Player heading", 17, White);
            mapCard.gameObject.SetActive(PlayerPrefs.GetInt(MinimapController.VisibilityPreferenceKey, 1) != 0);
        }

        private void MapFootprint(RectTransform map, string name, Bounds bounds, Color color)
        {
            Panel(name, map, Center, MapPoint(bounds.center), new Vector2(bounds.size.x, bounds.size.z) * mapScale, color);
        }

        private Vector2 MapPoint(Vector3 position) => (new Vector2(position.x, position.z) - mapCenter) * mapScale;

        private Vector2 MarkerPoint(Vector3 position)
        {
            Vector2 point = MapPoint(position);
            const float limit = MapSize / 2 - 11;
            return new Vector2(Mathf.Clamp(point.x, -limit, limit), Mathf.Clamp(point.y, -limit, limit));
        }

        public void UpdateMap(Vector3 player, Vector3 facing, Vector3? target, Vector3? officer)
        {
            playerMarker.anchoredPosition = MarkerPoint(player);
            playerMarker.localRotation = Quaternion.Euler(0, 0, -Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg);
            targetMarker.gameObject.SetActive(target.HasValue);
            officerMarker.gameObject.SetActive(officer.HasValue);
            if (target.HasValue) targetMarker.anchoredPosition = MarkerPoint(target.Value);
            if (officer.HasValue) officerMarker.anchoredPosition = MarkerPoint(officer.Value);
        }

        public void SetMapVisible(bool visible)
        {
            mapCard.gameObject.SetActive(visible);
            PlayerPrefs.SetInt(MinimapController.VisibilityPreferenceKey, visible ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void Refresh(ContractProgress current, CityPoliceStatus status, int reportSeconds = 0)
        {
            chapter.text = current.Finished ? "" : LocalizationManager.GetOrFallback("ui.city.chapter", "CHAPTER {current}/{total}",
                "current", (current.ContractIndex + 1).ToString(), "total", current.Story.contracts.Length.ToString());
            mission.text = current.Finished
                ? LocalizationManager.GetOrFallback("ui.city.free_roam", "FREE ROAM")
                : current.Current.title;
            objective.text = current.Finished
                ? LocalizationManager.GetOrFallback("ui.city.case_closed", "The case is closed. Explore the district freely.")
                : current.Step.objective;
            balance.text = LocalizationManager.Get("ui.street_trade.balance", "amount", current.Money.ToString("N0"));

            bool timed = current.Active && current.Current.timeLimit > 0;
            clockCard.gameObject.SetActive(timed);
            if (timed)
            {
                int remaining = Mathf.CeilToInt(Mathf.Max(0, current.Current.timeLimit - current.Elapsed));
                clock.text = $"{remaining / 60:00}:{remaining % 60:00}";
                clock.color = remaining <= 30 ? Danger : White;
            }

            bool reportPending = status == CityPoliceStatus.Clear && reportSeconds > 0;
            bool urgent = status != CityPoliceStatus.Clear || reportPending;
            policeCard.anchoredPosition = new Vector2(-34, timed ? -136 : -32);
            police.text = status switch
            {
                CityPoliceStatus.Pursuit => LocalizationManager.GetOrFallback("ui.city.police_pursuit", "Pursuit"),
                CityPoliceStatus.Searching => LocalizationManager.GetOrFallback("ui.city.police_searching", "Searching"),
                _ when reportPending => LocalizationManager.Get("ui.street_trade.report_countdown", "seconds", reportSeconds.ToString()),
                _ => LocalizationManager.GetOrFallback("ui.city.district_clear", "Clear")
            };
            Color statusColor = status == CityPoliceStatus.Pursuit ? Danger : urgent ? Peach : Aqua;
            police.color = policeIndicator.color = statusColor;
            policeHintCard.gameObject.SetActive(urgent);
            if (urgent)
            {
                policeHintCard.anchoredPosition = new Vector2(-34, timed ? -220 : -116);
                policeHint.text = reportPending
                    ? LocalizationManager.Get("ui.street_trade.report_hint")
                    : LocalizationManager.GetOrFallback("ui.notice.break_sight",
                        "Break sight around buildings. Officers can open doors during a chase.");
            }
        }

        public void UpdateNavigation(string target, float distance, float angle)
        {
            bool visible = !IsDialogueVisible && !string.IsNullOrEmpty(target);
            navigation.gameObject.SetActive(visible);
            if (!visible) return;

            destination.text = $"{target}  ·  {distance:0} m";
            directionArrow.localRotation = Quaternion.Euler(0, 0, -angle);
        }

        public void UpdateInteraction(string action, string actor, float held, float required, string customer)
        {
            bool holding = !IsDialogueVisible && required > 0 && held > 0;
            bool showInteraction = !IsDialogueVisible && !holding && !string.IsNullOrEmpty(action);
            bool showSale = !IsDialogueVisible && !holding && !string.IsNullOrEmpty(customer);
            holdCard.gameObject.SetActive(holding);
            interaction.gameObject.SetActive(showInteraction);
            saleInteraction.gameObject.SetActive(showSale);
            bool sideBySide = showInteraction && showSale;
            interaction.anchoredPosition = new Vector2(sideBySide ? -184 : 0, 106);
            saleInteraction.anchoredPosition = new Vector2(sideBySide ? 184 : 0, 106);
            if (showSale) saleTarget.text = customer;
            if (holding)
            {
                holdText.text = LocalizationManager.GetOrFallback("ui.city.hold_position", "Stay in position · {seconds} s",
                    "seconds", Mathf.CeilToInt(Mathf.Max(0, required - held)).ToString());
                holdFill.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 324 * Mathf.Clamp01(held / required));
            }
            else
            {
                prompt.text = action;
                promptTarget.text = actor;
            }
        }

        public void Notify(string text)
        {
            toast.text = text;
            toastUntil = Time.unscaledTime + 5f;
            toastCard.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        public void Show(Conversation conversation)
        {
            dialogue.SetActive(conversation != null);
            if (conversation == null) return;

            navigation.gameObject.SetActive(false);
            interaction.gameObject.SetActive(false);
            saleInteraction.gameObject.SetActive(false);
            holdCard.gameObject.SetActive(false);
            speaker.text = conversation.speaker;
            body.text = conversation.text;
            for (int i = 0; i < choices.Length; i++)
            {
                bool visible = conversation.choices != null && i < conversation.choices.Length;
                choices[i].gameObject.SetActive(visible);
                if (visible) labels[i].text = conversation.choices[i].text;
            }
        }

        public void HideResults() { if (resultOverlay != null) resultOverlay.SetActive(false); }

        public void ShowResults(ContractProgress current, int best, Action next, Action freeRoam, Action replay, Action quit)
        {
            if (resultOverlay != null) Destroy(resultOverlay);
            var cover = Panel("Campaign results", root, Vector2.zero, Vector2.zero, Vector2.zero, new Color32(20, 12, 29, 247));
            cover.anchorMax = Vector2.one;
            cover.offsetMin = cover.offsetMax = Vector2.zero;
            cover.GetComponent<Image>().raycastTarget = true;
            resultOverlay = cover.gameObject;
            float resultRowsHeight = current.Results.Count * 40f;
            var card = Panel("Score card", cover, Center, Vector2.zero, new Vector2(1000, 500 + resultRowsHeight), Ink);
            AccentRule(card, 1000);
            bool final = current.Finished;
            string resultTitle = final
                ? LocalizationManager.GetOrFallback("ui.city.case_closed_title", "CASE CLOSED / CAMPAIGN COMPLETE")
                : LocalizationManager.GetOrFallback("ui.city.level_complete", "LEVEL {level} COMPLETE", "level", current.Results.Count.ToString());
            Label(card, "Result title", resultTitle, 17, Peach, new Vector2(40, -26), new Vector2(920, 30), TextAnchor.MiddleLeft, FontStyle.Bold);
            string points = final
                ? LocalizationManager.GetOrFallback("ui.city.points_rank", "{points} POINTS  /  RANK {rank}",
                    "points", current.Score.ToString("N0"), "rank", current.Rank)
                : LocalizationManager.GetOrFallback("ui.city.points_gained", "+{points} POINTS",
                    "points", current.Results[current.Results.Count - 1].score.ToString("N0"));
            Label(card, "Points", points, 48, White, new Vector2(40, -65), new Vector2(920, 68), TextAnchor.MiddleLeft, FontStyle.BoldAndItalic);
            string summary = LocalizationManager.GetOrFallback("ui.city.summary",
                "{completed}/{total} levels   /   {retries} retries   /   {mistakes} mistakes   /   {minutes}m {seconds}s",
                "completed", current.Results.Count.ToString(), "total", current.Story.contracts.Length.ToString(),
                "retries", current.Failures.ToString(), "mistakes", current.Mistakes.ToString(),
                "minutes", Mathf.FloorToInt(current.TotalTime / 60).ToString(), "seconds", Mathf.FloorToInt(current.TotalTime % 60).ToString());
            Label(card, "Result summary", summary, 18, Muted, new Vector2(40, -141), new Vector2(920, 29));
            Label(card, "Campaign stats", CampaignStats(current, showScore: !final), 16, Aqua,
                new Vector2(40, -171), new Vector2(920, 28), TextAnchor.MiddleLeft, FontStyle.Bold);

            var table = Panel("Score breakdown", card, TopLeft, new Vector2(40, -212), new Vector2(920, 44 + resultRowsHeight), new Color32(38, 25, 47, 255));
            Label(table, "Level column", LocalizationManager.GetOrFallback("ui.city.level", "LEVEL"), 14, Peach,
                new Vector2(14, -4), new Vector2(350, 30), TextAnchor.MiddleLeft, FontStyle.Bold);
            string[] columns = {
                LocalizationManager.GetOrFallback("ui.city.base", "BASE"),
                LocalizationManager.GetOrFallback("ui.city.time", "TIME"),
                LocalizationManager.GetOrFallback("ui.city.clean", "CLEAN"),
                LocalizationManager.GetOrFallback("ui.city.penalty", "PENALTY"),
                LocalizationManager.GetOrFallback("ui.city.total", "TOTAL")
            };
            for (int col = 0; col < columns.Length; col++)
                Label(table, columns[col], columns[col], 14, Peach, new Vector2(380 + col * 105, -4), new Vector2(100, 30),
                    TextAnchor.MiddleRight, FontStyle.Bold);
            for (int row = 0; row < current.Results.Count; row++)
            {
                var result = current.Results[row];
                var line = Panel("Level result", table, TopLeft, new Vector2(0, -38 - row * 40), new Vector2(920, 39),
                    row % 2 == 0 ? Raised : Color.clear);
                Label(line, "Level title", current.Story.contracts[row].title, 17, White,
                    new Vector2(14, -3), new Vector2(360, 33), TextAnchor.MiddleLeft);
                string[] values = { result.baseScore.ToString(), "+" + result.timeBonus, "+" + result.stealthBonus, "-" + result.penalties, result.score.ToString() };
                for (int col = 0; col < values.Length; col++)
                    Label(line, columns[col], values[col], 17, col == 4 ? Aqua : Muted, new Vector2(380 + col * 105, -3), new Vector2(100, 33),
                        TextAnchor.MiddleRight, col == 4 ? FontStyle.Bold : FontStyle.Normal);
            }
            string footer = final
                ? LocalizationManager.GetOrFallback("ui.city.best_score",
                    "BEST SCORE: {score}  /  Your evidence reached the public. The district is safe.", "score", best.ToString("N0"))
                : LocalizationManager.GetOrFallback("ui.city.next_chapter_title", "Next chapter · {title}", "title", current.Current.title);
            Label(card, "Campaign footer", footer, 18, Peach, new Vector2(40, -264 - resultRowsHeight), new Vector2(920, 46),
                TextAnchor.MiddleLeft, FontStyle.Bold);
            if (current.Results.Count > 0 && current.Results[current.Results.Count - 1].measurementsVersion > 0)
            {
                var last = current.Results[current.Results.Count - 1];
                Label(card, "Mission performance", LocalizationManager.GetOrFallback("ui.city.performance",
                    "Last mission: {efficiency}% efficiency / {navigation}s navigating / {detections} detections",
                    "efficiency", (last.efficiency * 100).ToString("0"), "navigation", last.navigationSeconds.ToString("0"),
                    "detections", last.detections.ToString()), 15, Muted, new Vector2(40, -314 - resultRowsHeight), new Vector2(920, 26));
            }
            Label(card, "Score rules", LocalizationManager.GetOrFallback("ui.city.time_bonus_rules",
                "Time bonus: 2 points per second remaining. Clean run: +300. Retry: -200. Wrong answer: -75."), 15,
                Muted, new Vector2(40, -342 - resultRowsHeight), new Vector2(920, 43));
            float actionsY = -414 - resultRowsHeight;
            if (final)
            {
                ResultButton(card, LocalizationManager.GetOrFallback("ui.city.continue_free_roam", "CONTINUE FREE ROAM"), new Vector2(40, actionsY), 300, freeRoam, true);
                ResultButton(card, LocalizationManager.GetOrFallback("ui.city.new_campaign", "NEW CAMPAIGN"), new Vector2(350, actionsY), 300, replay);
                ResultButton(card, LocalizationManager.GetOrFallback("ui.city.quit_game", "QUIT GAME"), new Vector2(660, actionsY), 300, quit);
            }
            else
            {
                ResultButton(card, LocalizationManager.GetOrFallback("ui.city.next_chapter_enter", "NEXT CHAPTER  [ENTER]"), new Vector2(40, actionsY), 920, next, true);
            }
        }

        public static string CampaignStats(ContractProgress current, bool showScore = true)
        {
            string score = LocalizationManager.GetOrFallback("ui.city.score", "SCORE");
            string reputation = LocalizationManager.GetOrFallback("ui.city.rep", "REPUTATION");
            string stats = $"{reputation}  {current.Reputation}   ·   ${current.Money:N0}";
            return showScore ? $"{score}  {current.Score:N0}   ·   {stats}" : stats;
        }

        private void ResultButton(Transform parent, string text, Vector2 position, float width, Action action, bool primary = false)
        {
            Color fill = primary ? Peach : Raised;
            var rect = Panel(text, parent, TopLeft, position, new Vector2(width, 56), fill);
            AddButton(rect, fill, action);
            Label(rect, "Button label", text, 18, primary ? Ink : White,
                new Vector2(8, -4), new Vector2(width - 16, 48), TextAnchor.MiddleCenter, FontStyle.Bold, minSize: 14);
        }

        private static Button AddButton(RectTransform rect, Color fill, Action action)
        {
            var image = rect.GetComponent<Image>();
            image.raycastTarget = true;
            image.color = Color.white;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = fill;
            colors.highlightedColor = Color.Lerp(fill, White, 0.16f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = Color.Lerp(fill, Ink, 0.2f);
            button.colors = colors;
            image.canvasRenderer.SetColor(fill);
            button.onClick.AddListener(() => action?.Invoke());
            return button;
        }

        private static void AccentRule(Transform parent, float width)
        {
            Panel("Peach accent", parent, TopLeft, Vector2.zero, new Vector2(width * 0.65f, 3), Peach);
            Panel("Pink accent", parent, TopLeft, new Vector2(width * 0.65f, 0), new Vector2(width * 0.35f, 3), Pink);
        }

        private RectTransform Arrow(RectTransform parent, string name, int size, Color color)
        {
            Text arrow = Label(parent, name, "\u25B2", size, color, Vector2.zero, Vector2.one * (size + 5),
                TextAnchor.MiddleCenter, FontStyle.Bold);
            var rect = arrow.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Center;
            rect.anchoredPosition = Vector2.zero;
            var shadow = arrow.gameObject.AddComponent<Outline>();
            shadow.effectColor = Ink;
            shadow.effectDistance = new Vector2(1, -1);
            return rect;
        }

        private static RectTransform Panel(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = Place(go, parent, anchor, position, size);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private Text Label(Transform parent, string name, string text, int size, Color color, Vector2 position, Vector2 dimensions,
            TextAnchor alignment = TextAnchor.UpperLeft, FontStyle style = FontStyle.Normal, int minSize = 0)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            Place(go, parent, TopLeft, position, dimensions);
            var label = go.GetComponent<Text>();
            label.font = font;
            label.fontSize = size;
            label.fontStyle = style;
            label.color = color;
            label.text = text;
            label.alignment = alignment;
            if (minSize > 0)
            {
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = minSize;
                label.resizeTextMaxSize = size;
            }
            label.raycastTarget = false;
            return label;
        }

        private static RectTransform Place(GameObject go, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }
    }
}
