using System;
using System.Collections.Generic;
using TacticalEcho.Combat.Damage;
using UnityEngine;

namespace TacticalEcho.Combat.StatusEffects
{
    public sealed class StatusEffectController : MonoBehaviour
    {
        private readonly List<StatusEffectInstance> activeEffects = new();
        private IDamageable damageable;

        public event Action EffectsChanged;
        public IReadOnlyList<StatusEffectInstance> ActiveEffects => activeEffects;

        private void Awake()
        {
            // Damage-over-time effects (Bleed) flow through whatever IDamageable already owns
            // this target's HP, so Health stays the single owner of HP mutation.
            damageable = GetComponent<IDamageable>();
        }

        private void Update()
        {
            for (int i = activeEffects.Count - 1; i >= 0; i--)
            {
                StatusEffectInstance effect = activeEffects[i];

                if (effect.TryConsumeTick(Time.time, out float tickDamage))
                {
                    ApplyTickDamage(tickDamage);
                }

                if (!effect.IsExpired)
                {
                    continue;
                }

                activeEffects.RemoveAt(i);
                EffectsChanged?.Invoke();
            }
        }

        public void Apply(StatusEffectDefinition definition)
        {
            if (definition == null)
            {
                return;
            }

            StatusEffectInstance existing = activeEffects.Find(effect => effect.Definition.EffectId == definition.EffectId);
            if (existing == null)
            {
                activeEffects.Add(new StatusEffectInstance(definition, Time.time));
                EffectsChanged?.Invoke();
                return;
            }

            switch (definition.StackRule)
            {
                case StatusStackRule.AddStack:
                    existing.AddStack(Time.time);
                    break;
                case StatusStackRule.Replace:
                    activeEffects.Remove(existing);
                    activeEffects.Add(new StatusEffectInstance(definition, Time.time));
                    break;
                default:
                    existing.Refresh(Time.time);
                    break;
            }

            EffectsChanged?.Invoke();
        }

        /// <summary>
        /// Looks up an active effect by <see cref="StatusEffectDefinition.EffectId"/>. Consumers
        /// such as EnemyBrain use this to read a normalized value (e.g. Suppression) instead of
        /// hard-coding effect-specific state of their own.
        /// </summary>
        public bool HasEffect(string effectId, out StatusEffectInstance instance)
        {
            instance = activeEffects.Find(effect => effect.Definition.EffectId == effectId);
            return instance != null;
        }

        private void ApplyTickDamage(float amount)
        {
            if (damageable == null || !damageable.IsAlive || amount <= 0f)
            {
                return;
            }

            // Reuse the shared damage pipeline (DamageInfo -> IDamageable) instead of mutating
            // Health directly, so Bleed is an extension of the existing pipeline, not a second
            // damage system living next to it.
            damageable.ApplyDamage(new DamageInfo(amount, transform.position, Vector3.up, gameObject));
        }
    }
}
