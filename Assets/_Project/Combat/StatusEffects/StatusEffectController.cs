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

        
        
        
        
        
        public float MoveSpeedMultiplier
        {
            get
            {
                float multiplier = 1f;

                for (int i = 0; i < activeEffects.Count; i++)
                {
                    StatusEffectInstance instance = activeEffects[i];
                    if (instance?.Definition == null)
                    {
                        continue;
                    }

                    float perStack = instance.Definition.MoveSpeedMultiplier;
                    if (Mathf.Approximately(perStack, 1f))
                    {
                        continue;
                    }

                    for (int stack = 0; stack < instance.StackCount; stack++)
                    {
                        multiplier *= perStack;
                    }
                }

                return Mathf.Clamp(multiplier, 0.05f, 2f);
            }
        }

        private void Awake()
        {
            
            
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

            
            
            
            damageable.ApplyDamage(new DamageInfo(amount, transform.position, Vector3.up, gameObject));
        }
    }
}
