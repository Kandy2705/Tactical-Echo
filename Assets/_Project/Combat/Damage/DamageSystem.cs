using UnityEngine;

namespace TacticalEcho.Combat.Damage
{
    public static class DamageSystem
    {
        public static bool TryApply(Collider hitCollider, in DamageInfo damageInfo)
        {
            if (hitCollider == null)
            {
                return false;
            }

            IDamageable damageable = hitCollider.GetComponentInParent<IDamageable>();
            if (damageable == null || !damageable.IsAlive)
            {
                return false;
            }

            damageable.ApplyDamage(damageInfo);
            return true;
        }
    }
}
