using TacticalEcho.AnimationSystem.Runtime;
using TacticalEcho.CameraSystem;
using TacticalEcho.Combat.Damage;
using TacticalEcho.Combat.Health;
using TacticalEcho.Combat.StatusEffects;
using TacticalEcho.Combat.Weapons;
using TacticalEcho.Inventory.Equipment;
using TacticalEcho.UI;
using UnityEngine;

namespace TacticalEcho.Character.Player
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(Health))]
    public sealed class PlayerController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerCameraController playerCamera;
        [SerializeField] private WeaponController weapon;
        [SerializeField] private Transform cameraOrientation;
        [SerializeField] private Transform aimOrigin;
        [SerializeField] private PlayerAnimationController animationController;
        [SerializeField] private Health health;
        [SerializeField] private EquipmentController equipment;
        [SerializeField] private StatusEffectController statusEffects;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float walkSpeed = 4.5f;
        [SerializeField, Min(0f)] private float sprintSpeed = 7f;
        [SerializeField, Min(0f)] private float rotationSharpness = 14f;
        [SerializeField, Min(0f)] private float gravity = 24f;
        [SerializeField, Min(0f)] private float groundedStickForce = 2f;

        [Header("Combat Facing")]
        [Tooltip("How long the character keeps facing the camera/crosshair direction after a successful shot.")]
        [SerializeField, Min(0f)] private float fireFacingHoldTime = 0.18f;

        private CharacterController characterController;
        private PlayerCombatHud combatHud;
        private WeaponController subscribedWeapon;
        private Health subscribedHealth;
        private float verticalVelocity;
        private float fireFacingUntilTime;
        private Vector2 currentMoveInput;
        private bool isDead;

        public Vector3 PlanarVelocity { get; private set; }
        public bool IsGrounded => characterController != null && characterController.isGrounded;
        public bool IsAiming => !isDead && input != null && input.IsAiming;
        public bool IsSprinting => !isDead && input != null && input.IsSprinting && !IsAiming;
        public bool IsDead => isDead;
        public Health Health => health;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            health = health != null ? health : GetComponent<Health>();
            if (health == null)
            {
                health = gameObject.AddComponent<Health>();
            }

            equipment = equipment != null ? equipment : GetComponent<EquipmentController>();
            statusEffects = statusEffects != null ? statusEffects : GetComponent<StatusEffectController>();

            combatHud = GetComponent<PlayerCombatHud>();
            if (combatHud == null)
            {
                combatHud = gameObject.AddComponent<PlayerCombatHud>();
            }
        }

        private void OnEnable()
        {
            BindWeaponEvents();
            BindHealthEvents();
            combatHud?.Configure(weapon);
        }

        private void OnDisable()
        {
            UnbindWeaponEvents();
            UnbindHealthEvents();
        }

        private void Update()
        {
            if (characterController == null)
            {
                return;
            }

            if (isDead)
            {


                ApplyVerticalMovement();
                return;
            }

            if (input == null)
            {
                return;
            }

            UpdateMovement();
            UpdateRotation();
            UpdateCameraMode();
            UpdateShoulderSwitch();
            UpdateEquipment();
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
            ConfigureWeapon(weaponController);
            aimOrigin = newAimOrigin;
            animationController = newAnimationController;
        }

        public void ConfigureWeapon(WeaponController weaponController)
        {
            if (weapon == weaponController)
            {
                combatHud?.Configure(weapon);
                return;
            }

            UnbindWeaponEvents();
            weapon = weaponController;

            if (isActiveAndEnabled)
            {
                BindWeaponEvents();
            }

            combatHud?.Configure(weapon);
        }

        public void ConfigureCamera(PlayerCameraController cameraController, Transform orientation)
        {
            playerCamera = cameraController;
            cameraOrientation = orientation;
        }

        private void BindWeaponEvents()
        {
            if (weapon == null || subscribedWeapon == weapon)
            {
                return;
            }

            UnbindWeaponEvents();
            subscribedWeapon = weapon;
            subscribedWeapon.DamageApplied += HandleDamageApplied;
            subscribedWeapon.DamageFeedbackResolved += HandleDamageFeedback;
        }

        private void UnbindWeaponEvents()
        {
            if (subscribedWeapon == null)
            {
                return;
            }

            subscribedWeapon.DamageApplied -= HandleDamageApplied;
            subscribedWeapon.DamageFeedbackResolved -= HandleDamageFeedback;
            subscribedWeapon = null;
        }

        private void BindHealthEvents()
        {
            if (health == null || subscribedHealth == health)
            {
                return;
            }

            UnbindHealthEvents();
            subscribedHealth = health;
            subscribedHealth.Died += HandleDied;
            isDead = !subscribedHealth.IsAlive;

            if (isDead)
            {
                animationController?.PlayDeath();
            }
        }

        private void UnbindHealthEvents()
        {
            if (subscribedHealth == null)
            {
                return;
            }

            subscribedHealth.Died -= HandleDied;
            subscribedHealth = null;
        }

        private void HandleDamageApplied()
        {
            playerCamera?.ShowHitMarker();
        }

        private static void HandleDamageFeedback(DamageFeedback feedback)
        {
            FloatingDamageNumberSystem.Show(feedback);
        }

        private void HandleDied()
        {
            if (isDead)
            {
                return;
            }

            isDead = true;
            currentMoveInput = Vector2.zero;
            PlanarVelocity = Vector3.zero;
            verticalVelocity = 0f;
            fireFacingUntilTime = 0f;

            weapon?.CancelReload();
            playerCamera?.SetMode(CameraMode.Explore);
            animationController?.SetLocomotion(Vector2.zero, 0f, false, false);
            animationController?.PlayDeath();
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



            float speed = (IsSprinting ? sprintSpeed : walkSpeed)
                          * (statusEffects != null ? statusEffects.MoveSpeedMultiplier : 1f);
            PlanarVelocity = desiredDirection * speed;

            ApplyVerticalMovement();
        }





        private void ApplyVerticalMovement()
        {
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
            bool keepCombatFacing = IsAiming || Time.time < fireFacingUntilTime;

            if (keepCombatFacing && cameraOrientation != null)
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

        private void UpdateShoulderSwitch()
        {
            if (playerCamera != null && input.SwitchShoulderPressedThisFrame)
            {
                playerCamera.SwitchShoulder();
            }
        }





        private void UpdateEquipment()
        {
            if (equipment == null)
            {
                return;
            }

            if (input.PrimaryWeaponPressedThisFrame)
            {
                equipment.TrySetActiveSlot(EquipmentSlot.PrimaryWeapon);
            }
            else if (input.SecondaryWeaponPressedThisFrame)
            {
                equipment.TrySetActiveSlot(EquipmentSlot.SecondaryWeapon);
            }
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

            if (weapon.Runtime != null && weapon.Runtime.CanFire(Time.time))
            {
                FaceShotDirection(direction);
            }

            float movement01 = sprintSpeed > 0f
                ? Mathf.Clamp01(PlanarVelocity.magnitude / sprintSpeed)
                : 0f;

            if (weapon.TryFire(origin, direction, movement01, IsAiming))
            {
                fireFacingUntilTime = Time.time + fireFacingHoldTime;
                animationController?.PlayFire();

                if (playerCamera != null && weapon.Definition != null)
                {
                    playerCamera.AddRecoil(weapon.Definition.Recoil);
                }
            }
        }

        private void FaceShotDirection(Vector3 shotDirection)
        {
            Vector3 planarDirection = Vector3.ProjectOnPlane(shotDirection, Vector3.up);
            if (planarDirection.sqrMagnitude <= 0.001f)
            {
                return;
            }

            transform.rotation = Quaternion.LookRotation(planarDirection.normalized, Vector3.up);
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
