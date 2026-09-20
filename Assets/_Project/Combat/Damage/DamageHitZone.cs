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



        public const string HitZoneLayerName = "CharacterHitZone";

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

        private void Awake()
        {
            ApplyHitZoneLayer();
        }






        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void IsolateHitZoneLayer()
        {
            int hitZoneLayer = LayerMask.NameToLayer(HitZoneLayerName);
            if (hitZoneLayer < 0)
            {
                Debug.LogWarning(
                    $"[Damage] Layer \"{HitZoneLayerName}\" is missing from Tags and Layers. " +
                    "Body-part colliders will stay solid and can push characters around.");
                return;
            }

            for (int otherLayer = 0; otherLayer < 32; otherLayer++)
            {
                Physics.IgnoreLayerCollision(hitZoneLayer, otherLayer, true);
            }
        }

        private void ApplyHitZoneLayer()
        {
            int hitZoneLayer = LayerMask.NameToLayer(HitZoneLayerName);
            if (hitZoneLayer >= 0 && gameObject.layer != hitZoneLayer)
            {
                gameObject.layer = hitZoneLayer;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            damageMultiplier = Mathf.Max(0f, damageMultiplier);
            ApplyHitZoneLayer();
        }
#endif
    }
}
