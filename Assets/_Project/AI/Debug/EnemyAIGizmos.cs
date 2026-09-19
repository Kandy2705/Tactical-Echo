using TacticalEcho.AI.Brain;
using UnityEngine;
using UnityEngine.AI;

namespace TacticalEcho.AI.Debugging
{
    public sealed class EnemyAIGizmos : MonoBehaviour
    {
        [SerializeField] private EnemyBrain brain;

        [Header("Perception Debug")]
        [SerializeField, Range(6, 64)] private int visionArcSegments = 24;
        [SerializeField, Min(0f)] private float heardNoiseDisplayDuration = 2f;
        [SerializeField, Min(0.25f)] private float facingDirectionLength = 2f;
        [SerializeField] private Color visionColor = new(0.2f, 0.85f, 1f, 1f);
        [SerializeField] private Color visibleTargetColor = new(0.2f, 1f, 0.35f, 1f);
        [SerializeField] private Color blockedTargetColor = new(1f, 0.3f, 0.2f, 1f);
        [SerializeField] private Color hearingColor = new(1f, 0.75f, 0.15f, 1f);
        [SerializeField] private Color memoryColor = new(0.85f, 0.35f, 1f, 1f);
        [SerializeField] private Color facingColor = new(1f, 1f, 1f, 1f);
        [SerializeField] private Color navigationColor = new(0.25f, 1f, 0.65f, 1f);

        public void Configure(EnemyBrain newBrain)
        {
            brain = newBrain;
        }

        private void Reset()
        {
            brain = GetComponent<EnemyBrain>();
        }

        private void OnValidate()
        {
            brain = GetComponent<EnemyBrain>();
        }

        private void OnDrawGizmosSelected()
        {
            DrawFacingDirection();
            DrawNavigationFootprint();

            DrawVision();
            DrawHearing();
            DrawMemory();
        }

        private void DrawFacingDirection()
        {
            Vector3 origin = transform.position + Vector3.up * 0.08f;
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (forward.sqrMagnitude <= 0.001f)
            {
                return;
            }

            forward.Normalize();
            float length = Mathf.Max(0.25f, facingDirectionLength);
            Vector3 end = origin + forward * length;

            Gizmos.color = facingColor;
            Gizmos.DrawLine(origin, end);

            Vector3 right = Quaternion.AngleAxis(150f, Vector3.up) * forward;
            Vector3 left = Quaternion.AngleAxis(-150f, Vector3.up) * forward;
            Gizmos.DrawLine(end, end + right * 0.3f);
            Gizmos.DrawLine(end, end + left * 0.3f);
        }

        private void DrawNavigationFootprint()
        {
            NavMeshAgent agent = GetComponent<NavMeshAgent>();

            Gizmos.color = navigationColor;
            Vector3 center = transform.position + Vector3.up * 0.03f;
            Gizmos.DrawWireSphere(center, agent.radius);
        }

        private void DrawVision()
        {
            Transform origin = brain.Vision.EyeOrigin;
            float range = brain.Vision.Range;
            float halfFov = brain.Vision.FieldOfView * 0.5f;

            Gizmos.color = visionColor;
            Gizmos.DrawWireSphere(origin.position, range);

            Vector3 up = origin.up.sqrMagnitude > 0.001f ? origin.up.normalized : Vector3.up;
            Vector3 forward = Vector3.ProjectOnPlane(origin.forward, up);
            if (forward.sqrMagnitude <= 0.001f)
            {
                forward = origin.forward;
            }
            forward.Normalize();

            Vector3 left = Quaternion.AngleAxis(-halfFov, up) * forward;
            Vector3 right = Quaternion.AngleAxis(halfFov, up) * forward;
            Gizmos.DrawLine(origin.position, origin.position + left * range);
            Gizmos.DrawLine(origin.position, origin.position + right * range);
            Gizmos.DrawLine(origin.position, origin.position + forward * range);

            int segments = Mathf.Max(6, visionArcSegments);
            Vector3 previousPoint = origin.position + left * range;
            for (int i = 1; i <= segments; i++)
            {
                float t = i / (float)segments;
                float angle = Mathf.Lerp(-halfFov, halfFov, t);
                Vector3 direction = Quaternion.AngleAxis(angle, up) * forward;
                Vector3 nextPoint = origin.position + direction * range;
                Gizmos.DrawLine(previousPoint, nextPoint);
                previousPoint = nextPoint;
            }

            if (brain.Vision.Target == null)
            {
                return;
            }

            Vector3 targetPoint = brain.Vision.Target.position + Vector3.up;
            Gizmos.color = brain.Vision.HasLineOfSight ? visibleTargetColor : blockedTargetColor;
            Gizmos.DrawLine(origin.position, targetPoint);
            Gizmos.DrawWireSphere(targetPoint, 0.12f);
        }

        private void DrawHearing()
        {
            if (!brain.Hearing.HasLastAudibleNoise)
            {
                return;
            }

            if (Application.isPlaying
                && heardNoiseDisplayDuration > 0f
                && Time.time - brain.Hearing.LastAudibleTime > heardNoiseDisplayDuration)
            {
                return;
            }

            Vector3 noisePosition = brain.Hearing.LastAudibleNoisePosition;
            float audibleRadius = brain.Hearing.LastAudibleRadius;

            Gizmos.color = hearingColor;
            Gizmos.DrawWireSphere(noisePosition, audibleRadius);
            Gizmos.DrawSphere(noisePosition, 0.14f);
            Gizmos.DrawLine(brain.transform.position, noisePosition);
        }

        private void DrawMemory()
        {
            if (!brain.Memory.HasKnownPosition)
            {
                return;
            }

            Gizmos.color = memoryColor;
            Gizmos.DrawSphere(brain.Memory.LastKnownPosition, 0.2f);
            Gizmos.DrawLine(brain.transform.position, brain.Memory.LastKnownPosition);
        }
    }
}
