using UnityEngine;

namespace TacticalEcho.AnimationSystem.Runtime
{
    public sealed class PlayerAnimationController : MonoBehaviour
    {
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int AimHash = Animator.StringToHash("Aim");
        private static readonly int FireHash = Animator.StringToHash("Fire");
        private static readonly int ReloadHash = Animator.StringToHash("Reload");

        [SerializeField] private Animator animator;

        public void SetLocomotion(float speed) => animator?.SetFloat(SpeedHash, speed);
        public void SetAim(bool isAiming) => animator?.SetBool(AimHash, isAiming);
        public void PlayFire() => animator?.SetTrigger(FireHash);
        public void PlayReload() => animator?.SetTrigger(ReloadHash);
    }
}
