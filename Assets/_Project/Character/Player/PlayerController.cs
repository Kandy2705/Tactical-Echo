using TacticalEcho.AnimationSystem.Runtime;
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
        [SerializeField] private PlayerAnimationController animationController;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float walkSpeed = 4.5f;
        [SerializeField, Min(0f)] private float sprintSpeed = 7f;
        [SerializeField, Min(0f)] private float rotationSharpness = 14f;
        [SerializeField, Min(0f)] private float gravity = 24f;
        [SerializeField, Min(0f)] private float groundedStickForce = 2f;

        private CharacterController characterController;
        private float verticalVelocity;
        private Vector2 currentMoveInput;

        public Vector3 PlanarVelocity { get; private set; }
        public bool IsGrounded => characterController != null && characterController.isGrounded;
        public bool IsAiming => input != null && input.IsAiming;
        public bool IsSprinting => input != null && input.IsSprinting && !IsAiming;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
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
            UpdateCombat();
            UpdateAnimation();
        }

        public void ConfigureCoreReferences(
            PlayerInputReader inputReader,
            WeaponController weaponController,
            Transform newAimOrigin,
            PlayerAnimationController newAnimationController)
        {
            input = inputReader;
            weapon = weaponController;
            aimOrigin = newAimOrigin;
            animationController = newAnimationController;
        }

        public void ConfigureWeapon(WeaponController weaponController)
        {
            weapon = weaponController;
        }

        public void ConfigureCamera(PlayerCameraController cameraController, Transform orientation)
        {
            playerCamera = cameraController;
            cameraOrientation = orientation;
        }

        private void UpdateMovement()
        {
            currentMoveInput = Vector2.ClampMagnitude(input.Move, 1f);
            Transform orientation = cameraOrientation != null ? cameraOrientation : transform;

            Vector3 forward = Vector3.ProjectOnPlane(orientation.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(orientation.right, Vector3.up).normalized;
            Vector3 desiredDirection = forward * currentMoveInput.y + right * currentMoveInput.x;

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

        private void UpdateCombat()
        {
            if (weapon == null)
            {
                return;
            }

            if (input.ReloadPressedThisFrame)
            {
                if (weapon.TryBeginReload())
                {
                    animationController?.PlayReload();
                }
                return;
            }

            if (weapon.IsReloading)
            {
                return;
            }

            bool wantsToFire = weapon.Definition != null && weapon.Definition.FireMode == FireMode.Automatic
                ? input.IsFiring
                : input.FirePressedThisFrame;

            if (!wantsToFire)
            {
                return;
            }

            Vector3 origin = cameraOrientation != null
                ? cameraOrientation.position
                : aimOrigin != null
                    ? aimOrigin.position
                    : transform.position + Vector3.up * 1.5f;

            Vector3 direction = cameraOrientation != null
                ? cameraOrientation.forward
                : aimOrigin != null
                    ? aimOrigin.forward
                    : transform.forward;

            if (weapon.TryFire(origin, direction))
            {
                animationController?.PlayFire();
            }
        }

        private void UpdateAnimation()
        {
            if (animationController == null)
            {
                return;
            }

            float normalizedSpeed = sprintSpeed > 0f
                ? Mathf.Clamp01(PlanarVelocity.magnitude / sprintSpeed)
                : 0f;

            animationController.SetLocomotion(currentMoveInput, normalizedSpeed, IsSprinting, IsAiming);
        }
    }
}
