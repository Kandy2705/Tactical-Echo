using TacticalEcho.Character.Player;
using UnityEngine;

namespace TacticalEcho.AnimationSystem.Runtime
{
    public sealed class PlayerAnimationController : MonoBehaviour
    {
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int MoveXHash = Animator.StringToHash("MoveX");
        private static readonly int MoveYHash = Animator.StringToHash("MoveY");
        private static readonly int SprintingHash = Animator.StringToHash("Sprinting");
        private static readonly int AimHash = Animator.StringToHash("Aim");
        private static readonly int FireHash = Animator.StringToHash("Fire");
        private static readonly int ReloadHash = Animator.StringToHash("Reload");

        private const string UpperBodyLayerName = "Upper Body";

        [SerializeField] private PlayerVisualController visual;
        [SerializeField, Min(0f)] private float locomotionDampTime = 0.1f;

        [Header("Rifle Upper Body")]
        [SerializeField, Range(0f, 1f)] private float relaxedWeaponLayerWeight = 0.78f;
        [SerializeField, Range(0f, 1f)] private float aimWeaponLayerWeight = 1f;
        [SerializeField, Min(0.01f)] private float weaponLayerBlendSpeed = 10f;

        private Animator animator;
        private int upperBodyLayerIndex = -1;
        private float upperBodyLayerWeight;
        private bool isDead;

        private void Awake()
        {
            ResolveAnimator();
        }

        public void Configure(PlayerVisualController newVisual)
        {
            visual = newVisual;
            ResolveAnimator();
        }

        public void SetLocomotion(Vector2 moveInput, float normalizedSpeed, bool isSprinting, bool isAiming)
        {
            if (isDead || !HasPlayableAnimator())
            {
                return;
            }

            animator.SetFloat(SpeedHash, Mathf.Clamp01(normalizedSpeed), locomotionDampTime, Time.deltaTime);
            animator.SetFloat(MoveXHash, moveInput.x, locomotionDampTime, Time.deltaTime);
            animator.SetFloat(MoveYHash, moveInput.y, locomotionDampTime, Time.deltaTime);
            animator.SetBool(SprintingHash, isSprinting);
            animator.SetBool(AimHash, isAiming);

            UpdateUpperBodyLayer(isAiming);
        }

        public void PlayFire()
        {
            if (isDead || !HasPlayableAnimator())
            {
                return;
            }

            animator.ResetTrigger(FireHash);
            animator.SetTrigger(FireHash);
        }

        public void PlayReload()
        {
            if (isDead || !HasPlayableAnimator())
            {
                return;
            }

            animator.ResetTrigger(ReloadHash);
            animator.SetTrigger(ReloadHash);
        }

        public void PlayDeath()
        {
            if (isDead || !HasPlayableAnimator())
            {
                return;
            }

            isDead = true;
            animator.ResetTrigger(FireHash);
            animator.ResetTrigger(ReloadHash);
            animator.SetBool(SprintingHash, false);
            animator.SetBool(AimHash, false);
            animator.SetFloat(SpeedHash, 0f);
            animator.SetFloat(MoveXHash, 0f);
            animator.SetFloat(MoveYHash, 0f);

            if (upperBodyLayerIndex >= 0)
            {
                animator.SetLayerWeight(upperBodyLayerIndex, 0f);
                upperBodyLayerWeight = 0f;
            }

            DeathAnimationPlayer.Play(animator);
        }

        private void UpdateUpperBodyLayer(bool isAiming)
        {
            if (upperBodyLayerIndex < 0 || !HasPlayableAnimator())
            {
                return;
            }

            float targetWeight = isAiming ? aimWeaponLayerWeight : relaxedWeaponLayerWeight;
            float blend = 1f - Mathf.Exp(-weaponLayerBlendSpeed * Time.deltaTime);
            upperBodyLayerWeight = Mathf.Lerp(upperBodyLayerWeight, targetWeight, blend);
            animator.SetLayerWeight(upperBodyLayerIndex, upperBodyLayerWeight);
        }

        private void ResolveAnimator()
        {
            animator = visual != null ? visual.Animator : null;

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>(true);
            }

            if (!HasPlayableAnimator())
            {
                upperBodyLayerIndex = -1;
                upperBodyLayerWeight = 0f;
                return;
            }

            upperBodyLayerIndex = animator.GetLayerIndex(UpperBodyLayerName);
            upperBodyLayerWeight = upperBodyLayerIndex >= 0
                ? animator.GetLayerWeight(upperBodyLayerIndex)
                : 0f;
        }

        private bool HasPlayableAnimator()
        {
            return animator != null && animator.runtimeAnimatorController != null;
        }
    }
}
