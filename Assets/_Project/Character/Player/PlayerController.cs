using TacticalEcho.Combat.Weapons;
using UnityEngine;

namespace TacticalEcho.Character.Player
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private WeaponController weapon;
        [SerializeField] private Transform movementOrientation;
        [SerializeField, Min(0f)] private float moveSpeed = 5f;
        [SerializeField] private Transform aimOrigin;

        private CharacterController characterController;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
        }

        private void OnEnable()
        {
            if (input == null)
            {
                return;
            }

            input.FireRequested += HandleFireRequested;
            input.ReloadRequested += HandleReloadRequested;
        }

        private void OnDisable()
        {
            if (input == null)
            {
                return;
            }

            input.FireRequested -= HandleFireRequested;
            input.ReloadRequested -= HandleReloadRequested;
        }

        private void Update()
        {
            if (input == null || characterController == null)
            {
                return;
            }

            Vector2 moveInput = input.Move;
            Transform orientation = movementOrientation != null ? movementOrientation : transform;
            Vector3 move = orientation.forward * moveInput.y + orientation.right * moveInput.x;
            move.y = 0f;
            characterController.Move(move.normalized * moveSpeed * Time.deltaTime);
        }

        private void HandleFireRequested()
        {
            if (weapon == null)
            {
                return;
            }

            Transform origin = aimOrigin != null ? aimOrigin : transform;
            weapon.TryFire(origin.position, origin.forward);
        }

        private void HandleReloadRequested()
        {
            weapon?.TryBeginReload();
        }
    }
}
