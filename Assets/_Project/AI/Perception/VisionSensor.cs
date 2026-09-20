using UnityEngine;

namespace TacticalEcho.AI.Perception
{
    public sealed class VisionSensor : MonoBehaviour, ISensor
    {
        [SerializeField] private Transform eyeOrigin;
        [SerializeField] private Transform target;
        [SerializeField, Min(0.1f)] private float range = 20f;
        [SerializeField, Range(1f, 180f)] private float fieldOfView = 90f;
        [SerializeField, Min(0.01f)] private float scanInterval = 0.1f;
        [SerializeField] private LayerMask targetMask = ~0;
        [SerializeField] private LayerMask obstacleMask = ~0;

        private float scanTimer;

        public bool HasLineOfSight { get; private set; }
        public Transform VisibleTarget => HasLineOfSight ? target : null;
        public float Range => range;
        public float FieldOfView => fieldOfView;
        public Transform EyeOrigin => eyeOrigin;
        public Transform Target => target;

        public void Configure(
            Transform newEyeOrigin,
            Transform newTarget,
            LayerMask newTargetMask,
            LayerMask newObstacleMask,
            float newRange = 20f,
            float newFieldOfView = 90f)
        {
            eyeOrigin = newEyeOrigin;
            target = newTarget;
            targetMask = newTargetMask;
            obstacleMask = newObstacleMask;
            range = Mathf.Max(0.1f, newRange);
            fieldOfView = Mathf.Clamp(newFieldOfView, 1f, 180f);
            scanTimer = 0f;
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        public void TickSensor(float deltaTime)
        {
            scanTimer -= deltaTime;
            if (scanTimer > 0f)
            {
                return;
            }

            scanTimer = scanInterval;
            Scan();
        }

        private void Scan()
        {
            HasLineOfSight = false;
            if (eyeOrigin == null || target == null)
            {
                return;
            }

            Vector3 targetPoint = target.position + Vector3.up * 1.0f;
            Vector3 toTarget = targetPoint - eyeOrigin.position;
            float distance = toTarget.magnitude;
            if (distance <= 0f || distance > range)
            {
                return;
            }

            Vector3 direction = toTarget / distance;
            float minimumDot = Mathf.Cos(fieldOfView * 0.5f * Mathf.Deg2Rad);
            if (Vector3.Dot(eyeOrigin.forward, direction) < minimumDot)
            {
                return;
            }

            int mask = targetMask.value | obstacleMask.value;
            if (!Physics.Raycast(
                    eyeOrigin.position,
                    direction,
                    out RaycastHit hit,
                    distance,
                    mask,
                    QueryTriggerInteraction.Ignore))
            {
                return;
            }

            HasLineOfSight = hit.transform == target || hit.transform.IsChildOf(target);
        }
    }
}
