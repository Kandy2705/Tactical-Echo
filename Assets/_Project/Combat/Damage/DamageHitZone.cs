using UnityEngine;

namespace TacticalEcho.Combat.Damage
{
    public enum DamageHitZoneType
    {
        Generic,
        Torso,
        Head,
        Arm,
        Leg
    }

    public sealed class DamageHitZone : MonoBehaviour
    {
        [SerializeField] private DamageHitZoneType zoneType = DamageHitZoneType.Generic;
        [SerializeField, Min(0f)] private float damageMultiplier = 1f;

        public DamageHitZoneType ZoneType => zoneType;
        public float DamageMultiplier => Mathf.Max(0f, damageMultiplier);
        public bool IsCritical => zoneType == DamageHitZoneType.Head;

        public void Configure(DamageHitZoneType type, float multiplier)
        {
            zoneType = type;
            damageMultiplier = Mathf.Max(0f, multiplier);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            damageMultiplier = Mathf.Max(0f, damageMultiplier);
        }
#endif
    }
}
