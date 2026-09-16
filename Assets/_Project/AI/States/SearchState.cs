using TacticalEcho.AI.Brain;
using UnityEngine;

namespace TacticalEcho.AI.States
{
    public sealed class SearchState : EnemyStateBase
    {
        private const float SearchStoppingDistance = 0.6f;

        private Vector3 searchPosition;
        private bool hasSearchPosition;

        public SearchState(EnemyBrain brain) : base(brain) { }

        public override void Enter()
        {
            hasSearchPosition = Brain.Memory != null && Brain.Memory.HasKnownPosition;
            if (!hasSearchPosition)
            {
                return;
            }

            // Snapshot remembered information only. Search must never follow the live
            // player Transform after line-of-sight has been lost.
            searchPosition = Brain.Memory.LastKnownPosition;
            Brain.Movement?.SetDestination(searchPosition, SearchStoppingDistance);
        }

        public override void Tick(float deltaTime)
        {
            if (!hasSearchPosition || Brain.Movement == null)
            {
                return;
            }

            if (Brain.Movement.HasReachedDestination)
            {
                Brain.Movement.Stop();
            }
        }

        public override void Exit()
        {
            Brain.Movement?.Stop();
        }
    }
}
