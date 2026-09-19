using TacticalEcho.AI.Brain;

namespace TacticalEcho.AI.States
{
    public sealed class DeadState : EnemyStateBase
    {
        public DeadState(EnemyBrain brain) : base(brain) { }

        public override void Enter()
        {
            Brain.Movement.Stop();
            Brain.Weapon.CancelReload();
            Brain.AnimationController.PlayDeath();
        }
    }
}
