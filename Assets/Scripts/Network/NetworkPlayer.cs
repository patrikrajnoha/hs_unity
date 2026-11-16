using UnityEngine;
using Steamworks;

public class NetworkPlayer : MonoBehaviour
{
    public ulong steamID;
    public bool isLocalPlayer = false;
    public Color playerColor = Color.white;

    [Header("Color Settings")]
    public bool changeColorOnSpawn = true;
    public Renderer[] renderersToColor;

    void Start()
    {
        if (changeColorOnSpawn)
        {
            SetPlayerColor(playerColor);
        }

        if (isLocalPlayer)
        {
            gameObject.name = "LocalPlayer (YOU)";
            HighStakesFeatureBootstrap.EnsureForPlayer(gameObject);
            Debug.Log(" YOU spawned! (Green)");
        }
        else
        {
            gameObject.name = $"RemotePlayer_{steamID}";
            Debug.Log(" Remote player spawned! (Red)");
            DisablePlayerControl();
        }
    }

    void DisablePlayerControl()
    {
        // Disable all scripts on remote player
        MonoBehaviour[] scripts = GetComponents<MonoBehaviour>();
        foreach (var script in scripts)
        {
            if (script != this && !(script is Transform))
            {
                script.enabled = false;
            }
        }

        // Disable scripts on children
        foreach (Transform child in transform.GetComponentsInChildren<Transform>())
        {
            MonoBehaviour[] childScripts = child.GetComponents<MonoBehaviour>();
            foreach (var script in childScripts)
            {
                if (!(script is Transform) && !(script is NetworkPlayer) &&
                    !(script is PlayerVisualAnimator) && !(script is HighStakes.City.StreetPuppet))
                {
                    script.enabled = false;
                }
            }
        }

        // DISABLE CAMERA AND AUDIO LISTENER on remote player
        Camera cam = GetComponentInChildren<Camera>();
        if (cam != null)
        {
            cam.enabled = false;
            Debug.Log(" Disabled camera on remote player");
        }

        AudioListener listener = GetComponentInChildren<AudioListener>();
        if (listener != null)
        {
            listener.enabled = false;
            Debug.Log(" Disabled audio listener on remote player");
        }

        // DISABLE EVENT SYSTEM on remote player (keep only local player's)
        UnityEngine.EventSystems.EventSystem eventSystem = GetComponentInChildren<UnityEngine.EventSystems.EventSystem>();
        if (eventSystem != null)
        {
            Destroy(eventSystem.gameObject);
            Debug.Log(" Destroyed event system on remote player");
        }
    }

    void SetPlayerColor(Color color)
    {
        if (renderersToColor == null || renderersToColor.Length == 0)
        {
            renderersToColor = GetComponentsInChildren<Renderer>();
        }

        foreach (Renderer rend in renderersToColor)
        {
            if (rend != null)
            {
                Material[] materials = rend.materials;
                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i].color = color;
                }
                rend.materials = materials;
            }
        }
    }

    void Update()
    {
        if (isLocalPlayer)
        {
            SendPositionUpdate();
        }
    }

    private float lastSendTime = 0f;
    private float sendRate = 0.05f;

    void SendPositionUpdate()
    {
        if (Time.time - lastSendTime > sendRate)
        {
            if (NetworkManager.Instance != null)
                NetworkManager.Instance.SendPlayerPosition(transform.position, transform.rotation);
            lastSendTime = Time.time;
        }
    }

    public void UpdatePosition(Vector3 position, Quaternion rotation)
    {
        transform.position = Vector3.Lerp(transform.position, position, Time.deltaTime * 10f);
        transform.rotation = Quaternion.Lerp(transform.rotation, rotation, Time.deltaTime * 10f);
    }
}
