using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class NpcDialogueEntry
{
    public string key;
    public List<string> lines = new List<string>();
}

[Serializable]
public class NpcDialogueFile
{
    public List<NpcDialogueEntry> entries = new List<NpcDialogueEntry>();
}

/// <summary>
/// Loads all NPC-facing text from Resources/Dialogues/npc_dialogues.json.
/// The JSON file contains keys with one or more possible lines. Placeholders
/// can be replaced by passing pairs such as "item", "Weed".
/// </summary>
public static class NpcDialogue
{
    private const string ResourcePath = "Dialogues/npc_dialogues";

    private static readonly Dictionary<string, List<string>> Lines =
        new Dictionary<string, List<string>>();

    private static readonly HashSet<string> MissingKeys =
        new HashSet<string>();

    private static bool loaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        loaded = false; Lines.Clear(); MissingKeys.Clear();
    }

    public static string Get(string key, params string[] replacements)
    {
        EnsureLoaded();

        List<string> possibleLines;
        if (!Lines.TryGetValue(key, out possibleLines) || possibleLines.Count == 0)
        {
            if (MissingKeys.Add(key))
                Debug.LogError($"NpcDialogue: Missing dialogue key '{key}'.");

            return $"[{key}]";
        }

        string line = possibleLines[UnityEngine.Random.Range(0, possibleLines.Count)];
        for (int i = 0; i + 1 < replacements.Length; i += 2)
        {
            string placeholder = "{" + replacements[i] + "}";
            line = line.Replace(placeholder, replacements[i + 1] ?? string.Empty);
        }

        return line;
    }

    private static void EnsureLoaded()
    {
        if (loaded)
            return;

        loaded = true;
        TextAsset json = Resources.Load<TextAsset>(ResourcePath);
        if (json == null)
        {
            Debug.LogError($"NpcDialogue: Could not load Resources/{ResourcePath}.json.");
            return;
        }

        NpcDialogueFile file;
        try
        {
            file = JsonUtility.FromJson<NpcDialogueFile>(json.text);
        }
        catch (Exception exception)
        {
            Debug.LogError($"NpcDialogue: Invalid JSON in {ResourcePath}.json. {exception.Message}");
            return;
        }

        if (file == null || file.entries == null)
        {
            Debug.LogError($"NpcDialogue: {ResourcePath}.json does not contain an 'entries' array.");
            return;
        }

        foreach (NpcDialogueEntry entry in file.entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.key))
                continue;

            if (Lines.ContainsKey(entry.key))
            {
                Debug.LogError($"NpcDialogue: Duplicate dialogue key '{entry.key}'.");
                continue;
            }

            Lines.Add(entry.key, entry.lines ?? new List<string>());
        }
    }
}
