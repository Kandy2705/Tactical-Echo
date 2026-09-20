using UnityEngine;

namespace TacticalEcho.AnimationSystem.Runtime
{
    public sealed class EnemyAnimationController : MonoBehaviour
    {
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int AimHash = Animator.StringToHash("Aim");
        private static readonly int FireHash = Animator.StringToHash("Fire");
        private static readonly int ReloadHash = Animator.StringToHash("Reload");

        [SerializeField] private Animator animator;

        private bool isDead;

        public bool IsDead => isDead;

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

        public void Configure(Animator newAnimator)
        {
            animator = newAnimator;
            ResolveAnimator();
        }

        public void SetLocomotion(float speed)
        {
            if (isDead)
            {
                return;
            }

            ResolveAnimator();
            animator?.SetFloat(SpeedHash, Mathf.Clamp01(speed));
        }

        public void SetAim(bool isAiming)
        {
            if (isDead)
            {
                return;
            }

            ResolveAnimator();
            animator?.SetBool(AimHash, isAiming);
        }

        public void PlayFire()
        {
            if (isDead)
            {
                return;
            }

            ResolveAnimator();
            animator?.SetTrigger(FireHash);
        }

        public void PlayReload()
        {
            if (isDead)
            {
                return;
            }

            ResolveAnimator();
            animator?.SetTrigger(ReloadHash);
        }

        public void PlayDeath()
        {
            if (isDead)
            {
                return;
            }

            ResolveAnimator();
            if (animator == null)
            {
                Debug.LogWarning(
                    "[Enemy Animation] Cannot play death animation because no Animator is assigned.",
                    this);
                return;
            }

            isDead = true;

            animator.SetFloat(SpeedHash, 0f);
            animator.SetBool(AimHash, false);
            animator.ResetTrigger(FireHash);
            animator.ResetTrigger(ReloadHash);

            if (!DeathAnimationPlayer.Play(animator))
            {
                Debug.LogWarning(
                    "[Enemy Animation] Death state was entered, but the shared death clip could not be played.",
                    this);
            }
        }








        public void ClearDeath()
        {
            if (!isDead)
            {
                return;
            }

            isDead = false;

            ResolveAnimator();
            DeathAnimationPlayer.Stop(animator);
        }

        private void ResolveAnimator()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>(true);
            }
        }
    }
}
