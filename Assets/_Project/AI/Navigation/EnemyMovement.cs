using TacticalEcho.AnimationSystem.Runtime;
using UnityEngine;
using UnityEngine.AI;

namespace TacticalEcho.AI.Navigation
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class EnemyMovement : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float stoppingTolerance = 0.25f;
        [SerializeField, Min(0f)] private float navMeshSnapDistance = 2f;
        [SerializeField, Min(0f)] private float navMeshSnapVerticalTolerance = 1f;
        [SerializeField, Min(0f)] private float rotationSharpness = 12f;
        [SerializeField] private EnemyAnimationController animationController;

        private NavMeshAgent agent;

        public bool IsOnNavMesh => agent != null && agent.isOnNavMesh;
        public bool HasPath => IsOnNavMesh && agent.hasPath;
        public bool HasReachedDestination => IsOnNavMesh
            && !agent.pathPending
            && agent.remainingDistance <= agent.stoppingDistance + stoppingTolerance;

        private void Awake()
        {
            ResolveAgent();
            ResolveAnimationController();
        }

        private void Update()
        {
            UpdateLocomotionAnimation();
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

        public void ConfigureAnimation(EnemyAnimationController newAnimationController)
        {
            animationController = newAnimationController;
        }

        public bool TrySnapToNavMesh(float maxDistance = -1f)
        {
            ResolveAgent();
            if (agent == null)
            {
                return false;
            }

            if (agent.enabled && agent.isOnNavMesh)
            {
                return true;
            }

            float sampleDistance = maxDistance >= 0f ? maxDistance : navMeshSnapDistance;
            if (sampleDistance <= 0f
                || !TryFindNavMeshPosition(transform.position, sampleDistance, out NavMeshHit hit))
            {
                return false;
            }

            if (!agent.enabled)
            {
                agent.enabled = true;
            }

            return agent.Warp(hit.position);
        }

        public bool SetDestination(Vector3 destination, float stoppingDistance = 0f)
        {
            if (!TrySnapToNavMesh())
            {
                return false;
            }

            if (!TryFindNavMeshPosition(destination, navMeshSnapDistance, out NavMeshHit destinationHit))
            {
                return false;
            }

            agent.stoppingDistance = Mathf.Max(0f, stoppingDistance);

            // Stop() parks the agent with isStopped, so any new destination has to release
            // it again - otherwise the first Stop() would freeze the enemy permanently.
            agent.isStopped = false;
            return agent.SetDestination(destinationHit.position);
        }

        public void Stop()
        {
            if (!IsOnNavMesh)
            {
                return;
            }

            // ResetPath() alone only clears the route: the agent keeps its current velocity
            // and coasts to a halt, which reads as a corpse sliding away from where it fell.
            agent.ResetPath();
            agent.velocity = Vector3.zero;
            agent.isStopped = true;
        }

        public void FacePosition(Vector3 worldPosition)
        {
            Vector3 direction = Vector3.ProjectOnPlane(worldPosition - transform.position, Vector3.up);
            if (direction.sqrMagnitude <= 0.001f)
            {
                return;
            }

            Quaternion desiredRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            float t = rotationSharpness <= 0f
                ? 1f
                : 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, t);
        }

        private bool TryFindNavMeshPosition(Vector3 sourcePosition, float maxDistance, out NavMeshHit hit)
        {
            if (NavMesh.SamplePosition(sourcePosition, out hit, maxDistance, NavMesh.AllAreas)
                && Mathf.Abs(hit.position.y - sourcePosition.y) <= navMeshSnapVerticalTolerance)
            {
                return true;
            }

            return TryFindSameElevationNavMeshPosition(sourcePosition, maxDistance, out hit);
        }

        private bool TryFindSameElevationNavMeshPosition(Vector3 sourcePosition, float maxDistance, out NavMeshHit hit)
        {
            hit = default;

            NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
            Vector3 closestVertex = default;
            float closestPlanarDistance = float.PositiveInfinity;

            foreach (Vector3 vertex in triangulation.vertices)
            {
                if (Mathf.Abs(vertex.y - sourcePosition.y) > navMeshSnapVerticalTolerance)
                {
                    continue;
                }

                Vector3 planarOffset = Vector3.ProjectOnPlane(vertex - sourcePosition, Vector3.up);
                float planarDistance = planarOffset.magnitude;
                if (planarDistance > maxDistance || planarDistance >= closestPlanarDistance)
                {
                    continue;
                }

                closestVertex = vertex;
                closestPlanarDistance = planarDistance;
            }

            return !float.IsPositiveInfinity(closestPlanarDistance)
                && NavMesh.SamplePosition(closestVertex, out hit, 0.1f, NavMesh.AllAreas)
                && Mathf.Abs(hit.position.y - sourcePosition.y) <= navMeshSnapVerticalTolerance;
        }

        private void ResolveAgent()
        {
            if (agent == null)
            {
                agent = GetComponent<NavMeshAgent>();
            }
        }

        private void ResolveAnimationController()
        {
            if (animationController == null)
            {
                animationController = GetComponent<EnemyAnimationController>();
            }
        }

        private void UpdateLocomotionAnimation()
        {
            ResolveAnimationController();
            if (animationController == null)
            {
                return;
            }

            float normalizedSpeed = 0f;
            if (IsOnNavMesh && agent.speed > 0.001f)
            {
                Vector3 planarVelocity = Vector3.ProjectOnPlane(agent.velocity, Vector3.up);
                normalizedSpeed = Mathf.Clamp01(planarVelocity.magnitude / agent.speed);
            }

            animationController.SetLocomotion(normalizedSpeed);
        }
    }
}
