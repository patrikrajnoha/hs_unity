using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public static class AudioRuntimeChecks
{
    // Run inside the campaign Play Mode check, restoring every user's audio preference.
    public static void Check()
    {
        var audio = GameAudio.Instance;
        Require(audio.SourcesReady, "Music/ambience assets did not load");
        Require(UnityEngine.Object.FindObjectsByType<GameAudio>().Length == 1, "Duplicate audio manager");
        var panel = UnityEngine.Object.FindAnyObjectByType<AudioSettingsPanel>(FindObjectsInactive.Include);
        Require(panel != null && panel.GetComponentsInChildren<Slider>(true).Length == 4, "Pause settings lack four volume sliders");
        string[] keys = { "Master", "Music", "Effects", "Ambience", "Muted" };
        bool[] exists = keys.Select(k => PlayerPrefs.HasKey("HighStakes.Audio." + k)).ToArray();
        float[] old = keys.Take(4).Select(k => PlayerPrefs.GetFloat("HighStakes.Audio." + k)).ToArray();
        int oldMute = PlayerPrefs.GetInt("HighStakes.Audio.Muted");
        try
        {
            audio.SetVolume(AudioBus.Master, .71f); audio.SetVolume(AudioBus.Music, .23f);
            audio.SetMuted(true); audio.SaveSettings(); audio.SetMuted(false); audio.ReloadSettings();
            Require(audio.Muted && Mathf.Abs(audio.GetVolume(AudioBus.Music) - .23f) < .001f, "Audio preferences did not survive reload");
            Require(audio.GetComponentsInChildren<AudioSource>().All(s => s.volume == 0), "Mute did not silence all owned sources");
            audio.SetMuted(false); audio.SetVolume(AudioBus.Music, 0);
            var sources = audio.GetComponentsInChildren<AudioSource>();
            Require(sources.Where(s => s.name.Contains("music")).All(s => s.volume == 0), "Music channel did not mute independently");
            Require(sources.Single(s => s.name == "Interface").volume > 0, "Music slider also muted sound effects");
            audio.SetVolume(AudioBus.Effects, -10); Require(audio.GetVolume(AudioBus.Effects) == 0, "Negative volume accepted");
            audio.SetVolume(AudioBus.Effects, 10); Require(audio.GetVolume(AudioBus.Effects) == 1, "Volume above one accepted");
            var slider = panel.GetComponentsInChildren<Slider>(true).Single(s => s.name == "Volume Music");
            slider.value = .37f;
            Require(Mathf.Abs(audio.GetVolume(AudioBus.Music) - .37f) < .001f, "UI slider is not connected to audio");
            foreach (AudioCue cue in Enum.GetValues(typeof(AudioCue))) GameAudio.Play(cue);
        }
        finally
        {
            for (int i = 0; i < keys.Length; i++)
            {
                string key = "HighStakes.Audio." + keys[i];
                if (!exists[i]) PlayerPrefs.DeleteKey(key);
                else if (i == 4) PlayerPrefs.SetInt(key, oldMute);
                else PlayerPrefs.SetFloat(key, old[i]);
            }
            PlayerPrefs.Save(); audio.ReloadSettings();
        }
        Debug.Log("AUDIO RUNTIME PASS: assets, singleton, four UI sliders, channel isolation, mute, clamping and saved preferences.");
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
