using TacticalEcho.AI.Brain;
using UnityEngine;

namespace TacticalEcho.AI.States
{





    public sealed class SearchState : EnemyStateBase
    {
        private const float SearchStoppingDistance = 0.6f;
        private const float SearchTimeoutDuration = 10f;
        private const float SweepRadius = 5f;
        private const float PointDwellDuration = 1.1f;
        private const int SweepPointCount = 4;



        private readonly Vector3[] sweepPoints = new Vector3[SweepPointCount + 1];

        private int pointCount;
        private int pointIndex;
        private float elapsedTime;
        private float dwellRemaining;
        private bool isDwelling;
        private bool hasSearchPosition;

        public SearchState(EnemyBrain brain) : base(brain) { }

        public override void Enter()
        {
            elapsedTime = 0f;
            dwellRemaining = 0f;
            isDwelling = false;
            pointIndex = 0;
            pointCount = 0;

            hasSearchPosition = Brain.Memory != null && Brain.Memory.HasKnownPosition;
            if (!hasSearchPosition)
            {


                Brain.ChangeState(EnemyStateId.Patrol);
                return;
            }


            BuildSweep(Brain.Memory.LastKnownPosition);
            MoveToCurrentPoint();
        }

        public override void Tick(float deltaTime)
        {
            if (!hasSearchPosition)
            {
                return;
            }



            elapsedTime += deltaTime;
            if (elapsedTime >= SearchTimeoutDuration)
            {
                Brain.ChangeState(EnemyStateId.Patrol);
                return;
            }

            if (Brain.Movement == null)
            {
                return;
            }

            if (isDwelling)
            {
                dwellRemaining -= deltaTime;



                if (pointIndex + 1 < pointCount)
                {
                    Brain.Movement.FacePosition(sweepPoints[pointIndex + 1]);
                }

                if (dwellRemaining <= 0f)
                {
                    isDwelling = false;
                    pointIndex++;
                    MoveToCurrentPoint();
                }

                return;
            }

            if (Brain.Movement.HasReachedDestination)
            {
                Brain.Movement.Stop();
                isDwelling = true;
                dwellRemaining = PointDwellDuration;
            }
        }

        public override void Exit()
        {
            hasSearchPosition = false;
            isDwelling = false;
            elapsedTime = 0f;
            pointCount = 0;
            pointIndex = 0;
            Brain.Movement?.Stop();
        }





        private void BuildSweep(Vector3 origin)
        {
            sweepPoints[0] = origin;
            pointCount = 1;

            float startAngle = Mathf.Repeat(origin.x + origin.z, 360f);
            for (int i = 0; i < SweepPointCount; i++)
            {
                float angle = (startAngle + i * (360f / SweepPointCount)) * Mathf.Deg2Rad;
                sweepPoints[pointCount++] = origin + new Vector3(
                    Mathf.Cos(angle) * SweepRadius,
                    0f,
                    Mathf.Sin(angle) * SweepRadius);
            }
        }





        private void MoveToCurrentPoint()
        {
            while (pointIndex < pointCount)
            {
                if (Brain.Movement == null
                    || Brain.Movement.SetDestination(sweepPoints[pointIndex], SearchStoppingDistance))
                {
                    return;
                }

                pointIndex++;
            }

            Brain.ChangeState(EnemyStateId.Patrol);
        }
    }
}
