using System;
using System.Collections.Generic;
using HighStakes.City;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum AudioBus { Master, Music, Effects, Ambience }
public enum AudioCue { Click, Pickup, Objective, Success, Failure, Warning, Door }

/// <summary>Scene-independent audio, with owned sources and saved per-channel settings.</summary>
public sealed class GameAudio : MonoBehaviour
{
    private const string Prefix = "HighStakes.Audio.";
    private static GameAudio instance;
    private readonly float[] levels = { .8f, .55f, .8f, .65f };
    private readonly Dictionary<AudioCue, AudioClip> cues = new Dictionary<AudioCue, AudioClip>();
    private AudioSource calm, chase, air, ui, feet;
    private AudioSource[] effects;
    private AudioClip outsideStep, insideStep;
    private PlayerController player;
    private CharacterController controller;
    private Vector3 lastPosition;
    private float travelled, blend, lookupAt, saveAt;
    private int effectSlot;
    private bool muted, dirty, gameplayPaused, wasPursued;
    public static event Action SettingsChanged;
    public static GameAudio Instance => Ensure();
    public bool Muted => muted;
    public float MusicBlend => blend;
    public bool SourcesReady => calm != null && calm.clip != null && chase.clip != null && air.clip != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; SettingsChanged = null; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install() { Ensure(); }
    private static GameAudio Ensure()
    {
        if (instance == null) new GameObject("HighStakes Audio").AddComponent<GameAudio>();
        return instance;
    }
    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
        ReloadSettings();
        calm = Loop("District music", "quarter_theme");
        chase = Loop("Pursuit music", "pursuit_theme");
        air = Loop("City ambience", "city_air");
        ui = Source("Interface");
        feet = Source("Footsteps");
        effects = new AudioSource[12];
        for (int i = 0; i < effects.Length; i++) effects[i] = Source("Effect " + i);
        outsideStep = Load("step_stone"); insideStep = Load("step_indoor");
        string[] names = { "ui_click", "pickup", "objective", "success", "failure", "warning", "door" };
        for (int i = 0; i < names.Length; i++) cues[(AudioCue)i] = Load(names[i]);
        SceneManager.sceneLoaded += SceneChanged;
        ApplyMix();
    }
    private AudioClip Load(string name)
    {
        var clip = Resources.Load<AudioClip>("Audio/" + name);
        if (clip == null) Debug.LogWarning("Audio asset missing: " + name);
        return clip;
    }
    private AudioSource Source(string name)
    {
        var go = new GameObject(name); go.transform.SetParent(transform, false);
        var source = go.AddComponent<AudioSource>();
        source.playOnAwake = false; source.spatialBlend = 0; source.volume = 0;
        return source;
    }
    private AudioSource Loop(string name, string resource)
    {
        var source = Source(name); source.clip = Load(resource); source.loop = true;
        if (source.clip != null) source.Play();
        return source;
    }
    private void SceneChanged(Scene scene, LoadSceneMode mode)
    {
        player = null; controller = null; travelled = 0; lookupAt = 0; wasPursued = false;
        if (effects != null) foreach (var source in effects) source.Stop();
        if (feet != null) feet.Stop();
    }
    private void Update()
    {
        if (dirty && Time.unscaledTime >= saveAt) SaveSettings();
        bool inCity = SceneManager.GetActiveScene().name == "OldQuarter";
        if (inCity && player == null && Time.unscaledTime >= lookupAt)
        {
            lookupAt = Time.unscaledTime + 1;
            player = FindAnyObjectByType<PlayerController>();
            if (player != null) { controller = player.GetComponent<CharacterController>(); lastPosition = player.transform.position; }
        }
        bool pursued = false;
        if (inCity) foreach (var police in PoliceHeatRegistry.ActivePolice)
            if (police != null && police.isChasing) { pursued = true; break; }
        if (pursued && !wasPursued) Play(AudioCue.Warning);
        wasPursued = pursued;
        blend = Mathf.MoveTowards(blend, pursued ? 1 : 0, Time.unscaledDeltaTime / 2f);
        bool paused = Time.timeScale == 0;
        if (paused != gameplayPaused)
        {
            gameplayPaused = paused;
            foreach (var source in effects) { if (paused) source.Pause(); else source.UnPause(); }
            if (paused) feet.Pause(); else feet.UnPause();
        }
        ApplyMix();
        if (!inCity || player == null) return;
        Vector3 position = player.transform.position;
        float distance = Vector3.ProjectOnPlane(position - lastPosition, Vector3.up).magnitude;
        lastPosition = position;
        if (paused || player.IsDialogueInputLocked || controller == null || !controller.isGrounded || distance > 1.5f)
        { travelled = 0; return; }
        travelled += distance;
        if (travelled >= 1.75f)
        {
            travelled %= 1.75f;
            feet.clip = CityInterior.RoomAt(position).Contains("Streets") ? outsideStep : insideStep;
            feet.pitch = UnityEngine.Random.Range(.93f, 1.07f);
            feet.Play();
        }
    }
    private void ApplyMix()
    {
        float master = muted ? 0 : levels[0];
        float music = master * levels[1] * (Time.timeScale == 0 ? .45f : 1f);
        if (calm != null) calm.volume = music * (1 - blend) * .65f;
        if (chase != null) chase.volume = music * blend * .65f;
        bool inCity = SceneManager.GetActiveScene().name == "OldQuarter";
        if (air != null) air.volume = inCity && Time.timeScale > 0 ? master * levels[3] * .45f : 0;
        if (ui != null) ui.volume = master * levels[2] * .65f;
        if (feet != null) feet.volume = master * levels[2] * .65f;
        if (effects != null) foreach (var source in effects) source.volume = master * levels[2];
    }
    public float GetVolume(AudioBus bus) => levels[(int)bus];
    public void SetVolume(AudioBus bus, float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value)) return;
        levels[(int)bus] = Mathf.Clamp01(value); Changed();
    }
    public void SetMuted(bool value) { muted = value; Changed(); }
    public void RestoreDefaults()
    { levels[0] = .8f; levels[1] = .55f; levels[2] = .8f; levels[3] = .65f; muted = false; Changed(); }
    private void Changed()
    { ApplyMix(); dirty = true; saveAt = Time.unscaledTime + .3f; SettingsChanged?.Invoke(); }
    public static void SaveIfReady() { if (instance != null && instance.dirty) instance.SaveSettings(); }
    public void SaveSettings()
    {
        for (int i = 0; i < levels.Length; i++) PlayerPrefs.SetFloat(Prefix + (AudioBus)i, levels[i]);
        PlayerPrefs.SetInt(Prefix + "Muted", muted ? 1 : 0); PlayerPrefs.Save(); dirty = false;
    }
    public void ReloadSettings()
    {
        float[] defaults = { .8f, .55f, .8f, .65f };
        for (int i = 0; i < levels.Length; i++)
        {
            float value = PlayerPrefs.GetFloat(Prefix + (AudioBus)i, defaults[i]);
            levels[i] = float.IsNaN(value) || float.IsInfinity(value) ? defaults[i] : Mathf.Clamp01(value);
        }
        muted = PlayerPrefs.GetInt(Prefix + "Muted", 0) != 0;
        dirty = false; ApplyMix(); SettingsChanged?.Invoke();
    }
    public static void Play(AudioCue cue, Vector3? position = null)
    {
        var audio = Ensure();
        if (!audio.cues.TryGetValue(cue, out var clip) || clip == null) return;
        if (cue == AudioCue.Click || cue == AudioCue.Success || cue == AudioCue.Failure) { audio.ui.PlayOneShot(clip); return; }
        if (Time.timeScale == 0 && cue != AudioCue.Success && cue != AudioCue.Failure) return;
        var source = audio.effects[audio.effectSlot++ % audio.effects.Length];
        source.Stop(); source.clip = clip; source.pitch = 1;
        source.spatialBlend = position.HasValue ? .85f : 0;
        if (position.HasValue) { source.transform.position = position.Value; source.minDistance = 2; source.maxDistance = 22; source.rolloffMode = AudioRolloffMode.Linear; }
        source.Play();
    }
    private void OnApplicationFocus(bool focused) { if (!focused && dirty) SaveSettings(); }
    private void OnApplicationQuit() { if (dirty) SaveSettings(); }
    private void OnDestroy()
    {
        if (instance != this) return;
        SceneManager.sceneLoaded -= SceneChanged;
        if (dirty) SaveSettings(); instance = null;
    }
}
