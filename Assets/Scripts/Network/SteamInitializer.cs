using UnityEngine;
using Steamworks;

public class SteamInitializer : MonoBehaviour
{
    void Start()
    {
        if (!SteamManager.Initialized)
        {
            return;
        }

        Debug.Log(" Steam initialized successfully!");
        Debug.Log($" Your Username: {SteamFriends.GetPersonaName()}");
        Debug.Log($" Your Steam ID: {SteamUser.GetSteamID().m_SteamID}");
    }
}
