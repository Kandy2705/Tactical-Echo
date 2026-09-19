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

        [Header("Damage Over Time")]
        [Tooltip("Damage applied through the shared damage pipeline (DamageInfo -> IDamageable) each tick while this effect is active. 0 = a pure debuff with no damage-over-time, such as Suppression.")]
        [SerializeField, Min(0f)] private float damagePerTick = 0f;
        [Tooltip("Seconds between damage-over-time ticks. Only relevant when damagePerTick > 0.")]
        [SerializeField, Min(0.05f)] private float tickInterval = 1f;

        [Header("Modifiers")]
        [Tooltip("Movement speed multiplier while active. 1 = no change. Stacks multiply, so two 0.8 stacks give 0.64.")]
        [SerializeField, Range(0.05f, 2f)] private float moveSpeedMultiplier = 1f;

        public string EffectId => effectId;
        public float Duration => duration;
        public int MaxStacks => maxStacks;
        public StatusStackRule StackRule => stackRule;
        public float DamagePerTick => damagePerTick;
        public float TickInterval => tickInterval;

        /// <summary>
        /// Movement speed while this effect is active, as a multiplier of the character's
        /// normal speed. 1 means no movement modifier. This is the modifier channel that
        /// makes an effect change gameplay rather than only tick damage, so a Slow is a new
        /// definition asset rather than new code.
        /// </summary>
        public float MoveSpeedMultiplier => Mathf.Clamp(moveSpeedMultiplier, 0.05f, 2f);
    }
}
