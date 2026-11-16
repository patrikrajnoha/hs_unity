using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Connects the feature to spawned local players without scene-side manual wiring.</summary>
public static class HighStakesFeatureBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallSceneHook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureAllPlayers();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureAllPlayers();
    }

    public static void EnsureAllPlayers()
    {
        PlayerController[] players = Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (PlayerController player in players)
            EnsureForPlayer(player.gameObject);
        foreach (var agent in Object.FindObjectsByType<UnityEngine.AI.NavMeshAgent>(FindObjectsSortMode.None))
        {
            Animator animator = agent.GetComponentInChildren<Animator>();
            if (animator != null && animator.GetComponent<NPCVisualAnimatorDriver>() == null)
                animator.gameObject.AddComponent<NPCVisualAnimatorDriver>();
        }
    }

    public static void EnsureForPlayer(GameObject player)
    {
        if (player != null && player.GetComponent<HighStakes.City.CityInputBridge>() != null) return;
        if (player == null)
            return;

        NetworkPlayer networkPlayer = player.GetComponent<NetworkPlayer>();
        if (networkPlayer != null && !networkPlayer.isLocalPlayer)
            return;

        PoliceHeatController heat = player.GetComponent<PoliceHeatController>();
        if (heat == null)
            heat = player.AddComponent<PoliceHeatController>();
        else
            heat.enabled = true;

        MinimapController minimap = player.GetComponent<MinimapController>();
        if (minimap == null)
            minimap = player.AddComponent<MinimapController>();
        else
            minimap.enabled = true;
    }
}
