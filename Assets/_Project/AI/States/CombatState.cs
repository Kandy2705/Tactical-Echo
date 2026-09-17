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
            TickCombat(forceEvaluation: true);
        }

        public override void Tick(float deltaTime)
        {
            TickCombat(forceEvaluation: false);
        }

        public override void Exit()
        {
            Brain.Movement?.Stop();
            Brain.AnimationController?.SetAim(false);
        }

        private void TickCombat(bool forceEvaluation)
        {
            if (Brain.TacticalEvaluator == null)
            {
                FollowVisibleTargetFallback();
                return;
            }

            TacticalContext context = Brain.BuildTacticalContext();
            ITacticalAction action = Brain.TacticalEvaluator.Evaluate(context, forceEvaluation);
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
