namespace TacticalEcho.Combat.Damage
{
    public interface IDamageable
    {
        bool IsAlive { get; }
        void ApplyDamage(in DamageInfo damageInfo);
    }
}
