using UnityEngine;
using Steamworks;
using System.Collections.Generic;
using System.Text;

public class SteamNetworkManager : MonoBehaviour
{
    public static SteamNetworkManager Instance;

    private List<CSteamID> connectedPlayers = new List<CSteamID>();
    private Callback<P2PSessionRequest_t> m_P2PSessionRequest;
    private Callback<P2PSessionConnectFail_t> m_P2PSessionConnectFail;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        if (!SteamManager.Initialized)
        {
            return;
        }

        m_P2PSessionRequest = Callback<P2PSessionRequest_t>.Create(OnP2PSessionRequest);
        m_P2PSessionConnectFail = Callback<P2PSessionConnectFail_t>.Create(OnP2PSessionConnectFail);

        Debug.Log(" Steam Network Manager ready!");
    }

    void Update()
    {
        if (!SteamManager.Initialized) return;
        ReceiveMessages();
    }

    void OnP2PSessionRequest(P2PSessionRequest_t request)
    {
        if (!SteamManager.Initialized) return;

        Debug.Log($" Connection request from: {SteamFriends.GetFriendPersonaName(request.m_steamIDRemote)}");
        SteamNetworking.AcceptP2PSessionWithUser(request.m_steamIDRemote);

        if (!connectedPlayers.Contains(request.m_steamIDRemote))
        {
            connectedPlayers.Add(request.m_steamIDRemote);
            Debug.Log($" Connected to player! Total players: {connectedPlayers.Count + 1}");
        }
    }

    void OnP2PSessionConnectFail(P2PSessionConnectFail_t failure)
    {
        Debug.LogError($" Connection failed: {failure.m_eP2PSessionError}");
    }

    public void ConnectToFriend(string steamIDString)
    {
        if (!SteamManager.Initialized) return;

        if (ulong.TryParse(steamIDString, out ulong id))
        {
            CSteamID friendId = new CSteamID(id);
            Debug.Log($" Connecting to Steam ID: {id}");

            // Send initial packet
            byte[] data = Encoding.UTF8.GetBytes("HELLO");
            SteamNetworking.SendP2PPacket(friendId, data, (uint)data.Length,
                EP2PSend.k_EP2PSendReliable, 0);

            if (!connectedPlayers.Contains(friendId))
            {
                connectedPlayers.Add(friendId);
            }
        }
        else
        {
            Debug.LogError(" Invalid Steam ID format!");
        }
    }

    public void SendMessageToAll(string message)
    {
        if (!SteamManager.Initialized) return;

        byte[] data = Encoding.UTF8.GetBytes(message);

        foreach (CSteamID player in connectedPlayers)
        {
            SteamNetworking.SendP2PPacket(player, data, (uint)data.Length,
                EP2PSend.k_EP2PSendReliable, 0);
        }

        Debug.Log($" Sent message to {connectedPlayers.Count} player(s): {message}");
    }

    void ReceiveMessages()
    {
        uint packetSize;
        while (SteamNetworking.IsP2PPacketAvailable(out packetSize))
        {
            byte[] data = new byte[packetSize];
            uint bytesRead;
            CSteamID senderId;

            if (SteamNetworking.ReadP2PPacket(data, packetSize, out bytesRead, out senderId))
            {
                string message = Encoding.UTF8.GetString(data);
                string senderName = SteamFriends.GetFriendPersonaName(senderId);
                Debug.Log($" Received from {senderName}: {message}");
            }
        }
    }

    public string GetMySteamID()
    {
        if (!SteamManager.Initialized) return string.Empty;

        return SteamUser.GetSteamID().m_SteamID.ToString();
    }

    public int GetConnectedPlayerCount()
    {
        return connectedPlayers.Count;
    }
}
