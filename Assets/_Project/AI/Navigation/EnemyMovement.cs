using TacticalEcho.AnimationSystem.Runtime;
using TacticalEcho.Combat.Damage;
using TacticalEcho.Combat.StatusEffects;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace TacticalEcho.AI.Navigation
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class EnemyMovement : MonoBehaviour
    {
        private const float InitialNavMeshSnapDistance = 8f;
        private static readonly Vector3 RuntimeNavMeshSize = new(120f, 30f, 120f);

        [SerializeField, Min(0f)] private float stoppingTolerance = 0.25f;
        [SerializeField, Min(0f)] private float navMeshSnapDistance = 2f;
        [SerializeField, Min(0f)] private float navMeshSnapVerticalTolerance = 1f;
        [SerializeField, Min(0f)] private float rotationSharpness = 12f;
        [SerializeField] private EnemyAnimationController animationController;
        [Tooltip("Optional. Status effects that modify movement speed. Resolved from this GameObject when empty.")]
        [SerializeField] private StatusEffectController statusEffects;

        private NavMeshAgent agent;
        private float baseSpeed = -1f;

        public bool IsOnNavMesh => agent != null && agent.isOnNavMesh;
        public bool HasPath => IsOnNavMesh && agent.hasPath;
        public bool HasReachedDestination => IsOnNavMesh
            && !agent.pathPending
            && agent.remainingDistance <= agent.stoppingDistance + stoppingTolerance;

        private void Awake()
        {
            ResolveAgent();
            ResolveAnimationController();
            EnsureNavMeshAvailable();
            TrySnapToNavMesh(InitialNavMeshSnapDistance);
        }

        private void EnsureNavMeshAvailable()
        {
            if (HasUsableNavMesh())
            {
                return;
            }

            NavMeshModifier selfModifier = GetComponent<NavMeshModifier>();
            bool createdModifier = selfModifier == null;
            if (createdModifier)
            {
                selfModifier = gameObject.AddComponent<NavMeshModifier>();
            }

            selfModifier.ignoreFromBuild = true;
            selfModifier.applyToChildren = true;

            GameObject surfaceObject = new("Runtime_NavMeshSurface");
            surfaceObject.hideFlags = HideFlags.DontSave;
            surfaceObject.transform.position = transform.position;

            NavMeshSurface surface = surfaceObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Volume;
            surface.size = RuntimeNavMeshSize;
            surface.center = new Vector3(0f, 5f, 0f);
            surface.layerMask = BuildNavigationLayerMask();
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.defaultArea = NavMesh.GetAreaFromName("Walkable");
            surface.ignoreNavMeshAgent = true;
            surface.ignoreNavMeshObstacle = true;
            surface.overrideTileSize = true;
            surface.tileSize = 128;

            try
            {
                surface.BuildNavMesh();
            }
            finally
            {
                if (createdModifier)
                {
                    Destroy(selfModifier);
                }
            }

            if (!HasUsableNavMesh())
            {
                Destroy(surfaceObject);
                Debug.LogWarning("No usable NavMesh was found or generated at runtime; this enemy will not be able to navigate. Bake a real NavMesh for the scene (Window > AI > Navigation) instead of relying on this fallback.", this);
            }
        }

        private static bool HasUsableNavMesh()
        {
            NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
            return triangulation.vertices != null && triangulation.vertices.Length >= 3;
        }

        private static LayerMask BuildNavigationLayerMask()
        {
            int mask = ~0;
            ExcludeLayer(ref mask, "Ignore Raycast");
            ExcludeLayer(ref mask, "Water");
            ExcludeLayer(ref mask, "UI");
            ExcludeLayer(ref mask, "Reflection_Probes");
            ExcludeLayer(ref mask, "AccessibleVolume");
            ExcludeLayer(ref mask, "PostProcessing");
            ExcludeLayer(ref mask, DamageHitZone.HitZoneLayerName);
            return mask;
        }

        private static void ExcludeLayer(ref int mask, string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer >= 0)
            {
                mask &= ~(1 << layer);
            }
        }

        private void Update()
        {
            ApplyStatusSpeedModifier();
            UpdateLocomotionAnimation();
        }

        private void ApplyStatusSpeedModifier()
        {
            if (agent == null)
            {
                return;
            }

            if (statusEffects == null)
            {
                statusEffects = GetComponent<StatusEffectController>();
                if (statusEffects == null)
                {
                    return;
                }
            }

            if (baseSpeed < 0f)
            {
                baseSpeed = agent.speed;
            }

            agent.speed = baseSpeed * statusEffects.MoveSpeedMultiplier;
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

            baseSpeed = Mathf.Max(0f, speed);
            agent.speed = baseSpeed;
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
            agent.isStopped = false;
            return agent.SetDestination(destinationHit.position);
        }

        public void Stop()
        {
            if (!IsOnNavMesh)
            {
                return;
            }

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
