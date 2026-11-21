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
/// Compatibility facade for NPC-facing text. The active language is loaded
/// from Resources/Localization/{language}.json. Placeholders can be replaced
/// by passing pairs such as "item", "Weed".
/// </summary>
public static class NpcDialogue
{
    public static string Get(string key, params string[] replacements)
    {
        return LocalizationManager.Get(key, replacements);
    }
}
