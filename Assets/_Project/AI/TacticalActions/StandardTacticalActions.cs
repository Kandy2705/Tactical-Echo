using TacticalEcho.AI.Brain;
using UnityEngine;

namespace TacticalEcho.AI.TacticalActions
{
    public sealed class ShootAction : ITacticalAction
    {
        public TacticalActionId Id => TacticalActionId.Shoot;
        public bool CanExecute(in TacticalContext context) => context.HasLineOfSight && context.AmmoRatio > 0f;
        public float Score(in TacticalContext context) => context.HasLineOfSight ? context.AmmoRatio * context.PreferredRangeScore : 0f;
        public void Execute(EnemyBrain brain, in TacticalContext context) { }
    }

    public sealed class AdvanceAction : ITacticalAction
    {
        public TacticalActionId Id => TacticalActionId.Advance;
        public bool CanExecute(in TacticalContext context) => context.PathAvailable;
        public float Score(in TacticalContext context) => context.PathAvailable ? Mathf.Clamp01(1f - context.PreferredRangeScore) * (1f - context.Suppression) : 0f;
        public void Execute(EnemyBrain brain, in TacticalContext context) { }
    }

    public sealed class TakeCoverAction : ITacticalAction
    {
        public TacticalActionId Id => TacticalActionId.TakeCover;
        public bool CanExecute(in TacticalContext context) => context.CoverAvailable;
        public float Score(in TacticalContext context) => context.CoverAvailable ? Mathf.Clamp01(context.Threat + context.Suppression + (1f - context.HealthRatio)) / 3f : 0f;
        public void Execute(EnemyBrain brain, in TacticalContext context) { }
    }

    public sealed class RepositionAction : ITacticalAction
    {
        public TacticalActionId Id => TacticalActionId.Reposition;
        public bool CanExecute(in TacticalContext context) => context.PathAvailable;
        public float Score(in TacticalContext context) => context.PathAvailable ? Mathf.Clamp01(context.Threat * 0.5f + (1f - context.PreferredRangeScore) * 0.5f) : 0f;
        public void Execute(EnemyBrain brain, in TacticalContext context) { }
    }

    public sealed class ReloadAction : ITacticalAction
    {
        public TacticalActionId Id => TacticalActionId.Reload;
        public bool CanExecute(in TacticalContext context) => context.AmmoRatio < 1f;
        public float Score(in TacticalContext context) => Mathf.Clamp01((1f - context.AmmoRatio) * (context.CoverAvailable ? 1f : 0.35f));
        public void Execute(EnemyBrain brain, in TacticalContext context) { }
    }

    public sealed class RetreatAction : ITacticalAction
    {
        public TacticalActionId Id => TacticalActionId.Retreat;
        public bool CanExecute(in TacticalContext context) => context.PathAvailable;
        public float Score(in TacticalContext context) => context.PathAvailable ? Mathf.Clamp01((1f - context.HealthRatio) * context.Threat) : 0f;
        public void Execute(EnemyBrain brain, in TacticalContext context) { }
    }
}
