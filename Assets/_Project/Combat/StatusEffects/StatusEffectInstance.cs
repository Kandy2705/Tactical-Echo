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
        }

        public StatusEffectDefinition Definition { get; }
        public int StackCount { get; private set; }
        public float ExpiresAt { get; private set; }
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
    }
}
