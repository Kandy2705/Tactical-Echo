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

        [Header("Muzzle Flash")]
        [SerializeField] private bool enableMuzzleFlash = true;
        [SerializeField, Min(0.01f)] private float muzzleFlashDuration = 0.035f;
        [SerializeField, Min(0f)] private float muzzleFlashIntensity = 3f;
        [SerializeField, Min(0f)] private float muzzleFlashRange = 2.5f;

        public event Action Fired;
        public event Action<RaycastHit> HitResolved;
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
        }

        private void OnDisable()
        {
            Runtime?.CancelReload();

            if (muzzleFlashLight != null)
            {
                muzzleFlashLight.enabled = false;
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

                DamageSystem.TryApply(hit.collider, damageInfo);
                SpawnHitImpact(hit);
                HitResolved?.Invoke(hit);
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
            if (hitImpactPrefab == null)
            {
                return;
            }

            Quaternion rotation = Quaternion.LookRotation(hit.normal);
            GameObject impact = Instantiate(hitImpactPrefab, hit.point + hit.normal * 0.002f, rotation);
            Destroy(impact, hitImpactLifetime);
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
