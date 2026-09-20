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

        [Header("Status Effects")]
        [Tooltip("Optional. Read to turn an active Suppression effect into TacticalContext.Suppression. Falls back to a StatusEffectController on this GameObject if left unassigned.")]
        [SerializeField] private StatusEffectController statusEffects;

        private readonly StateMachine<EnemyStateId> stateMachine = new();
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
            if (statusEffects == null)
            {
                statusEffects = GetComponent<StatusEffectController>();
            }

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


            if (scheduler == null)
            {
                TickPerception(Time.deltaTime);

                if (!IsAiActive())
                {
                    return;
                }
            }





            stateMachine.Tick(Time.deltaTime);
        }






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
            vision?.TickSensor(deltaTime);
            hearing?.TickSensor(deltaTime);

            bool canSeeTarget = vision != null && vision.HasLineOfSight && vision.VisibleTarget != null;



            if (canSeeTarget && !IsTargetAlive(vision.VisibleTarget))
            {
                canSeeTarget = false;
            }

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
        }







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
            if (isDead || (health != null && !health.IsAlive))
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
            isDead = health != null && !health.IsAlive;

            if (!isDead)
            {



                animationController?.ClearDeath();
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







        private float ResolveSuppression()
        {
            if (statusEffects == null || !statusEffects.HasEffect(SuppressionEffectId, out StatusEffectInstance suppression))
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
            bool hasTargetPosition = memory != null && memory.HasKnownPosition;
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
                TooCloseScore = tooCloseScore,
                TooFarScore = tooFarScore,
                HealthRatio = health != null ? health.Normalized : 1f,
                TargetIsAlive = vision != null && IsTargetAlive(vision.VisibleTarget),
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
