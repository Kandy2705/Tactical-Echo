using TacticalEcho.AI.Brain;
using TacticalEcho.AI.TacticalActions;
using UnityEngine;

namespace TacticalEcho.AI.States
{
    public sealed class CombatState : EnemyStateBase
    {
        private const float FallbackStoppingDistance = 6f;

        public CombatState(EnemyBrain brain) : base(brain) { }

        public override void Enter()
        {
            TickCombat();
        }

        public override void Tick(float deltaTime)
        {
            TickCombat();
        }

        public override void Exit()
        {
            Brain.Movement?.Stop();
            Brain.AnimationController?.SetAim(false);
        }

        private void TickCombat()
        {
            if (Brain.TacticalEvaluator == null)
            {
                FollowVisibleTargetFallback();
                return;
            }

            TacticalContext context = Brain.BuildTacticalContext();
            ITacticalAction action = Brain.TacticalEvaluator.Evaluate(context);
            if (action == null)
            {
                Brain.Movement?.Stop();
                Brain.AnimationController?.SetAim(context.HasLineOfSight);
                return;
            }

            action.Execute(Brain, context);
        }

        private void FollowVisibleTargetFallback()
        {
            Transform visibleTarget = Brain.Vision != null ? Brain.Vision.VisibleTarget : null;
            if (visibleTarget == null)
            {
                Brain.Movement?.Stop();
                Brain.AnimationController?.SetAim(false);
                return;
            }

            Brain.AnimationController?.SetAim(true);
            Brain.Movement?.SetDestination(visibleTarget.position, FallbackStoppingDistance);
        }
    }
}
