using System.Collections.Generic;
using UnityEngine;

namespace TacticalEcho.AI.Patrol
{
    /// <summary>
    /// Authored patrol path: an ordered set of waypoints in the scene, like
    /// <see cref="Cover.CoverPoint"/> is authored cover. Route data lives here rather than on
    /// EnemyBrain so several enemies can share one path and so the brain stays a coordinator.
    /// </summary>
    public sealed class PatrolRoute : MonoBehaviour
    {
        [Tooltip("Ordered waypoints. Leave empty to use this object's direct children as the route.")]
        [SerializeField] private List<Transform> waypoints = new();
        [SerializeField] private bool loop = true;
        [SerializeField] private Color gizmoColor = new(0.35f, 0.85f, 1f, 0.9f);

        private readonly List<Transform> resolved = new();

        public bool HasWaypoints => ResolvedWaypoints.Count > 0;
        public int Count => ResolvedWaypoints.Count;
        public bool Loop => loop;

        private IReadOnlyList<Transform> ResolvedWaypoints
        {
            get
            {
                if (waypoints.Count > 0)
                {
                    return waypoints;
                }

                // Children as an implicit route: drop empty markers under the route object and
                // it works with no list wiring.
                if (resolved.Count != transform.childCount)
                {
                    resolved.Clear();
                    for (int i = 0; i < transform.childCount; i++)
                    {
                        resolved.Add(transform.GetChild(i));
                    }
                }

                return resolved;
            }
        }

        public bool TryGetWaypoint(int index, out Vector3 position)
        {
            IReadOnlyList<Transform> points = ResolvedWaypoints;
            if (index < 0 || index >= points.Count || points[index] == null)
            {
                position = default;
                return false;
            }

            position = points[index].position;
            return true;
        }

        /// <summary>Next index, or -1 when a non-looping route has been walked to its end.</summary>
        public int NextIndex(int index)
        {
            int count = ResolvedWaypoints.Count;
            if (count == 0)
            {
                return -1;
            }

            int next = index + 1;
            if (next < count)
            {
                return next;
            }

            return loop ? 0 : -1;
        }

        public int GetNearestIndex(Vector3 position)
        {
            IReadOnlyList<Transform> points = ResolvedWaypoints;
            int nearest = -1;
            float nearestSqr = float.PositiveInfinity;

            for (int i = 0; i < points.Count; i++)
            {
                if (points[i] == null)
                {
                    continue;
                }

                float sqr = (points[i].position - position).sqrMagnitude;
                if (sqr < nearestSqr)
                {
                    nearestSqr = sqr;
                    nearest = i;
                }
            }

            return nearest;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            IReadOnlyList<Transform> points = ResolvedWaypoints;
            Gizmos.color = gizmoColor;

            for (int i = 0; i < points.Count; i++)
            {
                if (points[i] == null)
                {
                    continue;
                }

                Gizmos.DrawWireSphere(points[i].position, 0.35f);

                int next = NextIndex(i);
                if (next >= 0 && next < points.Count && points[next] != null && (loop || next > i))
                {
                    Gizmos.DrawLine(points[i].position, points[next].position);
                }
            }
        }
#endif
    }
}
