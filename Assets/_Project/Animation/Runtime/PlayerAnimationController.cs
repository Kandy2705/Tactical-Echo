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

        [SerializeField] private PlayerVisualController visual;
        [SerializeField, Min(0f)] private float locomotionDampTime = 0.1f;

        private Animator animator;

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
        }

        public void PlayFire()
        {
            animator?.SetTrigger(FireHash);
        }

        public void PlayReload()
        {
            animator?.SetTrigger(ReloadHash);
        }

        private void ResolveAnimator()
        {
            animator = visual != null ? visual.Animator : null;

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>(true);
            }
        }
    }
}
