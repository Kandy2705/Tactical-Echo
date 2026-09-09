using TacticalEcho.AI.Brain;
using UnityEngine;

namespace TacticalEcho.AI.Debugging
{
    public sealed class EnemyAIGizmos : MonoBehaviour
    {
        [SerializeField] private EnemyBrain brain;

        private void OnDrawGizmosSelected()
        {
            if (brain == null || brain.Vision == null)
            {
                return;
            }

            Gizmos.DrawWireSphere(brain.transform.position, brain.Vision.Range);

            if (brain.Memory != null && brain.Memory.HasKnownPosition)
            {
                Gizmos.DrawSphere(brain.Memory.LastKnownPosition, 0.2f);
                Gizmos.DrawLine(brain.transform.position, brain.Memory.LastKnownPosition);
            }
        }
    }
}
