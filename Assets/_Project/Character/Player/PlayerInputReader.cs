using System;
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

        public event Action FireRequested;
        public event Action ReloadRequested;

        public Vector2 Move => ReadVector2(moveAction);
        public Vector2 Look => ReadVector2(lookAction);
        public bool IsSprinting => IsPressed(sprintAction);
        public bool IsAiming => IsPressed(aimAction);

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

        private void OnEnable()
        {
            SetEnabled(moveAction, true);
            SetEnabled(lookAction, true);
            SetEnabled(sprintAction, true);
            SetEnabled(fireAction, true);
            SetEnabled(reloadAction, true);
            SetEnabled(aimAction, true);

            if (fireAction != null)
            {
                fireAction.action.performed += OnFirePerformed;
            }

            if (reloadAction != null)
            {
                reloadAction.action.performed += OnReloadPerformed;
            }
        }

        private void OnDisable()
        {
            if (fireAction != null)
            {
                fireAction.action.performed -= OnFirePerformed;
            }

            if (reloadAction != null)
            {
                reloadAction.action.performed -= OnReloadPerformed;
            }

            SetEnabled(moveAction, false);
            SetEnabled(lookAction, false);
            SetEnabled(sprintAction, false);
            SetEnabled(fireAction, false);
            SetEnabled(reloadAction, false);
            SetEnabled(aimAction, false);
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
        }

        private void OnFirePerformed(InputAction.CallbackContext context)
        {
            FireRequested?.Invoke();
        }

        private void OnReloadPerformed(InputAction.CallbackContext context)
        {
            ReloadRequested?.Invoke();
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
