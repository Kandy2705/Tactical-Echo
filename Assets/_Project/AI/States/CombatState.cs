using TacticalEcho.AI.Brain;
using UnityEngine;

namespace TacticalEcho.AI.States
{
    public sealed class CombatState : EnemyStateBase
    {
        private const float FollowStoppingDistance = 6f;

        public CombatState(EnemyBrain brain) : base(brain) { }

        public override void Enter()
        {
            FollowVisibleTarget();
        }

        public override void Tick(float deltaTime)
        {
            FollowVisibleTarget();
        }

        public override void Exit()
        {
            Brain.Movement?.Stop();
        }

        private void FollowVisibleTarget()
        {
            Transform visibleTarget = Brain.Vision != null ? Brain.Vision.VisibleTarget : null;
            if (visibleTarget == null)
            {
                Brain.Movement?.Stop();
                return;
            }

            Brain.Movement?.SetDestination(visibleTarget.position, FollowStoppingDistance);
        }
    }
}
