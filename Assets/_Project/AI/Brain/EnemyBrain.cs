using TacticalEcho.AI.Memory;
using TacticalEcho.AI.Navigation;
using TacticalEcho.AI.Perception;
using TacticalEcho.AI.States;
using TacticalEcho.AI.TacticalActions;
using TacticalEcho.Combat.Health;
using TacticalEcho.Combat.Weapons;
using TacticalEcho.Core.Events;
using TacticalEcho.Core.StateMachine;
using UnityEngine;

namespace TacticalEcho.AI.Brain
{
    public sealed class EnemyBrain : MonoBehaviour
    {
        [Header("Perception")]
        [SerializeField] private VisionSensor vision;
        [SerializeField] private HearingSensor hearing;
        [SerializeField] private EnemyMemory memory;

        [Header("Execution")]
        [SerializeField] private EnemyMovement movement;
        [SerializeField] private WeaponController weapon;
        [SerializeField] private Health health;

        [Header("Decision")]
        [SerializeField] private TacticalEvaluator tacticalEvaluator;

        private readonly StateMachine<EnemyStateId> stateMachine = new();

        public EnemyStateId CurrentState => stateMachine.CurrentId;
        public VisionSensor Vision => vision;
        public HearingSensor Hearing => hearing;
        public EnemyMemory Memory => memory;
        public EnemyMovement Movement => movement;
        public WeaponController Weapon => weapon;
        public TacticalEvaluator TacticalEvaluator => tacticalEvaluator;
        public Health Health => health;

        private void Awake()
        {
            stateMachine.Register(EnemyStateId.Patrol, new PatrolState(this));
            stateMachine.Register(EnemyStateId.Investigate, new InvestigateState(this));
            stateMachine.Register(EnemyStateId.Combat, new CombatState(this));
            stateMachine.Register(EnemyStateId.Search, new SearchState(this));
            stateMachine.Register(EnemyStateId.Retreat, new RetreatState(this));
            stateMachine.Register(EnemyStateId.Dead, new DeadState(this));
            stateMachine.ChangeState(EnemyStateId.Patrol);
        }

        private void Update()
        {
            vision?.TickSensor(Time.deltaTime);
            hearing?.TickSensor(Time.deltaTime);

            bool canSeeTarget = vision != null && vision.HasLineOfSight && vision.VisibleTarget != null;
            bool heardNoise = false;

            if (canSeeTarget)
            {
                memory?.RememberSeen(vision.VisibleTarget.position);
            }

            if (hearing != null && hearing.TryConsumeNoise(out NoiseEventData noise))
            {
                memory?.RememberHeard(noise.Position, noise.Intensity);
                heardNoise = true;
            }

            memory?.TickMemory();
            UpdatePerceptionDrivenState(canSeeTarget, heardNoise);
            stateMachine.Tick(Time.deltaTime);
        }

        public void ConfigurePerception(VisionSensor newVision, HearingSensor newHearing, EnemyMemory newMemory)
        {
            vision = newVision;
            hearing = newHearing;
            memory = newMemory;
        }

        public void ConfigureExecution(EnemyMovement newMovement, WeaponController newWeapon, Health newHealth)
        {
            movement = newMovement;
            weapon = newWeapon;
            health = newHealth;
        }

        public bool ChangeState(EnemyStateId nextState)
        {
            return stateMachine.ChangeState(nextState);
        }

        public TacticalContext BuildTacticalContext(bool coverAvailable, float threat, float suppression)
        {
            float targetDistance = memory != null && memory.HasKnownPosition
                ? Vector3.Distance(transform.position, memory.LastKnownPosition)
                : float.MaxValue;

            float preferredRangeScore = Mathf.Clamp01(1f - Mathf.Abs(targetDistance - 12f) / 12f);

            return new TacticalContext
            {
                TargetDistance = targetDistance,
                PreferredRangeScore = preferredRangeScore,
                HealthRatio = health != null ? health.Normalized : 1f,
                AmmoRatio = weapon != null && weapon.Runtime != null ? weapon.Runtime.AmmoRatio : 0f,
                Threat = Mathf.Clamp01(threat),
                Suppression = Mathf.Clamp01(suppression),
                HasLineOfSight = vision != null && vision.HasLineOfSight,
                CoverAvailable = coverAvailable,
                PathAvailable = movement != null
            };
        }

        private void UpdatePerceptionDrivenState(bool canSeeTarget, bool heardNoise)
        {
            if (health != null && !health.IsAlive)
            {
                ChangeState(EnemyStateId.Dead);
                return;
            }

            if (canSeeTarget)
            {
                ChangeState(EnemyStateId.Combat);
                return;
            }

            if (heardNoise)
            {
                ChangeState(EnemyStateId.Investigate);
                return;
            }

            if (memory != null && memory.HasKnownPosition)
            {
                if (CurrentState == EnemyStateId.Combat || CurrentState == EnemyStateId.Investigate)
                {
                    ChangeState(EnemyStateId.Search);
                }
                return;
            }

            if (CurrentState != EnemyStateId.Patrol)
            {
                ChangeState(EnemyStateId.Patrol);
            }
        }
    }
}
