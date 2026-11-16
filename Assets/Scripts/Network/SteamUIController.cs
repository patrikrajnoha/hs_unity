using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class SteamUIController : MonoBehaviour
{
    public TMP_Text statusText;
    public TMP_InputField steamIDInput;
    public Button connectButton;

    void Start()
    {
        if (!SteamManager.Initialized)
        {
            if (connectButton != null)
                connectButton.interactable = false;

            if (statusText != null)
                statusText.text = "Steam unavailable - local mode";

            return;
        }

        if (connectButton != null)
        {
            connectButton.onClick.AddListener(OnConnectButtonClicked);
        }

        UpdateStatus();
        InvokeRepeating(nameof(UpdateStatus), 1f, 1f);
    }

    void OnDestroy()
    {
        CancelInvoke(nameof(UpdateStatus));

        if (connectButton != null)
        {
            connectButton.onClick.RemoveListener(OnConnectButtonClicked);
        }
    }

    void UpdateStatus()
    {
        if (!SteamManager.Initialized || NetworkManager.Instance == null || statusText == null)
            return;

        if (NetworkManager.Instance != null && statusText != null)
        {
            string myID = NetworkManager.Instance.GetMySteamID();
            int playerCount = NetworkManager.Instance.GetConnectedPlayerCount();

            statusText.text = $" Your Steam ID: {myID}\n" +
                            $" Connected Players: {playerCount}\n" +
                            $" Use WASD or Arrow Keys to move\n" +
                            $" Share your ID with your friend!";
        }
    }

    void OnConnectButtonClicked()
    {
        if (!SteamManager.Initialized)
            return;

        string friendID = steamIDInput.text;

        if (string.IsNullOrEmpty(friendID))
        {
            Debug.LogWarning(" Please enter a Steam ID!");
            return;
        }

        if (NetworkManager.Instance == null)
        {
            Debug.LogWarning(" Network manager is not available!");
            return;
        }

        NetworkManager.Instance.ConnectToFriend(friendID);
        steamIDInput.text = "";
    }
}
