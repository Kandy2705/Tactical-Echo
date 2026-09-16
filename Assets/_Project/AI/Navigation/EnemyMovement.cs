using UnityEngine;
using UnityEngine.AI;

namespace TacticalEcho.AI.Navigation
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class EnemyMovement : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float stoppingTolerance = 0.25f;
        [SerializeField, Min(0f)] private float navMeshSnapDistance = 2f;

        private NavMeshAgent agent;

        public bool IsOnNavMesh => agent != null && agent.isOnNavMesh;
        public bool HasPath => IsOnNavMesh && agent.hasPath;
        public bool HasReachedDestination => IsOnNavMesh
            && !agent.pathPending
            && agent.remainingDistance <= agent.stoppingDistance + stoppingTolerance;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
        }

        public bool SetDestination(Vector3 destination, float stoppingDistance = 0f)
        {
            if (!EnsureOnNavMesh())
            {
                return false;
            }

            agent.stoppingDistance = Mathf.Max(0f, stoppingDistance);
            return agent.SetDestination(destination);
        }

        public void Stop()
        {
            if (!IsOnNavMesh)
            {
                return;
            }

            agent.ResetPath();
        }

        private bool EnsureOnNavMesh()
        {
            if (agent == null)
            {
                return false;
            }

            if (agent.isOnNavMesh)
            {
                return true;
            }

            if (navMeshSnapDistance <= 0f
                || !NavMesh.SamplePosition(transform.position, out NavMeshHit hit, navMeshSnapDistance, NavMesh.AllAreas))
            {
                return false;
            }

            return agent.Warp(hit.position);
        }
    }
}
