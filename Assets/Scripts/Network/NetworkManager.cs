using UnityEngine;
using Steamworks;
using System.Collections.Generic;
using System.Text;
using UnityEngine.SceneManagement;

public class NetworkManager : MonoBehaviour
{
    public static NetworkManager Instance;

    [Header("Player Setup")]
    public GameObject playerPrefab;

    [Header("Connection Info")]
    public bool isHost = false;
    private List<CSteamID> connectedPlayers = new List<CSteamID>();
    private Dictionary<ulong, GameObject> spawnedPlayers = new Dictionary<ulong, GameObject>();

    private GameObject localPlayer;
    private bool gameStarted = false;

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
            return;
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

        // Listen for scene changes
        SceneManager.sceneLoaded += OnSceneLoaded;

        // Start as neither host nor client
        isHost = false;

        Debug.Log(" Network Manager ready!");
        Debug.Log($" My Steam ID: {SteamUser.GetSteamID().m_SteamID}");
    }

    void Update()
    {
        if (!SteamManager.Initialized) return;
        ReceiveMessages();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($" Scene loaded: {scene.name} (Index: {scene.buildIndex})");

        // If we're in the game scene and game has started, spawn players
        if (scene.buildIndex == 1 && gameStarted)
        {
            Debug.Log(" Waiting 1 second before spawning players...");
            Invoke(nameof(SpawnPlayers), 1f); // Wait 1 second to ensure both players are in scene
        }
    }

    // ============ CONNECTION ============

    void OnP2PSessionRequest(P2PSessionRequest_t request)
    {
        if (!SteamManager.Initialized) return;

        Debug.Log($" OnP2PSessionRequest FIRED!");
        Debug.Log($" Connection request from: {SteamFriends.GetFriendPersonaName(request.m_steamIDRemote)} ({request.m_steamIDRemote.m_SteamID})");

        // Accept the connection
        SteamNetworking.AcceptP2PSessionWithUser(request.m_steamIDRemote);

        if (!connectedPlayers.Contains(request.m_steamIDRemote))
        {
            connectedPlayers.Add(request.m_steamIDRemote);

            // The person who RECEIVES the connection is the HOST
            isHost = true;
            GameLogger.LogInfo("network", "player_connected", $"Accepted connection from {request.m_steamIDRemote.m_SteamID} as HOST. Total: {connectedPlayers.Count + 1}");
            Debug.Log($" YOU ARE NOW THE HOST! (received connection)");
            Debug.Log($" Total players: {connectedPlayers.Count + 1}");
        }
        else
        {
            Debug.Log(" This player was already in the list!");
        }
    }

    void OnP2PSessionConnectFail(P2PSessionConnectFail_t failure)
    {
        GameLogger.LogError("network", "connection_failed", $"P2P connection failed: {failure.m_eP2PSessionError}");
        Debug.LogError($" Connection failed: {failure.m_eP2PSessionError}");
    }

    public void ConnectToFriend(string steamIDString)
    {
        if (!SteamManager.Initialized) return;

        if (ulong.TryParse(steamIDString, out ulong id))
        {
            CSteamID friendId = new CSteamID(id);
            GameLogger.LogInfo("network", "connect_attempt", $"Connecting to Steam ID: {id}");
            Debug.Log($" Attempting to connect to Steam ID: {id}");

            // The person who clicks CONNECT is the CLIENT (not host)
            isHost = false;
            Debug.Log($" YOU ARE THE CLIENT! (initiating connection)");

            // Send initial connection packet
            byte[] data = Encoding.UTF8.GetBytes("CONNECT");
            bool sent = SteamNetworking.SendP2PPacket(friendId, data, (uint)data.Length,
                EP2PSend.k_EP2PSendReliable, 0);

            Debug.Log($" Packet sent: {sent}");

            if (!connectedPlayers.Contains(friendId))
            {
                connectedPlayers.Add(friendId);
                GameLogger.LogInfo("network", "player_connected", $"Connected to friend {id}. Total: {connectedPlayers.Count + 1}");
                Debug.Log($" Added friend to connected players list. Total: {connectedPlayers.Count + 1}");
            }
            else
            {
                Debug.Log(" Friend already in connected players list!");
            }
        }
        else
        {
            GameLogger.LogError("network", "invalid_steam_id", "Invalid Steam ID format: " + steamIDString);
            Debug.LogError(" Invalid Steam ID format!");
        }
    }

    // ============ GAME START ============

    public void StartGame()
    {
        if (!isHost)
        {
            Debug.LogWarning(" Only the host can start the game!");
            return;
        }

        Debug.Log(" Host starting game...");
        GameLogger.LogInfo("network", "game_started", "Host started game session");
        gameStarted = true;

        // Tell all connected players to load the game scene
        SendToAll("LOAD_GAME");

        // Small delay before host loads to ensure message is sent
        Invoke(nameof(LoadGameScene), 0.5f);
    }

    void LoadGameScene()
    {
        Debug.Log(" Loading game scene...");
        SceneManager.LoadScene(1);
    }

    // ============ SPAWNING ============

    void SpawnPlayers()
    {
        Debug.Log(" === SPAWNING PLAYERS ===");
        Debug.Log($"I am: {(isHost ? "HOST" : "CLIENT")}");
        Debug.Log($"Connected players count: {connectedPlayers.Count}");

        // Find spawn points
        GameObject[] spawnPointObjects = GameObject.FindGameObjectsWithTag("SpawnPoint");

        if (spawnPointObjects.Length == 0)
        {
            Debug.LogError(" No spawn points found! Add GameObjects with 'SpawnPoint' tag!");
            return;
        }

        Debug.Log($"Found {spawnPointObjects.Length} spawn points");

        Transform[] spawnPoints = new Transform[spawnPointObjects.Length];
        for (int i = 0; i < spawnPointObjects.Length; i++)
        {
            spawnPoints[i] = spawnPointObjects[i].transform;
            Debug.Log($"Spawn {i}: {spawnPoints[i].position}");
        }

        // Spawn local player (host gets spawn 0, client gets spawn 1)
        int mySpawnIndex = isHost ? 0 : 1;
        Debug.Log($"My spawn index: {mySpawnIndex}");
        GameLogger.LogInfo("network", "players_spawned", $"Spawned player as {(isHost ? "HOST" : "CLIENT")} at index {mySpawnIndex}");
        SpawnLocalPlayer(spawnPoints[mySpawnIndex].position, spawnPoints[mySpawnIndex].rotation);

        // Spawn remote players
        int remoteSpawnIndex = isHost ? 1 : 0;
        Debug.Log($"Remote spawn index: {remoteSpawnIndex}");

        foreach (CSteamID player in connectedPlayers)
        {
            Debug.Log($"Spawning remote player: {player.m_SteamID}");
            SpawnRemotePlayer(player.m_SteamID, spawnPoints[remoteSpawnIndex].position,
                spawnPoints[remoteSpawnIndex].rotation);
        }

        // Send our spawn info to others
        SendSpawnInfo();

        // HOST: Sync world state to client after spawning
        if (isHost)
        {
            Debug.Log(" Host syncing world state in 2 seconds...");
            Invoke(nameof(SyncWorldState), 2f);
        }

        Debug.Log(" === SPAWNING COMPLETE ===");
    }

    void SpawnLocalPlayer(Vector3 position, Quaternion rotation)
    {
        if (playerPrefab == null)
        {
            Debug.LogError(" Player Prefab not assigned!");
            return;
        }

        localPlayer = Instantiate(playerPrefab, position, rotation);

        NetworkPlayer netPlayer = localPlayer.GetComponent<NetworkPlayer>();
        if (netPlayer != null && SteamManager.Initialized)
        {
            netPlayer.isLocalPlayer = true;
            netPlayer.steamID = SteamUser.GetSteamID().m_SteamID;
            netPlayer.playerColor = Color.green;
        }

        Debug.Log($" Spawned local player at {position}");
    }

    void SpawnRemotePlayer(ulong steamID, Vector3 position, Quaternion rotation)
    {
        if (spawnedPlayers.ContainsKey(steamID))
        {
            Debug.Log(" Remote player already spawned!");
            return;
        }

        if (playerPrefab == null) return;

        GameObject remotePlayer = Instantiate(playerPrefab, position, rotation);

        NetworkPlayer netPlayer = remotePlayer.GetComponent<NetworkPlayer>();
        if (netPlayer != null)
        {
            netPlayer.isLocalPlayer = false;
            netPlayer.steamID = steamID;
            netPlayer.playerColor = Color.red;
        }

        spawnedPlayers[steamID] = remotePlayer;
        Debug.Log($" Spawned remote player {steamID} at {position}");
    }

    void SendSpawnInfo()
    {
        if (localPlayer == null) return;

        Vector3 pos = localPlayer.transform.position;
        Quaternion rot = localPlayer.transform.rotation;

        string message = $"SPAWN|{pos.x}|{pos.y}|{pos.z}|{rot.x}|{rot.y}|{rot.z}|{rot.w}";
        SendToAll(message);
    }

    // ============ WORLD STATE SYNC ============

    // This sends the entire world state from host to client
    public void SyncWorldState()
    {
        if (!isHost)
        {
            Debug.LogWarning(" Only host can sync world state!");
            return;
        }

        Debug.Log(" Host syncing world state to clients...");

        // Sync all networked objects
        NetworkedObject[] allObjects = FindObjectsOfType<NetworkedObject>();
        foreach (NetworkedObject obj in allObjects)
        {
            SyncObjectState(obj);
        }
    }

    public void SyncObjectState(NetworkedObject obj)
    {
        if (!isHost) return;

        string message = $"OBJECT|{obj.objectID}|{obj.isActive}|" +
                        $"{obj.transform.position.x}|{obj.transform.position.y}|{obj.transform.position.z}|" +
                        $"{obj.transform.rotation.x}|{obj.transform.rotation.y}|{obj.transform.rotation.z}|{obj.transform.rotation.w}";
        SendToAll(message);
    }

    void HandleObjectSync(string[] parts)
    {
        // OBJECT|objectID|isActive|posX|posY|posZ|rotX|rotY|rotZ|rotW
        if (parts.Length < 10) return;

        string objectID = parts[1];
        bool isActive = bool.Parse(parts[2]);

        Vector3 position = new Vector3(
            float.Parse(parts[3]),
            float.Parse(parts[4]),
            float.Parse(parts[5])
        );

        Quaternion rotation = new Quaternion(
            float.Parse(parts[6]),
            float.Parse(parts[7]),
            float.Parse(parts[8]),
            float.Parse(parts[9])
        );

        // Find the object by ID
        NetworkedObject[] objects = FindObjectsOfType<NetworkedObject>(true);
        foreach (NetworkedObject obj in objects)
        {
            if (obj.objectID == objectID)
            {
                obj.gameObject.SetActive(isActive);
                if (isActive)
                {
                    obj.transform.position = position;
                    obj.transform.rotation = rotation;
                }
                Debug.Log($" Synced object: {objectID} - Active: {isActive}");
                return;
            }
        }

        Debug.LogWarning($" Could not find object with ID: {objectID}");
    }

    public void SendPlayerPosition(Vector3 position, Quaternion rotation)
    {
        if (!SteamManager.Initialized) return;

        string message = $"POS|{position.x}|{position.y}|{position.z}|" +
                        $"{rotation.x}|{rotation.y}|{rotation.z}|{rotation.w}";

        byte[] data = Encoding.UTF8.GetBytes(message);
        foreach (CSteamID player in connectedPlayers)
        {
            SteamNetworking.SendP2PPacket(player, data, (uint)data.Length,
                EP2PSend.k_EP2PSendUnreliable, 0);
        }
    }

    // ============ MESSAGING ============

    void SendToAll(string message)
    {
        if (!SteamManager.Initialized) return;

        byte[] data = Encoding.UTF8.GetBytes(message);
        foreach (CSteamID player in connectedPlayers)
        {
            SteamNetworking.SendP2PPacket(player, data, (uint)data.Length,
                EP2PSend.k_EP2PSendReliable, 0);
        }
    }

    void ReceiveMessages()
    {
        if (!SteamManager.Initialized) return;

        uint packetSize;
        while (SteamNetworking.IsP2PPacketAvailable(out packetSize))
        {
            byte[] data = new byte[packetSize];
            uint bytesRead;
            CSteamID senderId;

            if (SteamNetworking.ReadP2PPacket(data, packetSize, out bytesRead, out senderId))
            {
                string message = Encoding.UTF8.GetString(data);
                HandleMessage(senderId.m_SteamID, message);
            }
        }
    }

    void HandleMessage(ulong senderID, string message)
    {
        string[] parts = message.Split('|');

        Debug.Log($" Received message: {parts[0]} from {senderID}");

        switch (parts[0])
        {
            case "CONNECT":
                Debug.Log($" CONNECT message received from {senderID}");
                // This should trigger OnP2PSessionRequest callback
                // But just in case, let's handle it here too
                if (!connectedPlayers.Contains(new CSteamID(senderID)))
                {
                    connectedPlayers.Add(new CSteamID(senderID));
                    if (!isHost)
                    {
                        Debug.Log(" Setting as host because we received CONNECT and aren't host yet");
                        isHost = true;
                    }
                }
                break;

            case "OBJECT":
                HandleObjectSync(parts);
                break;

            case "LOAD_GAME":
                Debug.Log(" Host started the game! Loading scene...");
                gameStarted = true;

                // Ensure we don't load multiple times
                if (SceneManager.GetActiveScene().buildIndex != 1)
                {
                    SceneManager.LoadScene(1);
                }
                break;

            case "SPAWN":
                // Remote player spawned, update their position if needed
                if (spawnedPlayers.ContainsKey(senderID))
                {
                    float x = float.Parse(parts[1]);
                    float y = float.Parse(parts[2]);
                    float z = float.Parse(parts[3]);
                    Vector3 position = new Vector3(x, y, z);

                    spawnedPlayers[senderID].transform.position = position;
                }
                break;

            case "POS":
                // Update remote player position
                if (spawnedPlayers.ContainsKey(senderID))
                {
                    float x = float.Parse(parts[1]);
                    float y = float.Parse(parts[2]);
                    float z = float.Parse(parts[3]);

                    float rx = float.Parse(parts[4]);
                    float ry = float.Parse(parts[5]);
                    float rz = float.Parse(parts[6]);
                    float rw = float.Parse(parts[7]);

                    Vector3 position = new Vector3(x, y, z);
                    Quaternion rotation = new Quaternion(rx, ry, rz, rw);

                    NetworkPlayer netPlayer = spawnedPlayers[senderID].GetComponent<NetworkPlayer>();
                    if (netPlayer != null)
                    {
                        netPlayer.UpdatePosition(position, rotation);
                    }
                }
                break;
        }
    }

    // ============ GETTERS ============

    public string GetMySteamID()
    {
        if (!SteamManager.Initialized) return string.Empty;

        return SteamUser.GetSteamID().m_SteamID.ToString();
    }

    public int GetConnectedPlayerCount()
    {
        return connectedPlayers.Count;
    }

    public bool IsHost()
    {
        return isHost;
    }

    public bool IsConnected()
    {
        return connectedPlayers.Count > 0;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}
