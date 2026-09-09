using System;
using TacticalEcho.Combat.Damage;
using TacticalEcho.Core.Events;
using UnityEngine;

namespace TacticalEcho.Combat.Weapons
{
    public sealed class WeaponController : MonoBehaviour
    {
        [SerializeField] private WeaponDefinition definition;
        [SerializeField] private Transform muzzle;
        [SerializeField] private LayerMask hitMask = ~0;

        public event Action Fired;
        public event Action ReloadStarted;
        public event Action ReloadCompleted;

        public WeaponRuntime Runtime { get; private set; }
        public WeaponDefinition Definition => definition;

        private void Awake()
        {
            if (definition != null)
            {
                Runtime = new WeaponRuntime(definition);
            }
        }

        public bool TryFire(Vector3 origin, Vector3 direction)
        {
            if (Runtime == null || !Runtime.TryConsumeShot(Time.time))
            {
                return false;
            }

            Vector3 normalizedDirection = direction.sqrMagnitude > 0f ? direction.normalized : transform.forward;

            if (Physics.Raycast(origin, normalizedDirection, out RaycastHit hit, definition.Range, hitMask, QueryTriggerInteraction.Ignore))
            {
                IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
                damageable?.ApplyDamage(new DamageInfo(definition.Damage, hit.point, hit.normal, gameObject));
            }

            Vector3 noisePosition = muzzle != null ? muzzle.position : transform.position;
            NoiseEventHub.Emit(new NoiseEventData(noisePosition, definition.NoiseRadius, 1f, gameObject));
            Fired?.Invoke();
            return true;
        }

        public bool TryBeginReload()
        {
            if (Runtime == null || !Runtime.TryBeginReload())
            {
                return false;
            }

            ReloadStarted?.Invoke();
            return true;
        }

        public void CompleteReload()
        {
            if (Runtime == null || !Runtime.IsReloading)
            {
                return;
            }

            Runtime.CompleteReload();
            ReloadCompleted?.Invoke();
        }
    }
}
