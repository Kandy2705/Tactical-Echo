using TacticalEcho.AI.Cover;
using TacticalEcho.AI.Memory;
using TacticalEcho.AI.Navigation;
using TacticalEcho.AI.Perception;
using TacticalEcho.AI.States;
using TacticalEcho.AI.TacticalActions;
using TacticalEcho.AnimationSystem.Runtime;
using TacticalEcho.Combat.Health;
using TacticalEcho.AI.Patrol;
using TacticalEcho.Combat.Damage;
using TacticalEcho.Combat.StatusEffects;
using TacticalEcho.Optimization;
using TacticalEcho.Combat.Weapons;
using TacticalEcho.Core.Events;
using TacticalEcho.Core.StateMachine;
using UnityEngine;

namespace TacticalEcho.AI.Brain
{
    [RequireComponent(typeof(StatusEffectController))]
    public sealed class EnemyBrain : MonoBehaviour
    {
        private const float PreferredCombatRange = 12f;
        private const string SuppressionEffectId = "suppression";

        [Header("Perception")]
        [SerializeField] private VisionSensor vision;
        [SerializeField] private HearingSensor hearing;
        [SerializeField] private EnemyMemory memory;

        [Header("Execution")]
        [SerializeField] private EnemyMovement movement;
        [SerializeField] private WeaponController weapon;
        [SerializeField] private Health health;
        [SerializeField] private PatrolRoute patrolRoute;
        [Tooltip("Run AI perception and decisions on the shared TickScheduler budget instead of every frame.")]
        [SerializeField] private bool useTickScheduler = true;
        [SerializeField] private EnemyAnimationController animationController;

        [Header("Decision")]
        [SerializeField] private TacticalEvaluator tacticalEvaluator;
        [SerializeField] private CoverEvaluator coverEvaluator;

        private readonly StateMachine<EnemyStateId> stateMachine = new();
        private StatusEffectController statusEffects;
        private Health subscribedHealth;
        private bool isDead;
        private TickScheduler scheduler;
        private Transform cachedTargetTransform;
        private IDamageable cachedTargetDamageable;

        public EnemyStateId CurrentState => stateMachine.CurrentId;
        public VisionSensor Vision => vision;
        public HearingSensor Hearing => hearing;
        public EnemyMemory Memory => memory;
        public EnemyMovement Movement => movement;
        public WeaponController Weapon => weapon;
        public EnemyAnimationController AnimationController => animationController;
        public TacticalEvaluator TacticalEvaluator => tacticalEvaluator;
        public CoverEvaluator CoverEvaluator => coverEvaluator;
        public StatusEffectController StatusEffects => statusEffects;
        public Health Health => health;
        public PatrolRoute PatrolRoute => patrolRoute;

        private void Awake()
        {
            statusEffects = GetComponent<StatusEffectController>();

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

            if (useTickScheduler)
            {
                // Keep the instance we registered with: resolving it again during teardown
                // would recreate the scheduler GameObject while the scene is unloading.
                scheduler = TickScheduler.Shared;
                scheduler.Register(TickAi);
            }
        }

        private void OnDisable()
        {
            UnbindHealthEvents();

            if (scheduler != null)
            {
                scheduler.Unregister(TickAi);
                scheduler = null;
            }
        }

        private void Update()
        {
            if (!IsAiActive())
            {
                return;
            }

            // Without a scheduler the brain owns the whole step itself.
            if (scheduler == null)
            {
                TickPerception(Time.deltaTime);

                if (!IsAiActive())
                {
                    return;
                }
            }

            // The state machine stays per-frame on purpose. It is cheap, and it drives
            // facing and destination updates - running it on the 10 Hz budget would make
            // enemies visibly snap when turning. The budget exists for the expensive part,
            // which is perception.
            stateMachine.Tick(Time.deltaTime);
        }

        /// <summary>
        /// Perception step registered with the shared tick budget: sensors (the raycasting
        /// part), memory decay and the perception-driven state transitions. Receives the real
        /// elapsed time, so sensor and memory timers stay correct at any budget interval.
        /// </summary>
        private void TickAi(float deltaTime)
        {
            if (!IsAiActive())
            {
                return;
            }

            TickPerception(deltaTime);
        }

        private void TickPerception(float deltaTime)
        {
            vision.TickSensor(deltaTime);
            hearing.TickSensor(deltaTime);

            bool canSeeTarget = vision.HasLineOfSight && vision.VisibleTarget != null;

            // Seeing a body is not contact. Without this the AI stays in Combat on a corpse
            // and keeps firing at it forever.
            if (canSeeTarget && !IsTargetAlive(vision.VisibleTarget))
            {
                canSeeTarget = false;
            }

            bool heardNoise = false;

            if (canSeeTarget)
            {
                memory.RememberSeen(vision.VisibleTarget.position);
            }

            if (hearing.TryConsumeNoise(out NoiseEventData noise))
            {
                memory.RememberHeard(noise.Position, noise.Intensity);
                heardNoise = true;
            }

            memory.TickMemory();
            UpdatePerceptionDrivenState(canSeeTarget, heardNoise);
        }

        /// <summary>
        /// Whether the thing the sensors can see is still worth reacting to. The sensor only
        /// reports what it detects; deciding that a corpse is not a threat is the brain's call.
        /// A target with no IDamageable at all is treated as alive, so non-damageable props
        /// behave exactly as before.
        /// </summary>
        private bool IsTargetAlive(Transform targetTransform)
        {
            if (targetTransform == null)
            {
                return false;
            }

            if (!ReferenceEquals(targetTransform, cachedTargetTransform))
            {
                cachedTargetTransform = targetTransform;
                cachedTargetDamageable = targetTransform.GetComponentInParent<IDamageable>();
            }

            return cachedTargetDamageable == null || cachedTargetDamageable.IsAlive;
        }

        private bool IsAiActive()
        {
            if (isDead || !health.IsAlive)
            {
                HandleDied();
                return false;
            }

            return true;
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
            isDead = !health.IsAlive;

            if (!isDead)
            {
                // Configuration can run after a transient death was latched during scene
                // startup. The brain clears its own flag here, so the animation layer has to
                // be released in the same step or the two disagree permanently.
                animationController.ClearDeath();
            }

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
            return BuildTacticalContextInternal(suppression: ResolveSuppression(), threatOverride: -1f, coverOverride: false);
        }

        /// <summary>
        /// Turns an active Suppression status effect into a normalized 0..1 value driven by its
        /// current stack count. This is how Suppression reaches TacticalContext/TakeCoverAction
        /// without EnemyBrain special-casing the effect itself - the StatusEffectController
        /// pipeline stays the single owner of effect lifecycle.
        /// </summary>
        private float ResolveSuppression()
        {
            if (!statusEffects.HasEffect(SuppressionEffectId, out StatusEffectInstance suppression))
            {
                return 0f;
            }

            int maxStacks = Mathf.Max(1, suppression.Definition.MaxStacks);
            return Mathf.Clamp01((float)suppression.StackCount / maxStacks);
        }

        public TacticalContext BuildTacticalContext(bool coverAvailable, float threat, float suppression)
        {
            return BuildTacticalContextInternal(
                suppression,
                Mathf.Clamp01(threat),
                coverAvailable);
        }

        private TacticalContext BuildTacticalContextInternal(
            float suppression,
            float threatOverride,
            bool coverOverride)
        {
            bool hasTargetPosition = memory.HasKnownPosition;
            Vector3 targetPosition = hasTargetPosition ? memory.LastKnownPosition : default;
            float targetDistance = hasTargetPosition
                ? Vector3.Distance(transform.position, targetPosition)
                : float.MaxValue;

            float preferredRangeScore = 0f;
            float tooCloseScore = 0f;
            float tooFarScore = 0f;
            if (hasTargetPosition)
            {
                float signedRangeError = targetDistance - PreferredCombatRange;
                preferredRangeScore = Mathf.Clamp01(
                    1f - Mathf.Abs(signedRangeError) / PreferredCombatRange);
                tooCloseScore = Mathf.Clamp01(
                    Mathf.Max(0f, -signedRangeError) / PreferredCombatRange);
                tooFarScore = Mathf.Clamp01(
                    Mathf.Max(0f, signedRangeError) / PreferredCombatRange);
            }

            WeaponRuntime runtime = weapon.Runtime;
            bool isReloading = runtime != null && runtime.IsReloading;
            bool canFire = runtime != null && runtime.CanFire(Time.time);
            bool canReload = runtime != null
                             && !runtime.IsReloading
                             && runtime.HasReserveAmmo
                             && runtime.MagazineAmmo < runtime.Definition.MagazineSize;

            bool coverAvailable = false;
            Vector3 coverPosition = default;
            if (hasTargetPosition
                && coverEvaluator.TryFindBestCover(transform.position, targetPosition, out CoverPoint bestCover))
            {
                coverAvailable = true;
                coverPosition = bestCover.Position;
            }

            coverAvailable |= coverOverride;

            float threat = threatOverride >= 0f
                ? threatOverride
                : vision.HasLineOfSight
                    ? 1f
                    : memory.Confidence;

            return new TacticalContext
            {
                TargetPosition = targetPosition,
                CoverPosition = coverPosition,
                TargetDistance = targetDistance,
                PreferredRangeScore = preferredRangeScore,
                TooCloseScore = tooCloseScore,
                TooFarScore = tooFarScore,
                HealthRatio = health.Normalized,
                TargetIsAlive = IsTargetAlive(vision.VisibleTarget),
                AmmoRatio = runtime != null ? runtime.AmmoRatio : 0f,
                Threat = Mathf.Clamp01(threat),
                Suppression = Mathf.Clamp01(suppression),
                HasTargetPosition = hasTargetPosition,
                HasLineOfSight = vision.HasLineOfSight && vision.VisibleTarget != null,
                CoverAvailable = coverAvailable,
                PathAvailable = movement.IsOnNavMesh,
                CanFire = canFire,
                CanReload = canReload,
                IsReloading = isReloading
            };
        }

        private void BindHealthEvents()
        {
            if (subscribedHealth == health)
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
            if (!health.IsAlive)
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

            if (memory.HasKnownPosition)
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
