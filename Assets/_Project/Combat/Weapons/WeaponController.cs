using System;
using TacticalEcho.Combat.Damage;
using TacticalEcho.Core.Events;
using UnityEngine;
using UnityEngine.Rendering;

namespace TacticalEcho.Combat.Weapons
{
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
        [SerializeField] private GameObject hitImpactPrefab;
        [SerializeField, Min(0.05f)] private float hitImpactLifetime = 2f;

        [Header("Tracer")]
        [SerializeField] private bool enableTracer = true;
        [SerializeField, Range(4, 24)] private int tracerPoolSize = 12;
        [SerializeField, Min(0.01f)] private float tracerDuration = 0.07f;
        [SerializeField, Min(0.001f)] private float tracerWidth = 0.012f;
        [SerializeField] private Color tracerStartColor = new(1f, 0.9f, 0.45f, 1f);
        [SerializeField] private Color tracerEndColor = new(1f, 0.45f, 0.15f, 0.15f);

        [Header("Fallback Hit Impact")]
        [SerializeField] private bool enableProceduralHitImpact = true;
        [SerializeField, Range(4, 32)] private int proceduralImpactPoolSize = 16;
        [SerializeField, Min(0.01f)] private float proceduralImpactDuration = 0.12f;
        [SerializeField, Min(0.005f)] private float proceduralImpactSize = 0.045f;
        [SerializeField] private Color proceduralImpactColor = new(1f, 0.72f, 0.25f, 1f);

        [Header("Muzzle Flash")]
        [SerializeField] private bool enableMuzzleFlash = true;
        [SerializeField, Min(0.01f)] private float muzzleFlashDuration = 0.04f;
        [SerializeField, Min(0f)] private float muzzleFlashIntensity = 4f;
        [SerializeField, Min(0f)] private float muzzleFlashRange = 3f;

        public event Action Fired;
        public event Action<RaycastHit> HitResolved;
        public event Action DamageApplied;
        public event Action ReloadStarted;
        public event Action ReloadCompleted;
        public event Action<int, int> AmmoChanged;

        public WeaponRuntime Runtime { get; private set; }
        public WeaponDefinition Definition => definition;
        public int MagazineAmmo => Runtime?.MagazineAmmo ?? 0;
        public int ReserveAmmo => Runtime?.ReserveAmmo ?? 0;
        public bool IsReloading => Runtime != null && Runtime.IsReloading;

        private AudioSource audioSource;
        private Light muzzleFlashLight;
        private float muzzleFlashEndTime;

        private ProceduralImpactMarker[] impactPool;
        private int nextImpactIndex;
        private Material proceduralImpactMaterial;

        private ShotTracer[] tracerPool;
        private int nextTracerIndex;
        private Material tracerMaterial;

        private sealed class ProceduralImpactMarker
        {
            public GameObject GameObject;
            public Transform Transform;
            public float EndTime;
        }

        private sealed class ShotTracer
        {
            public GameObject GameObject;
            public LineRenderer Line;
            public float EndTime;
        }

        private void Awake()
        {
            InitializeRuntime();
            ResolveAudioProfile();
            EnsureShotFeedbackComponents();
        }

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

            UpdateProceduralImpacts();
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
            DestroyImpactPool();
            DestroyTracerPool();

            if (proceduralImpactMaterial != null)
            {
                Destroy(proceduralImpactMaterial);
            }

            if (tracerMaterial != null)
            {
                Destroy(tracerMaterial);
            }
        }

        public void Configure(WeaponDefinition weaponDefinition, Transform muzzleTransform)
        {
            definition = weaponDefinition;
            muzzle = muzzleTransform;
            InitializeRuntime();
            ResolveAudioProfile();
            EnsureShotFeedbackComponents();
        }

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

                DamageInfo damageInfo = new(
                    definition.Damage,
                    hit.point,
                    hit.normal,
                    gameObject);

                bool damageApplied = DamageSystem.TryApply(hit.collider, damageInfo);
                SpawnHitImpact(hit);
                HitResolved?.Invoke(hit);

                if (damageApplied)
                {
                    DamageApplied?.Invoke();
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
            Runtime = definition != null ? new WeaponRuntime(definition) : null;
            if (Runtime != null)
            {
                AmmoChanged?.Invoke(Runtime.MagazineAmmo, Runtime.ReserveAmmo);
            }
        }

        private void ResolveAudioProfile()
        {
            if (audioProfile == null)
            {
                audioProfile = Resources.Load<WeaponAudioProfile>(DefaultAudioProfileResourcePath);
            }
        }

        private void EnsureShotFeedbackComponents()
        {
            audioSource = GetComponent<AudioSource>();
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

            if (enableProceduralHitImpact && hitImpactPrefab == null)
            {
                EnsureProceduralImpactPool();
            }

            if (enableTracer)
            {
                EnsureTracerPool();
            }

            if (!enableMuzzleFlash || muzzle == null)
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
            if (clip == null || audioSource == null)
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
                    name = "Runtime Rifle Tracer"
                };
            }

            tracerPool = new ShotTracer[targetSize];
            nextTracerIndex = 0;

            for (int i = 0; i < targetSize; i++)
            {
                GameObject tracerObject = new($"RuntimeTracer_{i:00}");
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

        private void SpawnHitImpact(in RaycastHit hit)
        {
            if (hitImpactPrefab != null)
            {
                Quaternion rotation = Quaternion.LookRotation(hit.normal);
                GameObject impact = Instantiate(hitImpactPrefab, hit.point + hit.normal * 0.002f, rotation);
                Destroy(impact, hitImpactLifetime);
                return;
            }

            if (!enableProceduralHitImpact)
            {
                return;
            }

            EnsureProceduralImpactPool();
            if (impactPool == null || impactPool.Length == 0)
            {
                return;
            }

            ProceduralImpactMarker marker = impactPool[nextImpactIndex];
            nextImpactIndex = (nextImpactIndex + 1) % impactPool.Length;

            marker.Transform.position = hit.point + hit.normal * 0.004f;
            marker.Transform.rotation = Quaternion.LookRotation(hit.normal);
            marker.Transform.localScale = Vector3.one * proceduralImpactSize;
            marker.EndTime = Time.time + proceduralImpactDuration;
            marker.GameObject.SetActive(true);
        }

        private void EnsureProceduralImpactPool()
        {
            int targetSize = Mathf.Clamp(proceduralImpactPoolSize, 4, 32);
            if (impactPool != null && impactPool.Length == targetSize)
            {
                return;
            }

            DestroyImpactPool();

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                impactPool = Array.Empty<ProceduralImpactMarker>();
                return;
            }

            if (proceduralImpactMaterial == null || proceduralImpactMaterial.shader != shader)
            {
                if (proceduralImpactMaterial != null)
                {
                    Destroy(proceduralImpactMaterial);
                }

                proceduralImpactMaterial = new Material(shader)
                {
                    name = "Runtime Procedural Bullet Impact"
                };

                if (proceduralImpactMaterial.HasProperty("_BaseColor"))
                {
                    proceduralImpactMaterial.SetColor("_BaseColor", proceduralImpactColor);
                }
                else if (proceduralImpactMaterial.HasProperty("_Color"))
                {
                    proceduralImpactMaterial.SetColor("_Color", proceduralImpactColor);
                }
            }

            impactPool = new ProceduralImpactMarker[targetSize];
            nextImpactIndex = 0;

            for (int i = 0; i < targetSize; i++)
            {
                GameObject markerObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                markerObject.name = $"RuntimeBulletImpact_{i:00}";

                Collider markerCollider = markerObject.GetComponent<Collider>();
                if (markerCollider != null)
                {
                    Destroy(markerCollider);
                }

                MeshRenderer renderer = markerObject.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = proceduralImpactMaterial;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }

                markerObject.SetActive(false);
                impactPool[i] = new ProceduralImpactMarker
                {
                    GameObject = markerObject,
                    Transform = markerObject.transform,
                    EndTime = 0f
                };
            }
        }

        private void DestroyImpactPool()
        {
            if (impactPool == null)
            {
                return;
            }

            foreach (ProceduralImpactMarker marker in impactPool)
            {
                if (marker?.GameObject != null)
                {
                    Destroy(marker.GameObject);
                }
            }

            impactPool = null;
        }

        private void UpdateProceduralImpacts()
        {
            if (impactPool == null)
            {
                return;
            }

            float now = Time.time;
            foreach (ProceduralImpactMarker marker in impactPool)
            {
                if (marker == null || marker.GameObject == null || !marker.GameObject.activeSelf)
                {
                    continue;
                }

                float remaining = marker.EndTime - now;
                if (remaining <= 0f)
                {
                    marker.GameObject.SetActive(false);
                    continue;
                }

                float normalized = Mathf.Clamp01(remaining / proceduralImpactDuration);
                marker.Transform.localScale = Vector3.one * proceduralImpactSize * Mathf.Lerp(0.25f, 1f, normalized);
            }
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
    }
}
