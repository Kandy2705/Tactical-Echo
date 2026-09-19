using TacticalEcho.AI.Brain;
using TacticalEcho.AI.States;
using UnityEngine;

namespace TacticalEcho.AI.TacticalActions
{
    public sealed class ShootAction : ITacticalAction
    {
        public TacticalActionId Id => TacticalActionId.Shoot;

        public bool CanExecute(in TacticalContext context)
        {
            return context.HasLineOfSight
                && context.TargetIsAlive
                && context.AmmoRatio > 0f
                && !context.IsReloading;
        }

        public float Score(in TacticalContext context)
        {
            float rangeUtility = Mathf.Lerp(0.25f, 1f, context.PreferredRangeScore);
            float ammoUtility = Mathf.Lerp(0.65f, 1f, context.AmmoRatio);
            float suppressionUtility = Mathf.Lerp(1f, 0.6f, context.Suppression);
            float fireReadinessUtility = context.CanFire ? 1f : 0.9f;

            return Mathf.Clamp01(
                rangeUtility
                * ammoUtility
                * suppressionUtility
                * fireReadinessUtility);
        }

        public void Execute(EnemyBrain brain, in TacticalContext context)
        {
            if (brain == null || brain.Weapon == null || !context.HasLineOfSight || !context.TargetIsAlive)
            {
                return;
            }

            Transform visibleTarget = brain.Vision != null ? brain.Vision.VisibleTarget : null;
            if (visibleTarget == null)
            {
                return;
            }

            Vector3 origin = brain.Vision != null && brain.Vision.EyeOrigin != null
                ? brain.Vision.EyeOrigin.position
                : brain.transform.position + Vector3.up * 1.5f;
            Vector3 targetPoint = visibleTarget.position + Vector3.up;
            Vector3 direction = targetPoint - origin;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            brain.Movement?.Stop();
            brain.Movement?.FacePosition(targetPoint);
            brain.AnimationController?.SetAim(true);

            if (brain.Weapon.TryFire(origin, direction.normalized, 0f, true))
            {
                brain.AnimationController?.PlayFire();
            }
        }
    }

    public sealed class AdvanceAction : ITacticalAction
    {
        private const float CombatStoppingDistance = 8f;

        public TacticalActionId Id => TacticalActionId.Advance;

        public bool CanExecute(in TacticalContext context)
        {
            return context.PathAvailable
                && context.HasTargetPosition
                && context.TooFarScore > 0.02f
                && !context.IsReloading;
        }

        public float Score(in TacticalContext context)
        {
            float suppressionUtility = Mathf.Lerp(1f, 0.55f, context.Suppression);
            float threatUtility = Mathf.Lerp(1f, 0.8f, context.Threat);
            return Mathf.Clamp01(context.TooFarScore * suppressionUtility * threatUtility * 0.95f);
        }

        public void Execute(EnemyBrain brain, in TacticalContext context)
        {
            if (brain == null || !context.HasTargetPosition)
            {
                return;
            }

            brain.AnimationController?.SetAim(context.HasLineOfSight);
            brain.Movement?.SetDestination(context.TargetPosition, CombatStoppingDistance);
        }
    }

    public sealed class TakeCoverAction : ITacticalAction
    {
        private const float CoverStoppingDistance = 0.4f;

        public TacticalActionId Id => TacticalActionId.TakeCover;

        public bool CanExecute(in TacticalContext context)
        {
            return context.PathAvailable
                && context.CoverAvailable
                && !context.IsReloading;
        }

        public float Score(in TacticalContext context)
        {
            float wounded = 1f - context.HealthRatio;
            return Mathf.Clamp01(
                context.Threat * 0.4f
                + context.Suppression * 0.4f
                + wounded * 0.4f
                + context.TooCloseScore * 0.1f);
        }

        public void Execute(EnemyBrain brain, in TacticalContext context)
        {
            if (brain == null || !context.CoverAvailable)
            {
                return;
            }

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

        public bool CanExecute(in TacticalContext context)
        {
            return context.PathAvailable
                && context.HasTargetPosition
                && !context.IsReloading;
        }

        public float Score(in TacticalContext context)
        {
            float rangePressure = 1f - context.PreferredRangeScore;
            return Mathf.Clamp01(
                0.1f
                + context.TooCloseScore * 0.55f
                + rangePressure * 0.15f
                + context.Threat * 0.15f
                + context.Suppression * 0.05f);
        }

        public void Execute(EnemyBrain brain, in TacticalContext context)
        {
            if (brain == null || !context.HasTargetPosition)
            {
                return;
            }

            Vector3 awayFromTarget = brain.transform.position - context.TargetPosition;
            awayFromTarget.y = 0f;
            if (awayFromTarget.sqrMagnitude <= 0.001f)
            {
                awayFromTarget = -brain.transform.forward;
            }

            awayFromTarget.Normalize();
            Vector3 lateral = Vector3.Cross(Vector3.up, awayFromTarget).normalized;
            float side = (brain.GetInstanceID() & 1) == 0 ? 1f : -1f;
            Vector3 destination = context.TargetPosition
                                  + awayFromTarget * DesiredRange
                                  + lateral * (LateralOffset * side);

            brain.AnimationController?.SetAim(context.HasLineOfSight);
            brain.Movement?.SetDestination(destination, RepositionStoppingDistance);
        }
    }

    public sealed class ReloadAction : ITacticalAction
    {
        public TacticalActionId Id => TacticalActionId.Reload;

        public bool CanExecute(in TacticalContext context)
        {
            return context.IsReloading || context.CanReload;
        }

        public float Score(in TacticalContext context)
        {
            if (context.IsReloading)
            {
                return 0.98f;
            }

            if (context.AmmoRatio <= 0.001f)
            {
                return 1f;
            }

            float ammoNeed = 1f - context.AmmoRatio;
            float exposureSafety = context.HasLineOfSight ? 0.55f : 1f;
            float coverBonus = context.CoverAvailable ? 0.15f : 0f;
            return Mathf.Clamp01(ammoNeed * (0.65f + exposureSafety * 0.2f + coverBonus));
        }

        public void Execute(EnemyBrain brain, in TacticalContext context)
        {
            if (brain == null || brain.Weapon == null)
            {
                return;
            }

            brain.Movement?.Stop();
            brain.AnimationController?.SetAim(false);

            if (context.IsReloading)
            {
                return;
            }

            if (brain.Weapon.TryBeginReload())
            {
                brain.AnimationController?.PlayReload();
            }
        }
    }

    public sealed class RetreatAction : ITacticalAction
    {
        private const float RetreatHealthThreshold = 0.45f;
        private const float CriticalHealthThreshold = 0.2f;

        public TacticalActionId Id => TacticalActionId.Retreat;

        public bool CanExecute(in TacticalContext context)
        {
            return context.PathAvailable
                && context.HasTargetPosition
                && context.HealthRatio < RetreatHealthThreshold;
        }

        public float Score(in TacticalContext context)
        {
            if (context.HealthRatio <= CriticalHealthThreshold)
            {
                return 1f;
            }

            float woundSeverity = Mathf.Clamp01(
                (RetreatHealthThreshold - context.HealthRatio)
                / (RetreatHealthThreshold - CriticalHealthThreshold));

            return Mathf.Clamp01(
                0.55f
                + woundSeverity * 0.25f
                + context.Threat * 0.15f
                + context.Suppression * 0.15f);
        }

        public void Execute(EnemyBrain brain, in TacticalContext context)
        {
            if (brain == null)
            {
                return;
            }

            brain.Weapon?.CancelReload();
            brain.AnimationController?.SetAim(false);
            brain.ChangeState(EnemyStateId.Retreat);
        }
    }
}
