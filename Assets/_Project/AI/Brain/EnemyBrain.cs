using TacticalEcho.AI.Cover;
using TacticalEcho.AI.Memory;
using TacticalEcho.AI.Navigation;
using TacticalEcho.AI.Perception;
using TacticalEcho.AI.States;
using TacticalEcho.AI.TacticalActions;
using TacticalEcho.AnimationSystem.Runtime;
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
        [SerializeField] private EnemyAnimationController animationController;

        [Header("Decision")]
        [SerializeField] private TacticalEvaluator tacticalEvaluator;
        [SerializeField] private CoverEvaluator coverEvaluator;

        private readonly StateMachine<EnemyStateId> stateMachine = new();
        private Health subscribedHealth;
        private bool isDead;

        public EnemyStateId CurrentState => stateMachine.CurrentId;
        public VisionSensor Vision => vision;
        public HearingSensor Hearing => hearing;
        public EnemyMemory Memory => memory;
        public EnemyMovement Movement => movement;
        public WeaponController Weapon => weapon;
        public EnemyAnimationController AnimationController => animationController;
        public TacticalEvaluator TacticalEvaluator => tacticalEvaluator;
        public CoverEvaluator CoverEvaluator => coverEvaluator;
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

        private void OnEnable()
        {
            BindHealthEvents();
        }

        private void OnDisable()
        {
            UnbindHealthEvents();
        }

        private void Update()
        {
            if (isDead || (health != null && !health.IsAlive))
            {
                HandleDied();
                return;
            }

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

        public void ConfigureExecution(
            EnemyMovement newMovement,
            WeaponController newWeapon,
            Health newHealth,
            EnemyAnimationController newAnimationController = null)
        {
            UnbindHealthEvents();

            movement = newMovement;
            weapon = newWeapon;
            health = newHealth;
            animationController = newAnimationController;
            isDead = health != null && !health.IsAlive;

            if (isActiveAndEnabled)
            {
                BindHealthEvents();
            }
        }

        public void ConfigureDecision(TacticalEvaluator newTacticalEvaluator, CoverEvaluator newCoverEvaluator)
        {
            tacticalEvaluator = newTacticalEvaluator;
            coverEvaluator = newCoverEvaluator;
        }

        public bool ChangeState(EnemyStateId nextState)
        {
            return stateMachine.ChangeState(nextState);
        }

        public TacticalContext BuildTacticalContext()
        {
            return BuildTacticalContextInternal(suppression: 0f, threatOverride: -1f, coverOverride: false);
        }

        public TacticalContext BuildTacticalContext(bool coverAvailable, float threat, float suppression)
        {
            TacticalContext context = BuildTacticalContextInternal(
                suppression,
                Mathf.Clamp01(threat),
                coverAvailable);
            return context;
        }

        private TacticalContext BuildTacticalContextInternal(
            float suppression,
            float threatOverride,
            bool coverOverride)
        {
            bool hasTargetPosition = memory != null && memory.HasKnownPosition;
            Vector3 targetPosition = hasTargetPosition ? memory.LastKnownPosition : default;
            float targetDistance = hasTargetPosition
                ? Vector3.Distance(transform.position, targetPosition)
                : float.MaxValue;

            float preferredRangeScore = hasTargetPosition
                ? Mathf.Clamp01(1f - Mathf.Abs(targetDistance - 12f) / 12f)
                : 0f;

            WeaponRuntime runtime = weapon != null ? weapon.Runtime : null;
            bool isReloading = runtime != null && runtime.IsReloading;
            bool canFire = runtime != null && runtime.CanFire(Time.time);
            bool canReload = runtime != null
                             && !runtime.IsReloading
                             && runtime.HasReserveAmmo
                             && runtime.MagazineAmmo < runtime.Definition.MagazineSize;

            bool coverAvailable = false;
            Vector3 coverPosition = default;
            if (hasTargetPosition
                && coverEvaluator != null
                && coverEvaluator.TryFindBestCover(transform.position, targetPosition, out CoverPoint bestCover))
            {
                coverAvailable = true;
                coverPosition = bestCover.Position;
            }

            coverAvailable |= coverOverride;

            float threat = threatOverride >= 0f
                ? threatOverride
                : vision != null && vision.HasLineOfSight
                    ? 1f
                    : memory != null
                        ? memory.Confidence
                        : 0f;

            return new TacticalContext
            {
                TargetPosition = targetPosition,
                CoverPosition = coverPosition,
                TargetDistance = targetDistance,
                PreferredRangeScore = preferredRangeScore,
                HealthRatio = health != null ? health.Normalized : 1f,
                AmmoRatio = runtime != null ? runtime.AmmoRatio : 0f,
                Threat = Mathf.Clamp01(threat),
                Suppression = Mathf.Clamp01(suppression),
                HasTargetPosition = hasTargetPosition,
                HasLineOfSight = vision != null && vision.HasLineOfSight && vision.VisibleTarget != null,
                CoverAvailable = coverAvailable,
                PathAvailable = movement != null && movement.IsOnNavMesh,
                CanFire = canFire,
                CanReload = canReload,
                IsReloading = isReloading
            };
        }

        private void BindHealthEvents()
        {
            if (health == null || subscribedHealth == health)
            {
                return;
            }

            UnbindHealthEvents();
            subscribedHealth = health;
            subscribedHealth.Died += HandleDied;

            if (!subscribedHealth.IsAlive)
            {
                HandleDied();
            }
        }

        private void UnbindHealthEvents()
        {
            if (subscribedHealth == null)
            {
                return;
            }

            subscribedHealth.Died -= HandleDied;
            subscribedHealth = null;
        }

        private void HandleDied()
        {
            if (isDead && CurrentState == EnemyStateId.Dead)
            {
                return;
            }

            isDead = true;
            ChangeState(EnemyStateId.Dead);
        }

        private void UpdatePerceptionDrivenState(bool canSeeTarget, bool heardNoise)
        {
            if (health != null && !health.IsAlive)
            {
                HandleDied();
                return;
            }

            // Retreat owns its lifecycle once chosen. Perception and memory still update,
            // but Combat must not immediately overwrite the retreat state on the next frame.
            if (CurrentState == EnemyStateId.Retreat)
            {
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
                // Losing LOS from Combat switches to Search immediately. Investigate and
                // Search keep working from remembered positions until their own state logic
                // finishes or memory expires. They never receive the live player Transform.
                if (CurrentState == EnemyStateId.Combat)
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
