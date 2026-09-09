using System;
using System.Collections.Generic;
using UnityEngine;

namespace TacticalEcho.Combat.StatusEffects
{
    public sealed class StatusEffectController : MonoBehaviour
    {
        private readonly List<StatusEffectInstance> activeEffects = new();

        public event Action EffectsChanged;
        public IReadOnlyList<StatusEffectInstance> ActiveEffects => activeEffects;

        private void Update()
        {
            for (int i = activeEffects.Count - 1; i >= 0; i--)
            {
                if (!activeEffects[i].IsExpired)
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
    }
}
