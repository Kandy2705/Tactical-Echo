using UnityEngine;

namespace TacticalEcho.Combat.Damage
{
    public readonly struct DamageFeedback
    {
        public DamageFeedback(float amount, Vector3 point, DamageHitZoneType zoneType, bool isCritical)
        {
            Amount = amount;
            Point = point;
            ZoneType = zoneType;
            IsCritical = isCritical;
        }

        public float Amount { get; }
        public Vector3 Point { get; }
        public DamageHitZoneType ZoneType { get; }
        public bool IsCritical { get; }
    }
}
