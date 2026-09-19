using UnityEngine;

namespace TacticalEcho.Character.Player
{
    public sealed class PlayerVisualController : MonoBehaviour
    {
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Animator animator;

        public Transform VisualRoot => visualRoot;
        public Animator Animator => animator;

        private void Awake()
        {
            animator.applyRootMotion = false;
        }

        public void Configure(Transform newVisualRoot, Animator newAnimator)
        {
            visualRoot = newVisualRoot;
            animator = newAnimator;

            animator.applyRootMotion = false;
        }
    }
}
