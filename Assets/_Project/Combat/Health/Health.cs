using System;
using TacticalEcho.Combat.Damage;
using UnityEngine;

namespace TacticalEcho.Combat.Health
{
    public sealed class Health : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(1f)] private float maxHealth = 100f;

        public event Action<float, float> HealthChanged;
        public event Action Died;

        public float Current { get; private set; }
        public float Max => maxHealth;
        public float Normalized => maxHealth <= 0f ? 0f : Current / maxHealth;
        public bool IsAlive => Current > 0f;

        private void Awake()
        {
            Current = maxHealth;
        }

        public void ApplyDamage(in DamageInfo damageInfo)
        {
            if (!IsAlive || damageInfo.Amount <= 0f)
            {
                return;
            }

            Current = Mathf.Max(0f, Current - damageInfo.Amount);
            HealthChanged?.Invoke(Current, maxHealth);

            if (Current <= 0f)
            {
                Died?.Invoke();
            }
        }
    }
}
