using TacticalEcho.CameraSystem;
using TacticalEcho.Combat.Weapons;
using UnityEngine;

namespace TacticalEcho.Character.Player
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerCameraController playerCamera;
        [SerializeField] private WeaponController weapon;
        [SerializeField] private Transform cameraOrientation;
        [SerializeField] private Transform aimOrigin;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float walkSpeed = 4.5f;
        [SerializeField, Min(0f)] private float sprintSpeed = 7f;
        [SerializeField, Min(0f)] private float rotationSharpness = 14f;
        [SerializeField, Min(0f)] private float gravity = 24f;
        [SerializeField, Min(0f)] private float groundedStickForce = 2f;

        private CharacterController characterController;
        private float verticalVelocity;

        public Vector3 PlanarVelocity { get; private set; }
        public bool IsGrounded => characterController != null && characterController.isGrounded;
        public bool IsAiming => input != null && input.IsAiming;
        public bool IsSprinting => input != null && input.IsSprinting && !IsAiming;

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

            UpdateMovement();
            UpdateRotation();
            UpdateCameraMode();
        }

        private void UpdateMovement()
        {
            Vector2 moveInput = Vector2.ClampMagnitude(input.Move, 1f);
            Transform orientation = cameraOrientation != null ? cameraOrientation : transform;

            Vector3 forward = Vector3.ProjectOnPlane(orientation.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(orientation.right, Vector3.up).normalized;
            Vector3 desiredDirection = forward * moveInput.y + right * moveInput.x;

            if (desiredDirection.sqrMagnitude > 1f)
            {
                desiredDirection.Normalize();
            }

            float speed = IsSprinting ? sprintSpeed : walkSpeed;
            PlanarVelocity = desiredDirection * speed;

            if (characterController.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -groundedStickForce;
            }
            else
            {
                verticalVelocity -= gravity * Time.deltaTime;
            }

            Vector3 velocity = PlanarVelocity + Vector3.up * verticalVelocity;
            characterController.Move(velocity * Time.deltaTime);
        }

        private void UpdateRotation()
        {
            Vector3 desiredForward;

            if (IsAiming && cameraOrientation != null)
            {
                desiredForward = Vector3.ProjectOnPlane(cameraOrientation.forward, Vector3.up);
            }
            else if (PlanarVelocity.sqrMagnitude > 0.001f)
            {
                desiredForward = PlanarVelocity;
            }
            else
            {
                return;
            }

            if (desiredForward.sqrMagnitude <= 0.001f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(desiredForward.normalized, Vector3.up);
            float t = 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, t);
        }

        private void UpdateCameraMode()
        {
            if (playerCamera == null)
            {
                return;
            }

            playerCamera.SetMode(IsAiming ? CameraMode.Aim : CameraMode.Explore);
        }

        private void HandleFireRequested()
        {
            if (weapon == null)
            {
                return;
            }

            Transform origin = aimOrigin != null
                ? aimOrigin
                : cameraOrientation != null
                    ? cameraOrientation
                    : transform;

            weapon.TryFire(origin.position, origin.forward);
        }

        private void HandleReloadRequested()
        {
            weapon?.TryBeginReload();
        }
    }
}
