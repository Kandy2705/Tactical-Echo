using TacticalEcho.AI.Brain;
using TacticalEcho.AI.States;
using UnityEngine;

namespace TacticalEcho.AI.TacticalActions
{
    public sealed class ShootAction : ITacticalAction
    {
        public TacticalActionId Id => TacticalActionId.Shoot;
        public bool CanExecute(in TacticalContext context) => context.HasLineOfSight && context.AmmoRatio > 0f && !context.IsReloading;

        public float Score(in TacticalContext context)
        {
            if (!CanExecute(context)) return 0f;
            float rangeScore = 0.45f + context.PreferredRangeScore * 0.55f;
            float ammoConfidence = Mathf.Lerp(0.65f, 1f, context.AmmoRatio);
            return Mathf.Clamp01(rangeScore * ammoConfidence);
        }

        public void Execute(EnemyBrain brain, in TacticalContext context)
        {
            if (brain == null || brain.Weapon == null || !context.HasLineOfSight) return;
            Transform visibleTarget = brain.Vision != null ? brain.Vision.VisibleTarget : null;
            if (visibleTarget == null) return;

            Vector3 origin = brain.Vision != null && brain.Vision.EyeOrigin != null
                ? brain.Vision.EyeOrigin.position
                : brain.transform.position + Vector3.up * 1.5f;
            Vector3 targetPoint = visibleTarget.position + Vector3.up;
            Vector3 direction = targetPoint - origin;
            if (direction.sqrMagnitude <= 0.0001f) return;

            brain.Movement?.Stop();
            brain.Movement?.FacePosition(targetPoint);
            brain.AnimationController?.SetAim(true);
            if (brain.Weapon.TryFire(origin, direction.normalized, 0f, true)) brain.AnimationController?.PlayFire();
        }
    }

    public sealed class AdvanceAction : ITacticalAction
    {
        private const float CombatStoppingDistance = 8f;
        public TacticalActionId Id => TacticalActionId.Advance;
        public bool CanExecute(in TacticalContext context) => context.PathAvailable && context.HasTargetPosition && !context.IsReloading;
        public float Score(in TacticalContext context) => CanExecute(context)
            ? Mathf.Clamp01(1f - context.PreferredRangeScore) * Mathf.Clamp01(1f - context.Suppression) * 0.75f
            : 0f;
        public void Execute(EnemyBrain brain, in TacticalContext context)
        {
            if (brain == null || !context.HasTargetPosition) return;
            brain.AnimationController?.SetAim(context.HasLineOfSight);
            brain.Movement?.SetDestination(context.TargetPosition, CombatStoppingDistance);
        }
    }

    public sealed class TakeCoverAction : ITacticalAction
    {
        private const float CoverStoppingDistance = 0.4f;
        public TacticalActionId Id => TacticalActionId.TakeCover;
        public bool CanExecute(in TacticalContext context) => context.PathAvailable && context.CoverAvailable && !context.IsReloading;
        public float Score(in TacticalContext context) => CanExecute(context)
            ? Mathf.Clamp01(context.Threat * 0.45f + context.Suppression * 0.35f + (1f - context.HealthRatio) * 0.55f)
            : 0f;
        public void Execute(EnemyBrain brain, in TacticalContext context)
        {
            if (brain == null || !context.CoverAvailable) return;
            brain.AnimationController?.SetAim(false);
            brain.Movement?.SetDestination(context.CoverPosition, CoverStoppingDistance);
        }
    }

    public sealed class RepositionAction : ITacticalAction
    {
        private const float DesiredRange = 10f;
        private const float LateralOffset = 3.5f;
        private const float RepositionStoppingDistance = 0.5f;
        public TacticalActionId Id => TacticalActionId.Reposition;
        public bool CanExecute(in TacticalContext context) => context.PathAvailable && context.HasTargetPosition && !context.IsReloading;

        public float Score(in TacticalContext context)
        {
            if (!CanExecute(context)) return 0f;
            float score = 0.2f + context.Threat * 0.15f + (1f - context.PreferredRangeScore) * 0.35f;
            return Mathf.Clamp01(score) * Mathf.Clamp01(1f - context.Suppression * 0.5f);
        }

        public void Execute(EnemyBrain brain, in TacticalContext context)
        {
            if (brain == null || !context.HasTargetPosition) return;
            Vector3 awayFromTarget = brain.transform.position - context.TargetPosition;
            awayFromTarget.y = 0f;
            if (awayFromTarget.sqrMagnitude <= 0.001f) awayFromTarget = -brain.transform.forward;
            awayFromTarget.Normalize();
            Vector3 lateral = Vector3.Cross(Vector3.up, awayFromTarget).normalized;
            float side = (brain.GetInstanceID() & 1) == 0 ? 1f : -1f;
            Vector3 destination = context.TargetPosition + awayFromTarget * DesiredRange + lateral * (LateralOffset * side);
            brain.AnimationController?.SetAim(context.HasLineOfSight);
            brain.Movement?.SetDestination(destination, RepositionStoppingDistance);
        }
    }

    public sealed class ReloadAction : ITacticalAction
    {
        public TacticalActionId Id => TacticalActionId.Reload;
        public bool CanExecute(in TacticalContext context) => context.CanReload && !context.IsReloading;

        public float Score(in TacticalContext context)
        {
            if (!CanExecute(context)) return 0f;
            if (context.AmmoRatio <= 0.001f) return 1.15f;
            float safety = context.CoverAvailable ? 0.9f : 0.45f;
            return Mathf.Clamp01((1f - context.AmmoRatio) * safety);
        }

        public void Execute(EnemyBrain brain, in TacticalContext context)
        {
            if (brain == null || brain.Weapon == null) return;
            brain.Movement?.Stop();
            brain.AnimationController?.SetAim(false);
            if (brain.Weapon.TryBeginReload()) brain.AnimationController?.PlayReload();
        }
    }

    public sealed class RetreatAction : ITacticalAction
    {
        private const float RetreatHealthThreshold = 0.5f;
        private const float CriticalHealthThreshold = 0.2f;
        public TacticalActionId Id => TacticalActionId.Retreat;
        public bool CanExecute(in TacticalContext context) => context.PathAvailable && context.HasTargetPosition && context.HealthRatio < RetreatHealthThreshold;

        public float Score(in TacticalContext context)
        {
            if (!CanExecute(context)) return 0f;
            if (context.HealthRatio <= CriticalHealthThreshold) return 1.25f;
            return Mathf.Clamp01((1f - context.HealthRatio) * 0.75f + context.Threat * 0.25f + context.Suppression * 0.25f);
        }

        public void Execute(EnemyBrain brain, in TacticalContext context)
        {
            if (brain == null) return;
            brain.AnimationController?.SetAim(false);
            brain.ChangeState(EnemyStateId.Retreat);
        }
    }
}
