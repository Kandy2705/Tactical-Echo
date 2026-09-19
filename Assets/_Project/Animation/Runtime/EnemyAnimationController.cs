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

        public void Configure(Animator newAnimator)
        {
            animator = newAnimator;
        }

        public void SetLocomotion(float speed)
        {
            if (isDead)
            {
                return;
            }

            animator.SetFloat(SpeedHash, Mathf.Clamp01(speed));
        }

        public void SetAim(bool isAiming)
        {
            if (isDead)
            {
                return;
            }

            animator.SetBool(AimHash, isAiming);
        }

        public void PlayFire()
        {
            if (isDead)
            {
                return;
            }

            animator.SetTrigger(FireHash);
        }

        public void PlayReload()
        {
            if (isDead)
            {
                return;
            }

            animator.SetTrigger(ReloadHash);
        }

        public void PlayDeath()
        {
            if (isDead)
            {
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

        /// <summary>
        /// Releases the death latch and hands the rig back to the AnimatorController.
        /// PlayDeath() both raises <see cref="isDead"/> (which mutes every locomotion and
        /// weapon parameter write) and starts a Playable graph that owns the Animator's
        /// output. Both must be undone together, otherwise a character whose brain is alive
        /// again keeps the death pose and never animates.
        /// </summary>
        public void ClearDeath()
        {
            if (!isDead)
            {
                return;
            }

            isDead = false;

            DeathAnimationPlayer.Stop(animator);
        }

    }
}
