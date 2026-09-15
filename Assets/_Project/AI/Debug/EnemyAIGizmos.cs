using TacticalEcho.AI.Brain;
using UnityEngine;

namespace TacticalEcho.AI.Debugging
{
    public sealed class EnemyAIGizmos : MonoBehaviour
    {
        [SerializeField] private EnemyBrain brain;

        public void Configure(EnemyBrain newBrain)
        {
            brain = newBrain;
        }

        private void OnDrawGizmosSelected()
        {
            if (brain == null)
            {
                return;
            }

            if (brain.Vision != null)
            {
                Transform origin = brain.Vision.EyeOrigin != null ? brain.Vision.EyeOrigin : brain.transform;
                Gizmos.DrawWireSphere(origin.position, brain.Vision.Range);

                float halfFov = brain.Vision.FieldOfView * 0.5f;
                Vector3 left = Quaternion.AngleAxis(-halfFov, Vector3.up) * origin.forward;
                Vector3 right = Quaternion.AngleAxis(halfFov, Vector3.up) * origin.forward;
                Gizmos.DrawLine(origin.position, origin.position + left * brain.Vision.Range);
                Gizmos.DrawLine(origin.position, origin.position + right * brain.Vision.Range);

                if (brain.Vision.Target != null)
                {
                    Gizmos.DrawLine(origin.position, brain.Vision.Target.position + Vector3.up);
                }
            }

            if (brain.Memory != null && brain.Memory.HasKnownPosition)
            {
                Gizmos.DrawSphere(brain.Memory.LastKnownPosition, 0.2f);
                Gizmos.DrawLine(brain.transform.position, brain.Memory.LastKnownPosition);
            }
        }
    }
}
