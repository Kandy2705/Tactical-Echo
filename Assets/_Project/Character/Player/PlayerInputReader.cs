using UnityEngine;
using UnityEngine.InputSystem;

namespace TacticalEcho.Character.Player
{
    public sealed class PlayerInputReader : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private InputActionReference moveAction;
        [SerializeField] private InputActionReference lookAction;
        [SerializeField] private InputActionReference sprintAction;

        [Header("Combat")]
        [SerializeField] private InputActionReference fireAction;
        [SerializeField] private InputActionReference reloadAction;
        [SerializeField] private InputActionReference aimAction;
        [Tooltip("When no Aim InputAction is assigned, right click toggles aim so a trackpad/mouse can fire without holding two buttons at once.")]
        [SerializeField] private bool toggleMouseAim = true;

        [Header("Equipment")]
        [Tooltip("Optional. When no InputAction is assigned, 1 and 2 select the primary and secondary weapon slot, matching the keyboard-fallback pattern already used for Reload.")]
        [SerializeField] private InputActionReference primaryWeaponAction;
        [SerializeField] private InputActionReference secondaryWeaponAction;

        [Header("Camera")]
        [Tooltip("Optional. When no InputAction is assigned, Q switches the over-the-shoulder camera side, matching the keyboard-fallback pattern already used for Reload.")]
        [SerializeField] private InputActionReference switchShoulderAction;

        private bool mouseAimToggled;

        public Vector2 Move => ReadVector2(moveAction);
        public Vector2 Look => ReadVector2(lookAction);
        public bool IsSprinting => IsPressed(sprintAction);



        public bool IsFiring => IsPressed(fireAction) || (Mouse.current != null && Mouse.current.leftButton.isPressed);
        public bool FirePressedThisFrame => WasPressedThisFrame(fireAction) || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);
        public bool ReloadPressedThisFrame => WasPressedThisFrame(reloadAction) || (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame);
        public bool PrimaryWeaponPressedThisFrame => WasPressedThisFrame(primaryWeaponAction) || (Keyboard.current != null && Keyboard.current.digit1Key.wasPressedThisFrame);
        public bool SecondaryWeaponPressedThisFrame => WasPressedThisFrame(secondaryWeaponAction) || (Keyboard.current != null && Keyboard.current.digit2Key.wasPressedThisFrame);
        public bool SwitchShoulderPressedThisFrame => WasPressedThisFrame(switchShoulderAction) || (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame);

        public bool IsAiming
        {
            get
            {
                if (aimAction != null)
                {
                    return IsPressed(aimAction);
                }

                if (Mouse.current == null)
                {
                    return false;
                }

                return toggleMouseAim ? mouseAimToggled : Mouse.current.rightButton.isPressed;
            }
        }

        public bool IsPointerLook
        {
            get
            {
                if (lookAction == null || lookAction.action == null)
                {
                    return true;
                }

                InputControl activeControl = lookAction.action.activeControl;
                return activeControl == null || activeControl.device is Pointer;
            }
        }

        private void Update()
        {
            if (aimAction == null && toggleMouseAim && Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            {
                mouseAimToggled = !mouseAimToggled;
            }
        }

        private void OnEnable()
        {
            SetEnabled(moveAction, true);
            SetEnabled(lookAction, true);
            SetEnabled(sprintAction, true);
            SetEnabled(fireAction, true);
            SetEnabled(reloadAction, true);
            SetEnabled(aimAction, true);
            SetEnabled(switchShoulderAction, true);
            SetEnabled(primaryWeaponAction, true);
            SetEnabled(secondaryWeaponAction, true);
        }

        private void OnDisable()
        {
            mouseAimToggled = false;
            SetEnabled(moveAction, false);
            SetEnabled(lookAction, false);
            SetEnabled(sprintAction, false);
            SetEnabled(fireAction, false);
            SetEnabled(reloadAction, false);
            SetEnabled(aimAction, false);
            SetEnabled(switchShoulderAction, false);
            SetEnabled(primaryWeaponAction, false);
            SetEnabled(secondaryWeaponAction, false);
        }

        public void Configure(
            InputActionReference move,
            InputActionReference look,
            InputActionReference sprint,
            InputActionReference fire,
            InputActionReference reload,
            InputActionReference aim)
        {
            moveAction = move;
            lookAction = look;
            sprintAction = sprint;
            fireAction = fire;
            reloadAction = reload;
            aimAction = aim;
            mouseAimToggled = false;
        }

        private static Vector2 ReadVector2(InputActionReference actionReference)
        {
            return actionReference != null
                ? actionReference.action.ReadValue<Vector2>()
                : Vector2.zero;
        }

        private static bool IsPressed(InputActionReference actionReference)
        {
            return actionReference != null && actionReference.action.IsPressed();
        }

        private static bool WasPressedThisFrame(InputActionReference actionReference)
        {
            return actionReference != null && actionReference.action.WasPressedThisFrame();
        }

        private static void SetEnabled(InputActionReference actionReference, bool enabled)
        {
            if (actionReference == null)
            {
                return;
            }

            if (enabled)
            {
                actionReference.action.Enable();
            }
            else
            {
                actionReference.action.Disable();
            }
        }
    }
}
