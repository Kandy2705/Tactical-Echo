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
        [SerializeField] private LayerMask targetMask;
        [SerializeField] private LayerMask obstacleMask;

        private float scanTimer;

        public bool HasLineOfSight { get; private set; }
        public Transform VisibleTarget => HasLineOfSight ? target : null;
        public float Range => range;
        public float FieldOfView => fieldOfView;

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

            Vector3 toTarget = target.position - eyeOrigin.position;
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
            if (!Physics.Raycast(eyeOrigin.position, direction, out RaycastHit hit, range, mask, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            HasLineOfSight = hit.transform == target || hit.transform.IsChildOf(target);
        }
    }
}
