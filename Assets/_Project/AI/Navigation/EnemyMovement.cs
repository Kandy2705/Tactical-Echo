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
            ResolveAgent();
        }

        public void ConfigureAgent(
            float speed,
            float acceleration,
            float radius,
            float height,
            bool updateRotation)
        {
            ResolveAgent();
            if (agent == null)
            {
                return;
            }

            agent.speed = Mathf.Max(0f, speed);
            agent.acceleration = Mathf.Max(0f, acceleration);
            agent.radius = Mathf.Max(0.05f, radius);
            agent.height = Mathf.Max(agent.radius * 2f, height);
            agent.updateRotation = updateRotation;
            agent.autoBraking = true;
            agent.autoRepath = true;
        }

        public bool TrySnapToNavMesh(float maxDistance = -1f)
        {
            ResolveAgent();
            if (agent == null)
            {
                return false;
            }

            if (agent.isOnNavMesh)
            {
                return true;
            }

            float sampleDistance = maxDistance >= 0f ? maxDistance : navMeshSnapDistance;
            if (sampleDistance <= 0f
                || !NavMesh.SamplePosition(transform.position, out NavMeshHit hit, sampleDistance, NavMesh.AllAreas))
            {
                return false;
            }

            return agent.Warp(hit.position);
        }

        public bool SetDestination(Vector3 destination, float stoppingDistance = 0f)
        {
            if (!TrySnapToNavMesh())
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

        private void ResolveAgent()
        {
            if (agent == null)
            {
                agent = GetComponent<NavMeshAgent>();
            }
        }
    }
}
