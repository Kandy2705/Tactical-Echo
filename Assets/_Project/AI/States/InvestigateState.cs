using TacticalEcho.AI.Brain;

namespace TacticalEcho.AI.States
{
    public sealed class InvestigateState : EnemyStateBase
    {
        private const float InvestigateStoppingDistance = 0.75f;

        public InvestigateState(EnemyBrain brain) : base(brain) { }

        public override void Enter()
        {
            MoveToLastHeardPosition();
        }

        public override void Tick(float deltaTime)
        {
            if (Brain.Movement == null)
            {
                return;
            }

            if (Brain.Movement.HasReachedDestination)
            {
                Brain.Movement.Stop();
                Brain.ChangeState(EnemyStateId.Search);
            }
        }

        public override void Exit()
        {
            Brain.Movement?.Stop();
        }

        private void MoveToLastHeardPosition()
        {
            if (Brain.Memory == null || !Brain.Memory.HasHeardPosition)
            {
                return;
            }

            Brain.Movement?.SetDestination(
                Brain.Memory.LastHeardPosition,
                InvestigateStoppingDistance);
        }
    }
}
