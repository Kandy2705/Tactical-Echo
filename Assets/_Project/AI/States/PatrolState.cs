using TacticalEcho.AI.Brain;
using TacticalEcho.AI.Patrol;
using UnityEngine;

namespace TacticalEcho.AI.States
{





    // Walks Brain.PatrolRoute waypoint by waypoint, dwelling at each one before advancing. If no
    // PatrolRoute is assigned (or it has no waypoints), this state degrades gracefully to just holding
    // position - it never falls back to wandering or picking its own destinations.
    public sealed class PatrolState : EnemyStateBase
    {
        private const float WaypointStoppingDistance = 0.5f;
        private const float WaypointDwellDuration = 2.5f;
        private const float RepathInterval = 1.5f;

        private int waypointIndex = -1;
        private float dwellRemaining;
        private float repathTimer;
        private bool isDwelling;

        public PatrolState(EnemyBrain brain) : base(brain) { }

        private PatrolRoute Route => Brain.PatrolRoute;

        public override void Enter()
        {
            isDwelling = false;
            dwellRemaining = 0f;
            repathTimer = 0f;

            // No authored route (or an empty one) - hold position instead of guessing a destination.
            if (Route == null || !Route.HasWaypoints)
            {
                waypointIndex = -1;
                Brain.Movement?.Stop();
                return;
            }



            waypointIndex = Route.GetNearestIndex(Brain.transform.position);
            MoveToCurrentWaypoint();
        }

        public override void Tick(float deltaTime)
        {
            if (waypointIndex < 0 || Brain.Movement == null)
            {
                return;
            }

            if (isDwelling)
            {
                dwellRemaining -= deltaTime;
                if (dwellRemaining <= 0f)
                {
                    AdvanceWaypoint();
                }

                return;
            }

            if (Brain.Movement.HasReachedDestination)
            {
                Brain.Movement.Stop();
                isDwelling = true;
                dwellRemaining = WaypointDwellDuration;
                return;
            }



            repathTimer += deltaTime;
            if (repathTimer >= RepathInterval)
            {
                repathTimer = 0f;
                if (!Brain.Movement.HasPath)
                {
                    MoveToCurrentWaypoint();
                }
            }
        }

        public override void Exit()
        {
            isDwelling = false;
            dwellRemaining = 0f;
            Brain.Movement?.Stop();
        }

        private void AdvanceWaypoint()
        {
            isDwelling = false;

            int next = Route != null ? Route.NextIndex(waypointIndex) : -1;
            if (next < 0)
            {

                waypointIndex = -1;
                return;
            }

            waypointIndex = next;
            MoveToCurrentWaypoint();
        }





        // Tries the current waypoint, then walks forward through the route (bounded by Route.Count so a
        // fully-unreachable route can't loop forever) until one waypoint accepts a NavMesh destination.
        private void MoveToCurrentWaypoint()
        {
            int attemptsLeft = Route != null ? Route.Count : 0;

            while (attemptsLeft-- > 0)
            {
                if (Route == null || !Route.TryGetWaypoint(waypointIndex, out Vector3 position))
                {
                    waypointIndex = -1;
                    return;
                }

                repathTimer = 0f;

                if (Brain.Movement == null || Brain.Movement.SetDestination(position, WaypointStoppingDistance))
                {
                    return;
                }

                int next = Route.NextIndex(waypointIndex);
                if (next < 0)
                {
                    waypointIndex = -1;
                    return;
                }

                waypointIndex = next;
            }

            waypointIndex = -1;
        }
    }
}
