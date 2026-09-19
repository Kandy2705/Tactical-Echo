using TacticalEcho.AI.Brain;
using TacticalEcho.AI.Patrol;
using UnityEngine;

namespace TacticalEcho.AI.States
{
    /// <summary>
    /// Walks the authored <see cref="PatrolRoute"/>, pausing at each waypoint. With no route
    /// assigned the enemy simply holds position - Patrol is the resting state, so it must be
    /// safe to enter with nothing authored.
    /// </summary>
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

            if (Route == null || !Route.HasWaypoints)
            {
                waypointIndex = -1;
                Brain.Movement?.Stop();
                return;
            }

            // Resume from the closest waypoint so returning from Combat or Search does not
            // send the enemy back to the start of the route.
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

            // A destination can be lost if the agent was warped or the NavMesh rebuilt, which
            // would otherwise leave the enemy standing still in Patrol forever.
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
                // End of a non-looping route: hold here rather than restarting.
                waypointIndex = -1;
                return;
            }

            waypointIndex = next;
            MoveToCurrentWaypoint();
        }

        /// <summary>
        /// Skips waypoints that cannot be pathed to, but only as many times as there are
        /// waypoints, so a route where nothing is reachable stops instead of recursing.
        /// </summary>
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
