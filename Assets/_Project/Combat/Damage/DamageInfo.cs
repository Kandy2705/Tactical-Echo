using UnityEngine;

namespace TacticalEcho.Combat.Damage
{
    public readonly struct DamageInfo
    {
        public DamageInfo(float amount, Vector3 point, Vector3 normal, GameObject source)
        {
            Amount = amount;
            Point = point;
            Normal = normal;
            Source = source;
        }

        public float Amount { get; }
        public Vector3 Point { get; }
        public Vector3 Normal { get; }
        public GameObject Source { get; }
    }
}
