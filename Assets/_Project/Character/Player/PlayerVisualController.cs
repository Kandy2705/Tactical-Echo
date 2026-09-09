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
            ResolveReferences();

            if (animator != null)
            {
                animator.applyRootMotion = false;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ResolveReferences();
        }
#endif

        public void Configure(Transform newVisualRoot, Animator newAnimator)
        {
            visualRoot = newVisualRoot;
            animator = newAnimator;

            if (animator != null)
            {
                animator.applyRootMotion = false;
            }
        }

        private void ResolveReferences()
        {
            if (visualRoot == null)
            {
                Transform candidate = transform.Find("Visual");
                visualRoot = candidate != null ? candidate : transform;
            }

            if (animator == null && visualRoot != null)
            {
                animator = visualRoot.GetComponentInChildren<Animator>(true);
            }
        }
    }
}
