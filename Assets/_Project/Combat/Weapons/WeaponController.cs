using System;
using System.Collections.Generic;
using TacticalEcho.Combat.Damage;
using TacticalEcho.Combat.Impacts;
using TacticalEcho.Combat.StatusEffects;
using TacticalEcho.Core.Events;
using UnityEngine;
using UnityEngine.Rendering;

namespace TacticalEcho.Combat.Weapons
{
    // Shared weapon execution boundary used by both Player and Enemy (see Docs/ARCHITECTURE.md - "Combat flow").
    // Owns hitscan firing, damage dispatch, reload lifecycle, on-hit status effects, and shot feedback
    // (tracer/audio/muzzle flash/noise). Player input and AI tactical scoring only ever call into this class;
    // they must not duplicate raycast/damage/reload logic themselves.
    public sealed class WeaponController : MonoBehaviour
    {
        private const string DefaultAudioProfileResourcePath = "Combat/RifleAudioProfile";

        [Header("Configuration")]
        [SerializeField] private WeaponDefinition definition;

        [Header("Scene References")]
        [SerializeField] private Transform muzzle;
        [SerializeField] private LayerMask hitMask = ~0;

        [Header("Shot Feedback")]
        [SerializeField] private WeaponAudioProfile audioProfile;
        [SerializeField] private AudioClip fireAudioClip;
        [SerializeField, Range(0f, 1f)] private float fireAudioVolume = 0.95f;

        // Data-driven so a new on-hit effect is one more list entry, not a new field plus a new
        // "if" branch in ApplyHitStatusEffects(). Existing rules: Suppression on every damaging hit,
        // Bleed on a critical hit only (see OnHitStatusEffectRule below).
        [Header("Status Effects On Hit")]
        [Tooltip("Effects this weapon applies to whatever it hits and damages, if the target has a StatusEffectController. Add one entry per effect.")]
        [SerializeField] private List<OnHitStatusEffectRule> onHitEffects = new();

        [Header("Tracer")]
        [SerializeField] private bool enableTracer = true;
        [SerializeField, Range(4, 24)] private int tracerPoolSize = 12;
        [SerializeField, Min(0.01f)] private float tracerDuration = 0.07f;
        [SerializeField, Min(0.001f)] private float tracerWidth = 0.012f;
        [SerializeField] private Color tracerStartColor = new(1f, 0.9f, 0.45f, 1f);
        [SerializeField] private Color tracerEndColor = new(1f, 0.45f, 0.15f, 0.15f);

        [Header("Muzzle Flash")]
        [SerializeField] private bool enableMuzzleFlash = true;
        [SerializeField, Min(0.01f)] private float muzzleFlashDuration = 0.04f;
        [SerializeField, Min(0f)] private float muzzleFlashIntensity = 4f;
        [SerializeField, Min(0f)] private float muzzleFlashRange = 3f;

        public event Action Fired;
        public event Action<RaycastHit> HitResolved;
        public event Action DamageApplied;
        public event Action<DamageFeedback> DamageFeedbackResolved;
        public event Action ReloadStarted;
        public event Action ReloadCompleted;
        public event Action<int, int> AmmoChanged;
        public event Action<WeaponDefinition> WeaponChanged;

        public WeaponRuntime Runtime { get; private set; }
        public WeaponDefinition Definition => definition;
        public int MagazineAmmo => Runtime?.MagazineAmmo ?? 0;
        public int ReserveAmmo => Runtime?.ReserveAmmo ?? 0;
        public bool IsReloading => Runtime != null && Runtime.IsReloading;

        private AudioSource audioSource;
        private Light muzzleFlashLight;
        private float muzzleFlashEndTime;




        private readonly Dictionary<WeaponDefinition, WeaponRuntime> runtimeByDefinition = new();

        private ShotTracer[] tracerPool;
        private int nextTracerIndex;
        private Material tracerMaterial;

        private sealed class ShotTracer
        {
            public GameObject GameObject;
            public LineRenderer Line;
            public float EndTime;
        }

        // One configurable on-hit effect: what to apply, and whether it requires a critical hit.
        // Keeping this a plain serializable rule (instead of a dedicated field per effect) is what lets
        // ApplyHitStatusEffects() stay a simple loop no matter how many effects a weapon ends up applying.
        [Serializable]
        private sealed class OnHitStatusEffectRule
        {
            [SerializeField] private StatusEffectDefinition definition;
            [Tooltip("On: only applies on a critical hit (e.g. a headshot). Off: applies on every damaging hit.")]
            [SerializeField] private bool requireCriticalHit;

            public StatusEffectDefinition Definition => definition;
            public bool RequireCriticalHit => requireCriticalHit;
        }

        private void Awake()
        {
            InitializeRuntime();
            ResolveAudioProfile();
        }

        // Per-frame upkeep only: spread decay, reload completion timing and the muzzle flash/tracer
        // fade-out. None of this owns gameplay decisions - Player input and AI actions call TryFire()/
        // TryBeginReload() to actually do something.

        private void Update()
        {
            Runtime?.RecoverSpread(Time.deltaTime);

            if (Runtime != null && Runtime.ShouldCompleteReload(Time.time))
            {
                CompleteReload();
            }

            if (muzzleFlashLight != null && muzzleFlashLight.enabled && Time.time >= muzzleFlashEndTime)
            {
                muzzleFlashLight.enabled = false;
            }

            UpdateTracers();
        }

        private void OnDisable()
        {
            Runtime?.CancelReload();

            if (muzzleFlashLight != null)
            {
                muzzleFlashLight.enabled = false;
            }
        }

        private void OnDestroy()
        {
            DestroyTracerPool();

            if (tracerMaterial != null)
            {
                Destroy(tracerMaterial);
            }
        }






        // Switches which weapon this controller fires, keeping a separate WeaponRuntime per definition
        // (see runtimeByDefinition) so swapping back to a weapon returns it with the ammo it was left with.
        public bool Equip(WeaponDefinition weaponDefinition)
        {
            if (weaponDefinition == null || weaponDefinition == definition)
            {
                return false;
            }

            Runtime?.CancelReload();
            definition = weaponDefinition;
            InitializeRuntime();
            ResolveAudioProfile();
            WeaponChanged?.Invoke(definition);
            return true;
        }

        // One-time setup used when this controller is provisioned in code (rather than authored in the
        // Inspector) - assigns the starting definition and muzzle instead of swapping an existing one.
        public void Configure(WeaponDefinition weaponDefinition, Transform muzzleTransform)
        {
            definition = weaponDefinition;
            muzzle = muzzleTransform;
            InitializeRuntime();
            ResolveAudioProfile();
        }

        // The weapon execution boundary: consumes ammo/cooldown, applies spread, raycasts, resolves
        // damage + on-hit status effects, then fires off tracer/audio/muzzle-flash/noise feedback.
        // Player and AI both call this instead of implementing any of it themselves.
        public bool TryFire(
            Vector3 origin,
            Vector3 direction,
            float movement01 = 0f,
            bool isAiming = false)
        {
            if (Runtime == null || definition == null || !Runtime.TryConsumeShot(Time.time))
            {
                return false;
            }

            Vector3 forward = direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : transform.forward;

            float spreadDegrees = Runtime.GetEffectiveSpread(movement01, isAiming);
            Vector3 shotDirection = ApplySpread(forward, spreadDegrees);
            Vector3 shotEnd = origin + shotDirection * definition.Range;

            if (Physics.Raycast(
                    origin,
                    shotDirection,
                    out RaycastHit hit,
                    definition.Range,
                    hitMask,
                    QueryTriggerInteraction.Ignore))
            {
                shotEnd = hit.point;

                DamageHitZone hitZone = hit.collider.GetComponent<DamageHitZone>();
                float multiplier = hitZone != null ? hitZone.DamageMultiplier : 1f;
                float resolvedDamage = definition.Damage * multiplier;
                DamageHitZoneType zoneType = hitZone != null ? hitZone.ZoneType : DamageHitZoneType.Generic;
                bool isCritical = hitZone != null && hitZone.IsCritical;

                DamageInfo damageInfo = new(
                    resolvedDamage,
                    hit.point,
                    hit.normal,
                    gameObject);

                bool damageApplied = DamageSystem.TryApply(hit.collider, damageInfo);
                SurfaceImpactSystem.HandleHit(hit, damageApplied);
                HitResolved?.Invoke(hit);

                if (damageApplied)
                {
                    DamageApplied?.Invoke();
                    DamageFeedbackResolved?.Invoke(new DamageFeedback(
                        resolvedDamage,
                        hit.point,
                        zoneType,
                        isCritical));
                    ApplyHitStatusEffects(hit.collider, isCritical);
                }
            }

            ShowTracer(muzzle != null ? muzzle.position : origin, shotEnd);
            PlayShotFeedback();
            EmitGunNoise();
            AmmoChanged?.Invoke(Runtime.MagazineAmmo, Runtime.ReserveAmmo);
            Fired?.Invoke();
            return true;
        }

        public bool TryBeginReload()
        {
            if (Runtime == null || !Runtime.TryBeginReload(Time.time))
            {
                return false;
            }

            PlayReloadClip(audioProfile != null ? audioProfile.ReloadStartClip : null);
            ReloadStarted?.Invoke();
            return true;
        }

        public void CompleteReload()
        {
            if (Runtime == null || !Runtime.CompleteReload())
            {
                return;
            }

            PlayReloadClip(audioProfile != null ? audioProfile.ReloadCompleteClip : null);
            AmmoChanged?.Invoke(Runtime.MagazineAmmo, Runtime.ReserveAmmo);
            ReloadCompleted?.Invoke();
        }

        public void CancelReload()
        {
            Runtime?.CancelReload();
        }

        private void InitializeRuntime()
        {
            if (definition == null)
            {
                Runtime = null;
                return;
            }

            if (!runtimeByDefinition.TryGetValue(definition, out WeaponRuntime runtime))
            {
                runtime = new WeaponRuntime(definition);
                runtimeByDefinition[definition] = runtime;
            }

            Runtime = runtime;
            AmmoChanged?.Invoke(Runtime.MagazineAmmo, Runtime.ReserveAmmo);
        }

        private void ResolveAudioProfile()
        {
            if (audioProfile == null)
            {
                audioProfile = Resources.Load<WeaponAudioProfile>(DefaultAudioProfileResourcePath);
            }
        }

        private void EnsureAudioSource()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.15f;
            audioSource.dopplerLevel = 0f;
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.minDistance = 1f;
            audioSource.maxDistance = 55f;
            audioSource.volume = 1f;
        }

        private void EnsureMuzzleFlash()
        {
            if (!enableMuzzleFlash || muzzle == null || muzzleFlashLight != null)
            {
                return;
            }

            Transform existing = muzzle.Find("RuntimeMuzzleFlash");
            if (existing != null)
            {
                muzzleFlashLight = existing.GetComponent<Light>();
            }

            if (muzzleFlashLight == null)
            {
                GameObject flashObject = new("RuntimeMuzzleFlash");
                flashObject.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
                flashObject.transform.SetParent(muzzle, false);
                muzzleFlashLight = flashObject.AddComponent<Light>();
                muzzleFlashLight.type = LightType.Point;
            }

            muzzleFlashLight.range = muzzleFlashRange;
            muzzleFlashLight.intensity = muzzleFlashIntensity;
            muzzleFlashLight.enabled = false;
        }

        private void PlayShotFeedback()
        {
            EnsureAudioSource();
            EnsureMuzzleFlash();

            AudioClip clip = audioProfile != null ? audioProfile.GetRandomFireClip() : null;
            if (clip == null)
            {
                clip = fireAudioClip;
            }

            if (clip != null && audioSource != null)
            {
                if (clip.loadState == AudioDataLoadState.Unloaded)
                {
                    clip.LoadAudioData();
                }

                audioSource.PlayOneShot(clip, fireAudioVolume);
            }

            if (enableMuzzleFlash && muzzleFlashLight != null)
            {
                muzzleFlashLight.intensity = muzzleFlashIntensity * UnityEngine.Random.Range(0.85f, 1.2f);
                muzzleFlashLight.enabled = true;
                muzzleFlashEndTime = Time.time + muzzleFlashDuration;
            }
        }

        private void PlayReloadClip(AudioClip clip)
        {
            if (clip == null)
            {
                return;
            }

            EnsureAudioSource();
            if (audioSource == null)
            {
                return;
            }

            if (clip.loadState == AudioDataLoadState.Unloaded)
            {
                clip.LoadAudioData();
            }

            audioSource.PlayOneShot(clip, Mathf.Clamp01(fireAudioVolume * 0.8f));
        }

        private void ShowTracer(Vector3 start, Vector3 end)
        {
            if (!enableTracer)
            {
                return;
            }

            EnsureTracerPool();
            if (tracerPool == null || tracerPool.Length == 0)
            {
                return;
            }

            ShotTracer tracer = tracerPool[nextTracerIndex];
            nextTracerIndex = (nextTracerIndex + 1) % tracerPool.Length;

            tracer.Line.SetPosition(0, start);
            tracer.Line.SetPosition(1, end);
            tracer.Line.startWidth = tracerWidth;
            tracer.Line.endWidth = tracerWidth * 0.45f;
            tracer.Line.startColor = tracerStartColor;
            tracer.Line.endColor = tracerEndColor;
            tracer.EndTime = Time.time + tracerDuration;
            tracer.GameObject.SetActive(true);
        }

        private void EnsureTracerPool()
        {
            int targetSize = Mathf.Clamp(tracerPoolSize, 4, 24);
            if (tracerPool != null && tracerPool.Length == targetSize)
            {
                return;
            }

            DestroyTracerPool();

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader == null)
            {
                tracerPool = Array.Empty<ShotTracer>();
                return;
            }

            if (tracerMaterial == null || tracerMaterial.shader != shader)
            {
                if (tracerMaterial != null)
                {
                    Destroy(tracerMaterial);
                }

                tracerMaterial = new Material(shader)
                {
                    name = "Runtime Rifle Tracer",
                    hideFlags = HideFlags.DontSave
                };
            }

            tracerPool = new ShotTracer[targetSize];
            nextTracerIndex = 0;

            for (int i = 0; i < targetSize; i++)
            {
                GameObject tracerObject = new($"RuntimeTracer_{i:00}");
                tracerObject.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
                tracerObject.transform.SetParent(transform, false);

                LineRenderer line = tracerObject.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.positionCount = 2;
                line.numCapVertices = 2;
                line.alignment = LineAlignment.View;
                line.textureMode = LineTextureMode.Stretch;
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.sharedMaterial = tracerMaterial;

                tracerObject.SetActive(false);
                tracerPool[i] = new ShotTracer
                {
                    GameObject = tracerObject,
                    Line = line,
                    EndTime = 0f
                };
            }
        }

        private void UpdateTracers()
        {
            if (tracerPool == null)
            {
                return;
            }

            float now = Time.time;
            foreach (ShotTracer tracer in tracerPool)
            {
                if (tracer == null || tracer.GameObject == null || !tracer.GameObject.activeSelf)
                {
                    continue;
                }

                if (now >= tracer.EndTime)
                {
                    tracer.GameObject.SetActive(false);
                    continue;
                }

                float normalized = Mathf.Clamp01((tracer.EndTime - now) / tracerDuration);
                Color start = tracerStartColor;
                Color end = tracerEndColor;
                start.a *= normalized;
                end.a *= normalized;
                tracer.Line.startColor = start;
                tracer.Line.endColor = end;
            }
        }

        private void DestroyTracerPool()
        {
            if (tracerPool == null)
            {
                return;
            }

            foreach (ShotTracer tracer in tracerPool)
            {
                if (tracer?.GameObject != null)
                {
                    Destroy(tracer.GameObject);
                }
            }

            tracerPool = null;
        }

        private static Vector3 ApplySpread(Vector3 forward, float spreadDegrees)
        {
            if (spreadDegrees <= 0.0001f)
            {
                return forward;
            }

            Vector3 referenceUp = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.98f
                ? Vector3.right
                : Vector3.up;

            Vector3 right = Vector3.Cross(referenceUp, forward).normalized;
            Vector3 up = Vector3.Cross(forward, right).normalized;

            Vector2 randomPoint = UnityEngine.Random.insideUnitCircle;
            float coneRadius = Mathf.Tan(spreadDegrees * Mathf.Deg2Rad);

            return (forward
                    + right * randomPoint.x * coneRadius
                    + up * randomPoint.y * coneRadius)
                .normalized;
        }

        private void EmitGunNoise()
        {
            Vector3 noisePosition = muzzle != null ? muzzle.position : transform.position;
            NoiseEventHub.Emit(new NoiseEventData(
                noisePosition,
                definition.NoiseRadius,
                definition.NoiseIntensity,
                gameObject));
        }

        private void ApplyHitStatusEffects(Collider hitCollider, bool isCritical)
        {
            if (onHitEffects.Count == 0)
            {
                return;
            }

            // Effects only land on targets that can actually hold them; anything else (props, terrain) is a no-op.
            StatusEffectController statusEffects = hitCollider.GetComponentInParent<StatusEffectController>();
            if (statusEffects == null)
            {
                return;
            }

            foreach (OnHitStatusEffectRule rule in onHitEffects)
            {
                if (rule.Definition == null || (rule.RequireCriticalHit && !isCritical))
                {
                    continue;
                }

                statusEffects.Apply(rule.Definition);
            }
        }
    }
}
