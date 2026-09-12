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

        private void Awake()
        {
            ResolveAnimator();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ResolveAnimator();
        }
#endif

        public void Configure(PlayerVisualController newVisual)
        {
            visual = newVisual;
            ResolveAnimator();
        }

        public void SetLocomotion(Vector2 moveInput, float normalizedSpeed, bool isSprinting, bool isAiming)
        {
            if (animator == null)
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
            if (animator == null)
            {
                return;
            }

            animator.ResetTrigger(FireHash);
            animator.SetTrigger(FireHash);
        }

        public void PlayReload()
        {
            if (animator == null)
            {
                return;
            }

            animator.ResetTrigger(ReloadHash);
            animator.SetTrigger(ReloadHash);
        }

        private void UpdateUpperBodyLayer(bool isAiming)
        {
            if (upperBodyLayerIndex < 0 || animator == null)
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

            upperBodyLayerIndex = animator != null ? animator.GetLayerIndex(UpperBodyLayerName) : -1;
            upperBodyLayerWeight = upperBodyLayerIndex >= 0 && animator != null
                ? animator.GetLayerWeight(upperBodyLayerIndex)
                : 0f;
        }
    }
}
