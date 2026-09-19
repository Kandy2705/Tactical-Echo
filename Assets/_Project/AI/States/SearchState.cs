using TacticalEcho.AI.Brain;
using UnityEngine;

namespace TacticalEcho.AI.States
{
    /// <summary>
    /// Sweeps the area around the last remembered position instead of standing on it.
    /// Every point searched is derived from the memory snapshot taken on Enter; the live
    /// player Transform is never read after line of sight is lost.
    /// </summary>
    public sealed class SearchState : EnemyStateBase
    {
        private const float SearchStoppingDistance = 0.6f;
        private const float SearchTimeoutDuration = 10f;
        private const float SweepRadius = 5f;
        private const float PointDwellDuration = 1.1f;
        private const int SweepPointCount = 4;

        // Allocated once per state instance, filled on Enter. Search runs every time contact
        // is lost, so the sweep must not allocate a fresh array each time it starts.
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

            hasSearchPosition = Brain.Memory.HasKnownPosition;
            if (!hasSearchPosition)
            {
                // Nothing remembered to search around. Give up immediately instead of idling
                // in Search forever.
                Brain.ChangeState(EnemyStateId.Patrol);
                return;
            }

            // Snapshot remembered information only, then derive the whole sweep from it.
            BuildSweep(Brain.Memory.LastKnownPosition);
            MoveToCurrentPoint();
        }

        public override void Tick(float deltaTime)
        {
            if (!hasSearchPosition)
            {
                return;
            }

            // Search is bounded: after SearchTimeoutDuration without re-acquiring the target
            // the AI gives up and resumes Patrol instead of sweeping forever.
            elapsedTime += deltaTime;
            if (elapsedTime >= SearchTimeoutDuration)
            {
                Brain.ChangeState(EnemyStateId.Patrol);
                return;
            }

            if (isDwelling)
            {
                dwellRemaining -= deltaTime;

                // Look toward the next point while pausing, so the sweep reads as searching
                // rather than as standing still.
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
            Brain.Movement.Stop();
        }

        /// <summary>
        /// The remembered position first, then a ring around it. The ring is deterministic so
        /// the sweep is reproducible when debugging a recorded run.
        /// </summary>
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

        /// <summary>
        /// Skips sweep points that cannot be reached; when none are left the sweep is over and
        /// the timeout in Tick returns the AI to Patrol.
        /// </summary>
        private void MoveToCurrentPoint()
        {
            while (pointIndex < pointCount)
            {
                if (Brain.Movement.SetDestination(sweepPoints[pointIndex], SearchStoppingDistance))
                {
                    return;
                }

                pointIndex++;
            }

            Brain.ChangeState(EnemyStateId.Patrol);
        }
    }
}
