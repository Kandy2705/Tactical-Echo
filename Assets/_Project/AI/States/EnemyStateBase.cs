using TacticalEcho.AI.Brain;
using TacticalEcho.Core.StateMachine;

namespace TacticalEcho.AI.States
{
    public abstract class EnemyStateBase : IState
    {
        protected EnemyStateBase(EnemyBrain brain)
        {
            Brain = brain;
        }

        protected EnemyBrain Brain { get; }

        public virtual void Enter() { }
        public virtual void Tick(float deltaTime) { }
        public virtual void Exit() { }
    }
}
