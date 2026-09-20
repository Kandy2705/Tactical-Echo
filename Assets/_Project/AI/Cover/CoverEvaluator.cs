using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace TacticalEcho.AI.Cover
{
    // Validates and scores CoverPoint candidates for one enemy; does not decide *when* to take cover
    // (TakeCoverAction owns that) or move to it (EnemyMovement does). A cover point counts only if it is
    // within range, NavMesh-reachable and not exposed to the threat's line of sight.
    public sealed class CoverEvaluator : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float searchRadius = 15f;
        [SerializeField] private LayerMask obstructionMask = ~0;

        private readonly List<CoverPoint> coverPoints = new();

        // Falls back to every CoverPoint in the scene when nothing was explicitly assigned via
        // SetCoverPoints(). NOTE: if the scene has zero authored CoverPoint instances (as of this writing,
        // it does), this list stays empty and TryFindBestCover() always returns false - TakeCoverAction is
        // fully implemented but has nothing to find until CoverPoint objects are placed.
        private void Awake()
        {
            if (coverPoints.Count == 0)
            {
                coverPoints.AddRange(FindObjectsByType<CoverPoint>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
            }
        }

        public void SetCoverPoints(IEnumerable<CoverPoint> points)
        {
            coverPoints.Clear();
            if (points == null)
            {
                return;
            }

            foreach (CoverPoint point in points)
            {
                if (point != null)
                {
                    coverPoints.Add(point);
                }
            }
        }

        // Picks the closest valid cover point to the agent (score is simply -travelDistance) rather than the
        // best angle/concealment; refine scoring here if cover selection needs to weigh more than proximity.
        public bool TryFindBestCover(Vector3 agentPosition, Vector3 threatPosition, out CoverPoint bestCover)
        {
            bestCover = null;
            float bestScore = float.MinValue;

            foreach (CoverPoint point in coverPoints)
            {
                if (point == null || Vector3.Distance(agentPosition, point.Position) > searchRadius)
                {
                    continue;
                }

                if (!NavMesh.SamplePosition(point.Position, out NavMeshHit navHit, 1f, NavMesh.AllAreas))
                {
                    continue;
                }

                Vector3 toThreat = threatPosition - point.Position;
                if (toThreat.sqrMagnitude <= 0.001f)
                {
                    continue;
                }

                bool isExposed = Physics.Raycast(
                    point.Position + Vector3.up,
                    toThreat.normalized,
                    toThreat.magnitude,
                    obstructionMask,
                    QueryTriggerInteraction.Ignore) == false;
                if (isExposed)
                {
                    continue;
                }

                float travelPenalty = Vector3.Distance(agentPosition, navHit.position);
                float score = -travelPenalty;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestCover = point;
                }
            }

            return bestCover != null;
        }
    }
}
