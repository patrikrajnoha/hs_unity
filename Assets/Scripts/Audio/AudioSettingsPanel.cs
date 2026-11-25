using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>The same audio controls in the main menu and pause settings.</summary>
public sealed class AudioSettingsPanel : MonoBehaviour
{
    private readonly Slider[] sliders = new Slider[4];
    private readonly TMP_Text[] labels = new TMP_Text[4];
    private readonly TMP_Text[] values = new TMP_Text[4];
    private TMP_Text muteLabel, defaultsLabel;
    private float previewAt;
    public static AudioSettingsPanel Create(Transform parent, float top)
    {
        var root = Rect("Audio settings", parent, new Vector2(0, top), new Vector2(400, 290));
        var panel = root.gameObject.AddComponent<AudioSettingsPanel>();
        panel.Build(); return panel;
    }
    private void OnEnable()
    { GameAudio.SettingsChanged += Refresh; LocalizationManager.LanguageChanged += Refresh; Refresh(); }
    private void OnDisable()
    {
        GameAudio.SettingsChanged -= Refresh; LocalizationManager.LanguageChanged -= Refresh;
        GameAudio.SaveIfReady();
    }
    private void Build()
    {
        for (int i = 0; i < 4; i++)
        {
            int channel = i;
            labels[i] = Label("Channel " + i, transform, new Vector2(0, -i * 53), new Vector2(330, 22));
            values[i] = Label("Value " + i, transform, new Vector2(340, -i * 53), new Vector2(60, 22));
            values[i].alignment = TextAlignmentOptions.Right;
            var row = Rect("Volume " + (AudioBus)i, transform, new Vector2(0, -i * 53 - 27), new Vector2(400, 18));
            var background = row.gameObject.AddComponent<Image>(); background.color = new Color32(47, 62, 84, 255);
            var fillArea = Rect("Fill area", row, Vector2.zero, new Vector2(400, 18));
            var fill = Rect("Fill", fillArea, Vector2.zero, new Vector2(400, 18));
            fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one;
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            fill.gameObject.AddComponent<Image>().color = new Color32(96, 213, 232, 255);
            var handleArea = Rect("Handle area", row, new Vector2(8, 0), new Vector2(384, 18));
            var handle = Rect("Handle", handleArea, Vector2.zero, new Vector2(18, 28));
            handle.anchorMin = handle.anchorMax = new Vector2(0, .5f); handle.pivot = new Vector2(.5f, .5f);
            var handleImage = handle.gameObject.AddComponent<Image>(); handleImage.color = Color.white;
            var slider = row.gameObject.AddComponent<Slider>();
            slider.fillRect = fill; slider.handleRect = handle; slider.targetGraphic = handleImage;
            slider.minValue = 0; slider.maxValue = 1; slider.direction = Slider.Direction.LeftToRight;
            slider.onValueChanged.AddListener(value => {
                GameAudio.Instance.SetVolume((AudioBus)channel, value);
                if ((channel == 0 || channel == 2) && Time.unscaledTime >= previewAt)
                { previewAt = Time.unscaledTime + .18f; GameAudio.Play(AudioCue.Click); }
            });
            sliders[i] = slider;
        }
        muteLabel = Button("Mute audio", new Vector2(0, -230), () => {
            GameAudio.Instance.SetMuted(!GameAudio.Instance.Muted); GameAudio.Play(AudioCue.Click);
        });
        defaultsLabel = Button("Audio defaults", new Vector2(205, -230), () => {
            GameAudio.Instance.RestoreDefaults(); GameAudio.Play(AudioCue.Click);
        });
        Refresh();
    }
    private TMP_Text Button(string name, Vector2 position, UnityEngine.Events.UnityAction action)
    {
        var root = Rect(name, transform, position, new Vector2(195, 42));
        var image = root.gameObject.AddComponent<Image>(); image.color = new Color32(53, 75, 101, 255);
        var button = root.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
        button.onClick.AddListener(action);
        var label = Label("Label", root, Vector2.zero, new Vector2(195, 42)); label.alignment = TextAlignmentOptions.Center;
        return label;
    }
    private void Refresh()
    {
        var audio = GameAudio.Instance;
        string[] fallback = { "Master", "Music", "Sound effects", "City ambience" };
        for (int i = 0; i < sliders.Length; i++)
        {
            if (sliders[i] == null) continue;
            labels[i].text = LocalizationManager.GetOrFallback("ui.audio." + ((AudioBus)i).ToString().ToLowerInvariant(), fallback[i]);
            sliders[i].SetValueWithoutNotify(audio.GetVolume((AudioBus)i));
            values[i].text = Mathf.RoundToInt(audio.GetVolume((AudioBus)i) * 100) + "%";
        }
        if (muteLabel != null) muteLabel.text = LocalizationManager.GetOrFallback(audio.Muted ? "ui.audio.unmute" : "ui.audio.mute", audio.Muted ? "UNMUTE" : "MUTE ALL");
        if (defaultsLabel != null) defaultsLabel.text = LocalizationManager.GetOrFallback("ui.audio.defaults", "RESET AUDIO");
    }
    private static RectTransform Rect(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        var root = new GameObject(name, typeof(RectTransform)); root.transform.SetParent(parent, false);
        var rect = root.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = pos; rect.sizeDelta = size;
        if (name == "Audio settings") { rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1); rect.pivot = new Vector2(.5f, 1); }
        return rect;
    }
    private static TMP_Text Label(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        var root = Rect(name, parent, pos, size); var label = root.gameObject.AddComponent<TextMeshProUGUI>();
        label.fontSize = 17; label.color = Color.white; label.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null) label.font = TMP_Settings.defaultFontAsset;
        return label;
    }
}
