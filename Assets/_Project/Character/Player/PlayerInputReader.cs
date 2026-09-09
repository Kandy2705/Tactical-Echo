using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TacticalEcho.Character.Player
{
    public sealed class PlayerInputReader : MonoBehaviour
    {
        [SerializeField] private InputActionReference moveAction;
        [SerializeField] private InputActionReference lookAction;
        [SerializeField] private InputActionReference fireAction;
        [SerializeField] private InputActionReference reloadAction;

        public event Action FireRequested;
        public event Action ReloadRequested;

        public Vector2 Move => moveAction != null ? moveAction.action.ReadValue<Vector2>() : Vector2.zero;
        public Vector2 Look => lookAction != null ? lookAction.action.ReadValue<Vector2>() : Vector2.zero;

        private void OnEnable()
        {
            Enable(moveAction);
            Enable(lookAction);
            Enable(fireAction);
            Enable(reloadAction);

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

            Disable(moveAction);
            Disable(lookAction);
            Disable(fireAction);
            Disable(reloadAction);
        }

        private void OnFirePerformed(InputAction.CallbackContext context) => FireRequested?.Invoke();
        private void OnReloadPerformed(InputAction.CallbackContext context) => ReloadRequested?.Invoke();

        private static void Enable(InputActionReference actionReference) => actionReference?.action.Enable();
        private static void Disable(InputActionReference actionReference) => actionReference?.action.Disable();
    }
}
