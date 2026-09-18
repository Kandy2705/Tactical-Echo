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

    /// <summary>
    /// Marks a collider as a body part for the shared damage pipeline: it supplies the damage
    /// multiplier and critical flag that <see cref="DamageSystem"/> reads after a weapon raycast.
    /// </summary>
    /// <remarks>
    /// Hit zones are colliders parented to animated bones, so they teleport every frame instead of
    /// sweeping. Left on a normal collision layer they behave as solid moving geometry and a
    /// CharacterController that touches one is depenetrated by the whole overlap depth in a single
    /// frame - which launches the player into the air whenever an enemy walks into them. They exist
    /// only to be found by raycasts, and raycasts ignore the layer collision matrix, so this type
    /// owns a dedicated layer that collides with nothing while staying fully shootable.
    /// </remarks>
    public sealed class DamageHitZone : MonoBehaviour
    {
        /// <summary>
        /// Layer reserved for body-part colliders. Must exist in Tags and Layers.
        /// </summary>
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

        /// <summary>
        /// Takes the hit zone layer out of every physics collision pair once per play session.
        /// Raycasts are unaffected by the collision matrix, so weapons keep hitting body parts
        /// while nothing is ever pushed by them.
        /// </summary>
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
