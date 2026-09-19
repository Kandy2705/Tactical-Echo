using System.Collections.Generic;
using System.Text;
using TacticalEcho.Combat.StatusEffects;
using UnityEngine;

namespace TacticalEcho.DebugTools
{
    
    
    
    
    public sealed class StatusEffectDebugOverlay : DebugOverlayBase
    {
        [Header("Observed")]
        [SerializeField] private StatusEffectController observedEffects;

        protected override string OverlayName => "Status Debug";

        protected override void Awake()
        {
            base.Awake();
            if (observedEffects == null)
            {
                observedEffects = GetComponentInChildren<StatusEffectController>(true);
            }
        }

        public void Observe(StatusEffectController effects)
        {
            observedEffects = effects;
        }

        protected override void BuildText(StringBuilder text)
        {
            if (observedEffects == null)
            {
                text.AppendLine("no StatusEffectController observed");
                return;
            }

            IReadOnlyList<StatusEffectInstance> active = observedEffects.ActiveEffects;
            if (active == null || active.Count == 0)
            {
                text.AppendLine("no active effects");
                return;
            }

            for (int index = 0; index < active.Count; index++)
            {
                StatusEffectInstance instance = active[index];
                if (instance?.Definition == null)
                {
                    continue;
                }

                StatusEffectDefinition definition = instance.Definition;

                text.Append(definition.EffectId)
                    .Append("  x").Append(instance.StackCount).Append('/').Append(definition.MaxStacks)
                    .Append("  ").Append(Seconds(instance.Remaining)).AppendLine(" left");

                if (definition.DamagePerTick > 0f)
                {
                    text.Append("   ").Append(definition.DamagePerTick.ToString("0.#"))
                        .Append(" dmg every ").Append(Seconds(definition.TickInterval))
                        .Append(", next in ").AppendLine(Seconds(instance.NextTickAt - Time.time));
                }

                text.Append("   rule ").AppendLine(definition.StackRule.ToString());
            }
        }
    }
}
