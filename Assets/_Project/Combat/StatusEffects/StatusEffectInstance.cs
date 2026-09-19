using UnityEngine;

namespace TacticalEcho.Combat.StatusEffects
{
    public sealed class StatusEffectInstance
    {
        public StatusEffectInstance(StatusEffectDefinition definition, float startTime)
        {
            Definition = definition;
            StackCount = 1;
            ExpiresAt = startTime + definition.Duration;
            NextTickAt = startTime + definition.TickInterval;
        }

        public StatusEffectDefinition Definition { get; }
        public int StackCount { get; private set; }
        public float ExpiresAt { get; private set; }
        public float NextTickAt { get; private set; }
        public float Remaining => Mathf.Max(0f, ExpiresAt - Time.time);
        public bool IsExpired => Time.time >= ExpiresAt;

        public void Refresh(float currentTime)
        {
            ExpiresAt = currentTime + Definition.Duration;
        }

        public void AddStack(float currentTime)
        {
            StackCount = Mathf.Min(Definition.MaxStacks, StackCount + 1);
            Refresh(currentTime);
        }

        
        
        
        
        
        public bool TryConsumeTick(float currentTime, out float tickDamage)
        {
            tickDamage = 0f;
            if (Definition.DamagePerTick <= 0f || currentTime < NextTickAt)
            {
                return false;
            }

            tickDamage = Definition.DamagePerTick * StackCount;
            NextTickAt = currentTime + Definition.TickInterval;
            return true;
        }
    }
}
