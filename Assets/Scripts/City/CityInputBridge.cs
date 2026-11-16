using UnityEngine;
using UnityEngine.InputSystem;

namespace HighStakes.City
{
    [RequireComponent(typeof(PlayerController))]
    public sealed class CityInputBridge : MonoBehaviour
    {
        private InputAction move, look, jump, run;
        private void OnEnable()
        {
            PlayerController player = GetComponent<PlayerController>();
            move = new InputAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddBinding("<Gamepad>/leftStick");
            look = new InputAction("Look", InputActionType.Value, "<Mouse>/delta");
            jump = new InputAction("Jump", InputActionType.Button, "<Keyboard>/space");
            jump.AddBinding("<Gamepad>/buttonSouth");
            run = new InputAction("Run", InputActionType.Button, "<Keyboard>/leftShift");
            move.performed += player.OnMove; move.canceled += player.OnMove;
            look.performed += player.OnLook; look.canceled += player.OnLook;
            jump.performed += player.OnJump;
            run.started += player.OnRun; run.canceled += player.OnRun;
            move.Enable(); look.Enable(); jump.Enable(); run.Enable();
        }
        private void OnDisable()
        {
            move?.Dispose(); look?.Dispose(); jump?.Dispose(); run?.Dispose();
        }
    }
}
