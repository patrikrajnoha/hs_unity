using System;
using System.Collections.Generic;
using UnityEngine;
using HighStakes.City;

[Serializable]
public sealed class LocalizationEntry
{
    public string key;
    public List<string> lines = new List<string>();
}

[Serializable]
public sealed class LocalizationFile
{
    public List<LocalizationEntry> entries = new List<LocalizationEntry>();
}

/// <summary>
/// Loads the active language from Resources/Localization/{language}.json.
/// Empty or missing translations fall back to English until the translation
/// is filled in by the author.
/// </summary>
public static class LocalizationManager
{
    public const string English = "en";
    public const string Slovak = "sk";
    private const string LanguagePreferenceKey = "HighStakes.Language";

    private static readonly Dictionary<string, List<string>> currentLines =
        new Dictionary<string, List<string>>();
    private static readonly Dictionary<string, List<string>> englishLines =
        new Dictionary<string, List<string>>();
    private static bool loaded;

    public static string CurrentLanguage { get; private set; } = English;
    public static event Action LanguageChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        loaded = false;
        CurrentLanguage = English;
        currentLines.Clear();
        englishLines.Clear();
        LanguageChanged = null;
    }

    public static void EnsureLoaded()
    {
        if (loaded)
            return;

        CurrentLanguage = Normalize(PlayerPrefs.GetString(LanguagePreferenceKey, English));
        LoadLanguage(English, englishLines);
        LoadLanguage(CurrentLanguage, currentLines);
        loaded = true;
    }

    public static void SetLanguage(string language)
    {
        EnsureLoaded();
        string normalized = Normalize(language);
        if (normalized == CurrentLanguage)
        {
            LanguageChanged?.Invoke();
            return;
        }

        CurrentLanguage = normalized;
        currentLines.Clear();
        LoadLanguage(CurrentLanguage, currentLines);
        PlayerPrefs.SetString(LanguagePreferenceKey, CurrentLanguage);
        PlayerPrefs.Save();
        LanguageChanged?.Invoke();
    }

    public static void ToggleLanguage()
    {
        SetLanguage(CurrentLanguage == English ? Slovak : English);
    }

    public static string Get(string key, params string[] replacements)
    {
        EnsureLoaded();
        string line;
        if (!TryPick(currentLines, key, out line) &&
            !TryPick(englishLines, key, out line))
        {
            Debug.LogError($"Localization: Missing key '{key}'.");
            return $"[{key}]";
        }

        return ApplyReplacements(line, replacements);
    }

    public static string GetOrFallback(string key, string fallback, params string[] replacements)
    {
        string value = Get(key, replacements);
        return value == $"[{key}]" ? fallback : value;
    }

    public static string GetCityActorName(string actorId, string fallback)
    {
        if (string.IsNullOrWhiteSpace(actorId))
            return fallback;

        // Residents are generated at runtime as local0..local9 and share one
        // localized label. Avoid querying a non-existent per-resident key,
        // which would otherwise spam the Console with missing-key errors.
        if (actorId.StartsWith("local", StringComparison.Ordinal))
            return GetOrFallback("city.actor.local.name", fallback);

        string localized = GetOrFallback($"city.actor.{actorId}.name", string.Empty);
        return string.IsNullOrEmpty(localized) ? fallback : localized;
    }

    public static string GetItemName(Item item)
    {
        if (item == null)
            return string.Empty;

        string fallback = string.IsNullOrWhiteSpace(item.itemName) ? item.name : item.itemName;
        if (string.IsNullOrWhiteSpace(item.name))
            return fallback;

        return GetOrFallback($"item.{item.name}.name", fallback);
    }

    public static void LocalizeGuidance(QuestGuidanceContent content)
    {
        if (content == null || content.stages == null)
            return;

        EnsureLoaded();
        foreach (QuestGuidanceStage stage in content.stages)
        {
            if (stage == null)
                continue;

            string prefix = $"guidance.stage.{stage.id}";
            stage.title = GetOrFallback(prefix + ".title", stage.title);
            stage.transitionMessage = GetOrFallback(prefix + ".transition", stage.transitionMessage);
            if (stage.objectives == null)
                continue;

            foreach (QuestGuidanceObjective objective in stage.objectives)
            {
                if (objective == null)
                    continue;

                string objectivePrefix = prefix + ".objective." + objective.id;
                objective.title = GetOrFallback(objectivePrefix + ".title", objective.title);
                objective.description = GetOrFallback(objectivePrefix + ".description", objective.description);
                objective.progress = GetOrFallback(objectivePrefix + ".progress", objective.progress);
                objective.hint = GetOrFallback(objectivePrefix + ".hint", objective.hint);
            }
        }
    }

    public static void LocalizeStory(StoryFile story)
    {
        if (story == null)
            return;

        EnsureLoaded();
        if (story.contracts != null)
        {
            foreach (Contract contract in story.contracts)
            {
                if (contract == null)
                    continue;

                contract.title = GetOrFallback($"city.contract.{contract.id}.title", contract.title);
                contract.description = GetOrFallback($"city.contract.{contract.id}.description", contract.description);
                if (contract.steps == null)
                    continue;

                for (int i = 0; i < contract.steps.Length; i++)
                {
                    if (contract.steps[i] == null)
                        continue;
                    contract.steps[i].objective = GetOrFallback(
                        $"city.contract.{contract.id}.step.{i}.objective", contract.steps[i].objective);
                }
            }
        }

        if (story.conversations == null)
            return;

        foreach (Conversation conversation in story.conversations)
        {
            if (conversation == null)
                continue;

            string prefix = $"city.conversation.{conversation.id}";
            conversation.speaker = GetOrFallback(prefix + ".speaker", conversation.speaker);
            conversation.text = GetOrFallback(prefix + ".text", conversation.text);
            if (conversation.choices == null)
                continue;

            for (int i = 0; i < conversation.choices.Length; i++)
            {
                if (conversation.choices[i] == null)
                    continue;
                conversation.choices[i].text = GetOrFallback(
                    prefix + $".choice.{i}.text", conversation.choices[i].text);
            }
        }
    }

    private static string Normalize(string language)
    {
        return string.Equals(language, Slovak, StringComparison.OrdinalIgnoreCase) ? Slovak : English;
    }

    private static void LoadLanguage(string language, Dictionary<string, List<string>> destination)
    {
        TextAsset asset = Resources.Load<TextAsset>($"Localization/{language}");
        if (asset == null)
        {
            Debug.LogWarning($"Localization: Missing Resources/Localization/{language}.json.");
            return;
        }

        LocalizationFile file;
        try
        {
            file = JsonUtility.FromJson<LocalizationFile>(asset.text);
        }
        catch (Exception exception)
        {
            Debug.LogError($"Localization: Invalid JSON in {language}.json. {exception.Message}");
            return;
        }

        if (file == null || file.entries == null)
            return;

        foreach (LocalizationEntry entry in file.entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.key) || destination.ContainsKey(entry.key))
                continue;
            destination.Add(entry.key, entry.lines ?? new List<string>());
        }
    }

    private static bool TryPick(Dictionary<string, List<string>> source, string key, out string line)
    {
        line = null;
        List<string> values;
        if (!source.TryGetValue(key, out values) || values == null)
            return false;

        List<string> nonEmpty = values.FindAll(value => !string.IsNullOrWhiteSpace(value));
        if (nonEmpty.Count == 0)
            return false;

        line = nonEmpty[UnityEngine.Random.Range(0, nonEmpty.Count)];
        return true;
    }

    private static string ApplyReplacements(string line, string[] replacements)
    {
        for (int i = 0; i + 1 < replacements.Length; i += 2)
        {
            string placeholder = "{" + replacements[i] + "}";
            line = line.Replace(placeholder, replacements[i + 1] ?? string.Empty);
        }
        return line;
    }
}
