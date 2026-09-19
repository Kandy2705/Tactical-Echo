using System;
using TacticalEcho.Combat.Damage;
using UnityEngine;

namespace TacticalEcho.Combat.Health
{
    public sealed class Health : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(1f)] private float maxHealth = 100f;

        private bool initialized;

        public event Action<float, float> HealthChanged;
        public event Action Died;

        public float Current { get; private set; }
        public float Max => maxHealth;
        public float Normalized => maxHealth <= 0f ? 0f : Current / maxHealth;

        
        
        
        
        
        
        
        public bool IsAlive => !initialized || Current > 0f;

        private void Awake()
        {
            EnsureInitialized();
        }

        public void ApplyDamage(in DamageInfo damageInfo)
        {
            EnsureInitialized();

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

        private void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            Current = maxHealth;
        }
    }
}
