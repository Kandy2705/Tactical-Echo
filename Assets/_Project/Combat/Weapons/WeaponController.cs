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

        private void Awake()
        {
            InitializeRuntime();
        }

        private void Update()
        {
            if (Runtime != null && Runtime.ShouldCompleteReload(Time.time))
            {
                CompleteReload();
            }
        }

        private void OnDisable()
        {
            Runtime?.CancelReload();
        }

        public void Configure(WeaponDefinition weaponDefinition, Transform muzzleTransform)
        {
            definition = weaponDefinition;
            muzzle = muzzleTransform;
            InitializeRuntime();
        }

        public bool TryFire(Vector3 origin, Vector3 direction)
        {
            if (Runtime == null || definition == null || !Runtime.TryConsumeShot(Time.time))
            {
                return false;
            }

            Vector3 shotDirection = direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : transform.forward;

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
                HitResolved?.Invoke(hit);
            }

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
