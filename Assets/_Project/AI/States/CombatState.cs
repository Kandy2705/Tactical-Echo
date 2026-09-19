using TacticalEcho.AI.Brain;
using TacticalEcho.AI.TacticalActions;

namespace TacticalEcho.AI.States
{
    public sealed class CombatState : EnemyStateBase
    {
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
            Brain.Movement.Stop();
            Brain.AnimationController.SetAim(false);
        }

        private void TickCombat(bool forceEvaluation)
        {
            TacticalContext context = Brain.BuildTacticalContext();
            ITacticalAction action = Brain.TacticalEvaluator.Evaluate(context, forceEvaluation);
            if (action == null)
            {
                Brain.Movement.Stop();
                Brain.AnimationController.SetAim(context.HasLineOfSight);
                return;
            }

            action.Execute(Brain, context);
        }

    }
}
