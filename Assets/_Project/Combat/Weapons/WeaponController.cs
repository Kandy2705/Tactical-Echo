using System;
using TacticalEcho.Combat.Damage;
using TacticalEcho.Core.Events;
using UnityEngine;

namespace TacticalEcho.Combat.Weapons
{
    public sealed class WeaponController : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private WeaponDefinition definition;

        [Header("Scene References")]
        [SerializeField] private Transform muzzle;
        [SerializeField] private LayerMask hitMask = ~0;

        [Header("Shot Feedback")]
        [SerializeField] private AudioClip fireAudioClip;
        [SerializeField, Range(0f, 1f)] private float fireAudioVolume = 0.9f;
        [SerializeField] private GameObject hitImpactPrefab;
        [SerializeField, Min(0.05f)] private float hitImpactLifetime = 2f;

        [Header("Fallback Hit Impact")]
        [SerializeField] private bool enableProceduralHitImpact = true;
        [SerializeField, Range(4, 32)] private int proceduralImpactPoolSize = 16;
        [SerializeField, Min(0.01f)] private float proceduralImpactDuration = 0.12f;
        [SerializeField, Min(0.005f)] private float proceduralImpactSize = 0.045f;
        [SerializeField] private Color proceduralImpactColor = new(1f, 0.72f, 0.25f, 1f);

        [Header("Muzzle Flash")]
        [SerializeField] private bool enableMuzzleFlash = true;
        [SerializeField, Min(0.01f)] private float muzzleFlashDuration = 0.035f;
        [SerializeField, Min(0f)] private float muzzleFlashIntensity = 3f;
        [SerializeField, Min(0f)] private float muzzleFlashRange = 2.5f;

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

        private sealed class ProceduralImpactMarker
        {
            public GameObject GameObject;
            public Transform Transform;
            public float EndTime;
        }

        private void Awake()
        {
            InitializeRuntime();
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
            if (impactPool != null)
            {
                foreach (ProceduralImpactMarker marker in impactPool)
                {
                    if (marker?.GameObject != null)
                    {
                        Destroy(marker.GameObject);
                    }
                }
            }

            if (proceduralImpactMaterial != null)
            {
                Destroy(proceduralImpactMaterial);
            }
        }

        public void Configure(WeaponDefinition weaponDefinition, Transform muzzleTransform)
        {
            definition = weaponDefinition;
            muzzle = muzzleTransform;
            InitializeRuntime();
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

            if (Physics.Raycast(
                    origin,
                    shotDirection,
                    out RaycastHit hit,
                    definition.Range,
                    hitMask,
                    QueryTriggerInteraction.Ignore))
            {
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

            ReloadStarted?.Invoke();
            return true;
        }

        public void CompleteReload()
        {
            if (Runtime == null || !Runtime.CompleteReload())
            {
                return;
            }

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

        private void EnsureShotFeedbackComponents()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 1f;
                audioSource.rolloffMode = AudioRolloffMode.Linear;
                audioSource.minDistance = 2f;
                audioSource.maxDistance = 45f;
            }

            if (enableProceduralHitImpact && hitImpactPrefab == null)
            {
                EnsureProceduralImpactPool();
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
            if (fireAudioClip != null && audioSource != null)
            {
                audioSource.PlayOneShot(fireAudioClip, fireAudioVolume);
            }

            if (enableMuzzleFlash && muzzleFlashLight != null)
            {
                muzzleFlashLight.intensity = muzzleFlashIntensity * UnityEngine.Random.Range(0.85f, 1.15f);
                muzzleFlashLight.enabled = true;
                muzzleFlashEndTime = Time.time + muzzleFlashDuration;
            }
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

            if (impactPool != null)
            {
                foreach (ProceduralImpactMarker oldMarker in impactPool)
                {
                    if (oldMarker?.GameObject != null)
                    {
                        Destroy(oldMarker.GameObject);
                    }
                }
            }

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
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
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
