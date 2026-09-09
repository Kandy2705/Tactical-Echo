using UnityEngine;

namespace TacticalEcho.Combat.StatusEffects
{
    public enum StatusStackRule
    {
        RefreshDuration,
        AddStack,
        Replace
    }

    [CreateAssetMenu(menuName = "Tactical Echo/Combat/Status Effect Definition", fileName = "StatusEffectDefinition")]
    public sealed class StatusEffectDefinition : ScriptableObject
    {
        [SerializeField] private string effectId = "effect";
        [SerializeField, Min(0.01f)] private float duration = 3f;
        [SerializeField, Min(1)] private int maxStacks = 1;
        [SerializeField] private StatusStackRule stackRule = StatusStackRule.RefreshDuration;

        public string EffectId => effectId;
        public float Duration => duration;
        public int MaxStacks => maxStacks;
        public StatusStackRule StackRule => stackRule;
    }
}
