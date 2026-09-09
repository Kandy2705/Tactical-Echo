using TacticalEcho.AI.Brain;

namespace TacticalEcho.AI.TacticalActions
{
    public interface ITacticalAction
    {
        TacticalActionId Id { get; }
        bool CanExecute(in TacticalContext context);
        float Score(in TacticalContext context);
        void Execute(EnemyBrain brain, in TacticalContext context);
    }
}
