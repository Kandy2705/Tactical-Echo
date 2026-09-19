using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace TacticalEcho.AI.Cover
{
    public sealed class CoverEvaluator : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float searchRadius = 15f;
        [SerializeField] private LayerMask obstructionMask = ~0;

        private readonly List<CoverPoint> coverPoints = new();

        public void SetCoverPoints(IEnumerable<CoverPoint> points)
        {
            coverPoints.Clear();
            coverPoints.AddRange(points);
        }

        public bool TryFindBestCover(Vector3 agentPosition, Vector3 threatPosition, out CoverPoint bestCover)
        {
            bestCover = null;
            float bestScore = float.MinValue;

            foreach (CoverPoint point in coverPoints)
            {
                if (Vector3.Distance(agentPosition, point.Position) > searchRadius)
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
