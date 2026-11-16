using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace HighStakes.City
{
    public sealed class CityHud : MonoBehaviour
    {
        private static readonly Color Ink = new Color32(12, 22, 29, 245);
        private static readonly Color Muted = new Color32(160, 181, 183, 255);
        private static readonly Color Mint = new Color32(119, 233, 196, 255);
        private Font font;
        private RectTransform root;
        private Text objective, mission, status, prompt, direction, toast, body, speaker;
        private GameObject dialogue;
        private GameObject resultOverlay;
        public bool ResultsVisible => resultOverlay != null && resultOverlay.activeSelf;
        private readonly Button[] choices = new Button[3];
        private readonly Text[] labels = new Text[3];
        private float toastUntil;
        private RectTransform playerDot, targetDot, officerDot;
        public Action<int> Choose;

        public void Build()
        {
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
            scaler.matchWidthOrHeight = 0.5f;
            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("City Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var brand = Panel("Brand", root, new Vector2(0, 1), new Vector2(30, -28), new Vector2(350, 76), Ink);
            Label(brand, "HIGHSTAKES", 26, Color.white, new Vector2(18, -8), new Vector2(310, 36));
            Label(brand, "OLD QUARTER  /  CONTRACTS", 11, Mint, new Vector2(19, -48), new Vector2(310, 18));
            var card = Panel("Contract", root, new Vector2(0, 1), new Vector2(30, -120), new Vector2(350, 176), Ink);
            Panel("Accent", card, new Vector2(0, 1), Vector2.zero, new Vector2(3, 176), Mint);
            Label(card, "CURRENT CONTRACT", 11, Muted, new Vector2(20, -16), new Vector2(310, 20));
            mission = Label(card, "", 21, Color.white, new Vector2(20, -45), new Vector2(310, 38));
            objective = Label(card, "", 16, Muted, new Vector2(20, -90), new Vector2(310, 72));
            var statusPanel = Panel("Status", root, new Vector2(1, 1), new Vector2(-30, -28), new Vector2(290, 154), Ink);
            status = Label(statusPanel, "", 17, Mint, new Vector2(20, -16), new Vector2(252, 126));
            status.supportRichText = true;
            direction = Label(root, "", 17, Mint, new Vector2(-240, -34), new Vector2(480, 70), new Vector2(0.5f, 1));
            direction.alignment = TextAnchor.MiddleCenter;
            prompt = Label(root, "", 18, Color.white, new Vector2(-350, 112), new Vector2(700, 40), new Vector2(0.5f, 0));
            prompt.alignment = TextAnchor.MiddleCenter;
            toast = Label(root, "", 20, Mint, new Vector2(-400, -132), new Vector2(800, 70), new Vector2(0.5f, 1));
            toast.alignment = TextAnchor.MiddleCenter;
            var controls = Label(root, "WASD  MOVE     SHIFT  RUN     SPACE  JUMP     V  CAMERA     E  INTERACT     ESC  MENU", 12,
                Muted, new Vector2(-530, 35), new Vector2(1060, 28), new Vector2(0.5f, 0));
            controls.alignment = TextAnchor.MiddleCenter;

            var dialogRect = Panel("Conversation", root, new Vector2(0.5f, 0), new Vector2(0, 70), new Vector2(980, 310), Ink);
            dialogRect.pivot = new Vector2(0.5f, 0);
            dialogue = dialogRect.gameObject;
            speaker = Label(dialogRect, "", 13, Mint, new Vector2(28, -20), new Vector2(920, 26));
            body = Label(dialogRect, "", 20, Color.white, new Vector2(28, -56), new Vector2(924, 100));
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                var rect = Panel("Choice " + i, dialogRect, new Vector2(0, 1), new Vector2(28, -170 - i * 42), new Vector2(924, 36), new Color32(28, 48, 54, 255));
                var button = rect.gameObject.AddComponent<Button>();
                button.targetGraphic = rect.GetComponent<Image>();
                var colors = button.colors;
                colors.highlightedColor = new Color(0.5f, 0.9f, 0.8f);
                colors.pressedColor = new Color(0.3f, 0.7f, 0.6f);
                button.colors = colors;
                button.onClick.AddListener(() => Choose?.Invoke(index));
                choices[i] = button;
                labels[i] = Label(rect, "", 16, Color.white, new Vector2(12, -4), new Vector2(895, 28));
            }
            dialogue.SetActive(false);
            var map = Panel("District map", root, new Vector2(1, 0), new Vector2(-30, 70), new Vector2(196, 220), Ink);
            Label(map, "OLD QUARTER", 10, Muted, new Vector2(15, -10), new Vector2(170, 20));
            var mapArea = Panel("Streets", map, new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(168, 168), new Color32(31, 45, 51, 255));
            mapArea.pivot = Vector2.one * 0.5f;
            foreach (int x in new[] { -1, 1 })
                foreach (int y in new[] { -1, 1 })
                {
                    var block = Panel("Block", mapArea, Vector2.one * 0.5f, new Vector2(x * 43, y * 43), new Vector2(60, 60), new Color32(63, 77, 76, 255));
                    block.pivot = Vector2.one * 0.5f;
                }
            playerDot = MapDot(mapArea, "You", Color.white, 7);
            targetDot = MapDot(mapArea, "Objective", Mint, 9);
            officerDot = MapDot(mapArea, "Officer", new Color32(248, 127, 110, 255), 7);
        }

        public void Refresh(ContractProgress progress, float distance, string target, string police, string interaction, string room)
        {
            mission.text = progress.Finished ? "Campaign complete" : progress.Current.title;
            objective.text = progress.Finished ? "The case is closed." : progress.Step.objective;
            string clock = progress.Active ? $"\n{Mathf.CeilToInt(Mathf.Max(0, progress.Current.timeLimit - progress.Elapsed))}s remaining" : "";
            status.text = $"LEVEL {Mathf.Min(progress.ContractIndex + 1, progress.Story.contracts.Length)}/{progress.Story.contracts.Length}    SCORE {progress.Score}\n${progress.Money:0000}    /    REP {progress.Reputation}\n<color=#BBCACC>{police}</color>{clock}";
            direction.text = string.IsNullOrEmpty(target) ? "" : $"{target.ToUpperInvariant()}\n{distance:0} m  /  {room}";
            direction.fontSize = 14;
            prompt.text = dialogue.activeSelf ? "" : interaction;
            if (Time.unscaledTime > toastUntil) toast.text = "";
        }

        public void Notify(string text) { toast.text = text; toastUntil = Time.unscaledTime + 5f; }

        public void Show(Conversation conversation)
        {
            dialogue.SetActive(conversation != null);
            if (conversation == null) return;
            speaker.text = conversation.speaker.ToUpperInvariant();
            body.text = conversation.text;
            for (int i = 0; i < choices.Length; i++)
            {
                bool visible = conversation.choices != null && i < conversation.choices.Length;
                choices[i].gameObject.SetActive(visible);
                if (visible) labels[i].text = $"{i + 1}   {conversation.choices[i].text}";
            }
        }

        public void HideResults() { if (resultOverlay != null) resultOverlay.SetActive(false); }

        public void ShowResults(ContractProgress progress, int best, Action next, Action replay, Action quit)
        {
            if (resultOverlay != null) Destroy(resultOverlay);
            var cover = Panel("Campaign results", root, Vector2.zero, Vector2.zero, Vector2.zero, new Color32(6, 14, 20, 250));
            cover.anchorMin = Vector2.zero; cover.anchorMax = Vector2.one;
            cover.offsetMin = cover.offsetMax = Vector2.zero;
            resultOverlay = cover.gameObject;
            var card = Panel("Score card", cover, Vector2.one * 0.5f, Vector2.zero, new Vector2(1000, 720), Ink);
            card.pivot = Vector2.one * 0.5f;
            bool final = progress.Finished;
            Label(card, final ? "CASE CLOSED / CAMPAIGN COMPLETE" : $"LEVEL {progress.Results.Count} COMPLETE", 16, Mint,
                new Vector2(40, -26), new Vector2(920, 30));
            Label(card, final ? $"{progress.Score:N0} POINTS  /  RANK {progress.Rank}" : $"+{progress.Results[progress.Results.Count - 1].score:N0} POINTS", 38,
                Color.white, new Vector2(40, -76), new Vector2(920, 60));
            string summary = $"{progress.Results.Count}/{progress.Story.contracts.Length} levels   /   {progress.Failures} retries   /   {progress.Mistakes} mistakes   /   {Mathf.FloorToInt(progress.TotalTime / 60)}m {Mathf.FloorToInt(progress.TotalTime % 60)}s";
            Label(card, summary, 18, Muted, new Vector2(40, -145), new Vector2(920, 32));
            Label(card, "LEVEL", 14, Mint, new Vector2(40, -207), new Vector2(350, 30));
            string[] columns = { "BASE", "TIME", "CLEAN", "PENALTY", "TOTAL" };
            for (int col = 0; col < columns.Length; col++)
                Label(card, columns[col], 13, Mint, new Vector2(420 + col * 106, -207), new Vector2(100, 30)).alignment = TextAnchor.MiddleRight;
            int row = 0;
            foreach (var result in progress.Results)
            {
                Label(card, result.title, 17, Color.white, new Vector2(40, -248 - row * 43), new Vector2(360, 32));
                string[] values = { result.baseScore.ToString(), "+" + result.timeBonus, "+" + result.stealthBonus, "-" + result.penalties, result.score.ToString() };
                for (int col = 0; col < values.Length; col++)
                    Label(card, values[col], 16, col == 4 ? Mint : Muted, new Vector2(420 + col * 106, -248 - row * 43), new Vector2(100, 32)).alignment = TextAnchor.MiddleRight;
                row++;
            }
            string footer = final ? $"BEST SCORE: {best:N0}  /  Your evidence reached the public. The district is safe."
                : $"CAMPAIGN SCORE: {progress.Score:N0}  /  Next: {progress.Current.title}";
            Label(card, footer, 17, Mint, new Vector2(40, -492), new Vector2(920, 60));
            Label(card, "Time bonus: 2 points per second remaining. Clean run: +300. Retry: -200. Wrong answer: -75.", 13,
                Muted, new Vector2(40, -562), new Vector2(920, 40));
            if (final)
            {
                ResultButton(card, "NEW CAMPAIGN", new Vector2(40, -632), new Vector2(440, 50), replay);
                ResultButton(card, "QUIT GAME", new Vector2(520, -632), new Vector2(440, 50), quit);
            }
            else ResultButton(card, "CONTINUE TO NEXT LEVEL  [ ENTER ]", new Vector2(40, -632), new Vector2(920, 50), next);
            prompt.text = ""; direction.text = "";
        }
        private void ResultButton(Transform parent, string text, Vector2 position, Vector2 size, Action action)
        {
            var rect = Panel(text, parent, new Vector2(0, 1), position, size, new Color32(30, 81, 74, 255));
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>();
            button.onClick.AddListener(() => action?.Invoke());
            var textLabel = Label(rect, text, 18, Color.white, new Vector2(8, -8), size - new Vector2(16, 16));
            textLabel.alignment = TextAnchor.MiddleCenter;
        }

        public void UpdateMap(Vector3 player, Vector3? target, Vector3? officer)
        {
            playerDot.anchoredPosition = new Vector2(player.x, player.z) * 1.7f;
            targetDot.gameObject.SetActive(target.HasValue);
            officerDot.gameObject.SetActive(officer.HasValue);
            if (target.HasValue) targetDot.anchoredPosition = new Vector2(target.Value.x, target.Value.z) * 1.7f;
            if (officer.HasValue) officerDot.anchoredPosition = new Vector2(officer.Value.x, officer.Value.z) * 1.7f;
        }
        private RectTransform MapDot(Transform parent, string name, Color color, float size)
        {
            var dot = Panel(name, parent, Vector2.one * 0.5f, Vector2.zero, Vector2.one * size, color);
            dot.pivot = Vector2.one * 0.5f;
            dot.GetComponent<Image>().raycastTarget = false;
            return dot;
        }

        private RectTransform Panel(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(anchor.x == 1 ? 1 : 0, anchor.y == 0 ? 0 : 1);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            go.GetComponent<Image>().color = color;
            return rect;
        }
        private Text Label(Transform parent, string text, int size, Color color, Vector2 position, Vector2 dimensions, Vector2? anchor = null)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor ?? new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;
            var label = go.GetComponent<Text>();
            label.font = font; label.fontSize = size; label.color = color; label.text = text;
            label.raycastTarget = false;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }
    }
}
