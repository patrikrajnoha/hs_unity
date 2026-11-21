using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor utilities for HighStakes local gameplay logging.
/// </summary>
public static class HighStakesLogTools
{
    [MenuItem("Tools/HighStakes/Open Logs Folder", false, 100)]
    [MenuItem("HighStakes/Open Logs Folder", false, 100)]
    public static void OpenLogsFolderMenuItem()
    {
        string dir = GameLogger.LogDirectory;
        if (string.IsNullOrEmpty(dir))
        {
            // If runtime instance is not active yet, resolve default editor directory
            dir = System.IO.Path.Combine(Application.persistentDataPath, "Logs");
        }

        if (!System.IO.Directory.Exists(dir))
        {
            try
            {
                System.IO.Directory.CreateDirectory(dir);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[HighStakesLogTools] Could not create logs directory '{dir}': {ex.Message}");
                return;
            }
        }

        EditorUtility.RevealInFinder(dir);
    }
}
