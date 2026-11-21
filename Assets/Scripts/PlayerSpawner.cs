using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerSpawner : MonoBehaviour
{
    public GameObject playerPrefab;

    void Start()
    {
        // Join manually or auto-detect
        PlayerInput.Instantiate(playerPrefab, controlScheme: "Keyboard&Mouse", pairWithDevice: Keyboard.current);
        PlayerInput.Instantiate(playerPrefab, controlScheme: "Gamepad", pairWithDevice: Gamepad.current);
        GameLogger.LogInfo("player", "player_spawned", "Player instantiated via PlayerInput");
    }
}
