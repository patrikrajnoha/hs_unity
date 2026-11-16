using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MenuUIController : MonoBehaviour
{
    [Header("UI Elements")]
    public TMP_Text statusText;
    public TMP_InputField steamIDInput;
    public Button connectButton;
    public Button playButton;
    public GameObject lobbyPanel;

    void Start()
    {
        if (connectButton != null)
            connectButton.onClick.AddListener(OnConnectButtonClicked);

        if (playButton != null)
        {
            playButton.onClick.AddListener(OnPlayButtonClicked);
            // Local play must remain available even when Steam is disabled in the editor.
            playButton.interactable = true;
        }

        if (!SteamManager.Initialized)
        {
            if (connectButton != null)
                connectButton.interactable = false;

            if (statusText != null)
                statusText.text = "Steam unavailable - local mode";

            return;
        }

        if (playButton != null)
        {
            playButton.interactable = false; // Start disabled
        }

        UpdateUI();
        InvokeRepeating(nameof(UpdateUI), 0.5f, 0.5f);
    }

    void UpdateUI()
    {
        if (!SteamManager.Initialized || NetworkManager.Instance == null) return;

        string myID = NetworkManager.Instance.GetMySteamID();
        int playerCount = NetworkManager.Instance.GetConnectedPlayerCount();
        bool isHost = NetworkManager.Instance.IsHost();
        bool isConnected = NetworkManager.Instance.IsConnected();

        // Update status text
        if (statusText != null)
        {
            statusText.text = $" Your Steam ID: {myID}\n";

            if (!isConnected)
            {
                statusText.text += " Waiting for connection...\n";
                statusText.text += " Share your ID or enter friend's ID";
            }
            else
            {
                statusText.text += $" Connected! Players: {playerCount + 1}\n";
                statusText.text += isHost ? " You are the HOST" : " You are the CLIENT";
            }
        }

        // Play button only enabled for host when connected
        if (playButton != null)
        {
            playButton.interactable = !isConnected || isHost;

            TMP_Text buttonText = playButton.GetComponentInChildren<TMP_Text>();
            if (buttonText != null)
            {
                if (isHost && isConnected)
                    buttonText.text = " START GAME";
                else if (!isHost && isConnected)
                    buttonText.text = " Waiting for host...";
                else
                    buttonText.text = " PLAY OLD QUARTER";
            }
        }
    }

    void OnConnectButtonClicked()
    {
        if (!SteamManager.Initialized || NetworkManager.Instance == null)
            return;

        string friendID = steamIDInput.text;

        if (string.IsNullOrEmpty(friendID))
        {
            Debug.LogWarning(" Please enter a Steam ID!");
            return;
        }

        NetworkManager.Instance.ConnectToFriend(friendID);
        steamIDInput.text = "";
    }

    void OnPlayButtonClicked()
    {
        if (!SteamManager.Initialized || NetworkManager.Instance == null || !NetworkManager.Instance.IsConnected())
        {
            SceneManager.LoadScene("OldQuarter");
            return;
        }
        if (SteamManager.Initialized && NetworkManager.Instance != null && NetworkManager.Instance.IsHost())
        {
            Debug.Log(" Starting game...");
            NetworkManager.Instance.StartGame();
            return;
        }

    }
}
