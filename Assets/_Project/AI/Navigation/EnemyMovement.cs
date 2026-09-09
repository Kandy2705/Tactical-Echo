using UnityEngine;
using UnityEngine.AI;

namespace TacticalEcho.AI.Navigation
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class EnemyMovement : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float stoppingTolerance = 0.25f;

        private NavMeshAgent agent;

        public bool HasPath => agent != null && agent.hasPath;
        public bool HasReachedDestination => agent != null && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + stoppingTolerance;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
        }

        public bool SetDestination(Vector3 destination)
        {
            return agent != null && agent.isOnNavMesh && agent.SetDestination(destination);
        }

        public void Stop()
        {
            if (agent == null || !agent.isOnNavMesh)
            {
                return;
            }

            agent.ResetPath();
        }
    }
}
