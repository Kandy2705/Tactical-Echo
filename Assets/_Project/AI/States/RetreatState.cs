using TacticalEcho.AI.Brain;
using UnityEngine;

namespace TacticalEcho.AI.States
{
    public sealed class RetreatState : EnemyStateBase
    {
        private const float RetreatDistance = 8f;
        private const float RetreatStoppingDistance = 0.75f;

        private bool hasRetreatDestination;

        public RetreatState(EnemyBrain brain) : base(brain) { }

        public override void Enter()
        {
            Brain.AnimationController.SetAim(false);

            if (!Brain.Memory.HasKnownPosition)
            {
                FinishRetreat();
                return;
            }

            Vector3 awayFromThreat = Brain.transform.position - Brain.Memory.LastKnownPosition;
            awayFromThreat.y = 0f;
            if (awayFromThreat.sqrMagnitude <= 0.001f)
            {
                awayFromThreat = -Brain.transform.forward;
            }

            Vector3 destination = Brain.transform.position + awayFromThreat.normalized * RetreatDistance;
            hasRetreatDestination = Brain.Movement.SetDestination(destination, RetreatStoppingDistance);

            if (!hasRetreatDestination)
            {
                FinishRetreat();
            }
        }

        public override void Tick(float deltaTime)
        {
            if (!hasRetreatDestination)
            {
                return;
            }

            if (Brain.Movement.HasReachedDestination)
            {
                Brain.Movement.Stop();
                hasRetreatDestination = false;
                FinishRetreat();
            }
        }

        public override void Exit()
        {
            hasRetreatDestination = false;
            Brain.Movement.Stop();
        }

        private void FinishRetreat()
        {
            if (Brain.Vision.HasLineOfSight && Brain.Vision.VisibleTarget != null)
            {
                Brain.ChangeState(EnemyStateId.Combat);
                return;
            }

            if (Brain.Memory.HasKnownPosition)
            {
                Brain.ChangeState(EnemyStateId.Search);
                return;
            }

            Brain.ChangeState(EnemyStateId.Patrol);
        }
    }
}
