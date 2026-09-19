using System.Collections.Generic;
using UnityEngine;

namespace TacticalEcho.AI.Patrol
{
    
    
    
    
    
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
            if (index < 0 || index >= points.Count)
            {
                position = default;
                return false;
            }

            position = points[index].position;
            return true;
        }

        
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
                Gizmos.DrawWireSphere(points[i].position, 0.35f);

                int next = NextIndex(i);
                if (next >= 0 && next < points.Count && (loop || next > i))
                {
                    Gizmos.DrawLine(points[i].position, points[next].position);
                }
            }
        }
#endif
    }
}
