using UnityEngine;

namespace TacticalEcho.CameraSystem.Obstruction
{
    public sealed class CameraObstructionHandler : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private LayerMask obstructionMask;
        [SerializeField, Range(0.05f, 1f)] private float fadedAlpha = 0.25f;

        private readonly RaycastHit[] hits = new RaycastHit[16];

        public float FadedAlpha => fadedAlpha;

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            Vector3 direction = target.position - transform.position;
            float distance = direction.magnitude;
            if (distance <= 0f)
            {
                return;
            }

            Physics.RaycastNonAlloc(transform.position, direction / distance, hits, distance, obstructionMask, QueryTriggerInteraction.Ignore);
            // Renderer fade ownership lives here. PrimeTween will be used only to smooth presentation,
            // while MaterialPropertyBlock owns the actual renderer property changes.
        }
    }
}
