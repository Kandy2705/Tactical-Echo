using TacticalEcho.AI.Brain;
using UnityEngine;

namespace TacticalEcho.AI.States
{
    public sealed class SearchState : EnemyStateBase
    {
        private const float SearchStoppingDistance = 0.6f;
        private const float SearchTimeoutDuration = 6f;

        private Vector3 searchPosition;
        private bool hasSearchPosition;
        private float elapsedTime;

        public SearchState(EnemyBrain brain) : base(brain) { }

        public override void Enter()
        {
            elapsedTime = 0f;
            hasSearchPosition = Brain.Memory != null && Brain.Memory.HasKnownPosition;
            if (!hasSearchPosition)
            {
                // Nothing remembered to search around. Give up immediately instead of idling
                // in Search forever.
                Brain.ChangeState(EnemyStateId.Patrol);
                return;
            }

            // Snapshot remembered information only. Search must never follow the live
            // player Transform after line-of-sight has been lost.
            searchPosition = Brain.Memory.LastKnownPosition;
            Brain.Movement?.SetDestination(searchPosition, SearchStoppingDistance);
        }

        public override void Tick(float deltaTime)
        {
            if (!hasSearchPosition)
            {
                return;
            }

            if (Brain.Movement != null && Brain.Movement.HasReachedDestination)
            {
                Brain.Movement.Stop();
            }

            // Search is bounded: after SearchTimeoutDuration without re-acquiring the target,
            // the AI gives up and resumes Patrol instead of idling at the last known position
            // forever. This keeps the state observable/finite, matching the acceptance
            // criteria that Search must not run indefinitely.
            elapsedTime += deltaTime;
            if (elapsedTime >= SearchTimeoutDuration)
            {
                Brain.ChangeState(EnemyStateId.Patrol);
            }
        }

        public override void Exit()
        {
            hasSearchPosition = false;
            elapsedTime = 0f;
            Brain.Movement?.Stop();
        }
    }
}
