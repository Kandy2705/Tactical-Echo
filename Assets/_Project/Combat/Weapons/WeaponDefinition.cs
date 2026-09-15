using UnityEngine;

namespace TacticalEcho.Combat.Weapons
{
    [CreateAssetMenu(menuName = "Tactical Echo/Combat/Weapon Definition", fileName = "WeaponDefinition")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string weaponId = "rifle_hk416";

        [Header("Ballistics")]
        [SerializeField, Min(0f)] private float damage = 24f;
        [SerializeField, Min(0.1f)] private float range = 120f;
        [Tooltip("Rounds fired per second.")]
        [SerializeField, Min(0.01f)] private float fireRate = 10f;
        [SerializeField] private FireMode fireMode = FireMode.Automatic;

        [Header("Ammo")]
        [SerializeField, Min(1)] private int magazineSize = 30;
        [SerializeField, Min(0)] private int startingReserveAmmo = 90;
        [SerializeField, Min(0.01f)] private float reloadTime = 2.1f;

        [Header("Spread")]
        [Tooltip("Minimum cone angle in degrees.")]
        [SerializeField, Min(0f)] private float baseSpread = 0.25f;
        [Tooltip("Maximum cone angle after movement/continuous fire.")]
        [SerializeField, Min(0f)] private float maxSpread = 2.2f;
        [Tooltip("Additional spread added after each successful shot.")]
        [SerializeField, Min(0f)] private float spreadPerShot = 0.16f;
        [Tooltip("Spread recovered per second while not firing.")]
        [SerializeField, Min(0f)] private float spreadRecoveryPerSecond = 1.35f;
        [Tooltip("Additional spread at full movement speed.")]
        [SerializeField, Min(0f)] private float movementSpread = 0.65f;
        [Tooltip("Multiplier applied to spread while aiming over the shoulder.")]
        [SerializeField, Range(0.1f, 1f)] private float aimSpreadMultiplier = 0.65f;

        [Header("Recoil")]
        [SerializeField, Min(0f)] private float recoil = 1f;

        [Header("AI Hearing")]
        [SerializeField, Min(0f)] private float noiseRadius = 32f;
        [SerializeField, Range(0f, 2f)] private float noiseIntensity = 1f;

        public string WeaponId => weaponId;
        public float Damage => damage;
        public float Range => range;
        public float FireRate => fireRate;
        public FireMode FireMode => fireMode;
        public int MagazineSize => magazineSize;
        public int StartingReserveAmmo => startingReserveAmmo;
        public float ReloadTime => reloadTime;
        public float BaseSpread => baseSpread;
        public float MaxSpread => Mathf.Max(baseSpread, maxSpread);
        public float SpreadPerShot => spreadPerShot;
        public float SpreadRecoveryPerSecond => spreadRecoveryPerSecond;
        public float MovementSpread => movementSpread;
        public float AimSpreadMultiplier => aimSpreadMultiplier;
        public float Recoil => recoil;
        public float NoiseRadius => noiseRadius;
        public float NoiseIntensity => noiseIntensity;

#if UNITY_EDITOR
        private void OnValidate()
        {
            weaponId = string.IsNullOrWhiteSpace(weaponId) ? name.ToLowerInvariant().Replace(' ', '_') : weaponId.Trim();
            damage = Mathf.Max(0f, damage);
            range = Mathf.Max(0.1f, range);
            fireRate = Mathf.Max(0.01f, fireRate);
            magazineSize = Mathf.Max(1, magazineSize);
            startingReserveAmmo = Mathf.Max(0, startingReserveAmmo);
            reloadTime = Mathf.Max(0.01f, reloadTime);
            baseSpread = Mathf.Max(0f, baseSpread);
            maxSpread = Mathf.Max(baseSpread, maxSpread);
            spreadPerShot = Mathf.Max(0f, spreadPerShot);
            spreadRecoveryPerSecond = Mathf.Max(0f, spreadRecoveryPerSecond);
            movementSpread = Mathf.Max(0f, movementSpread);
            aimSpreadMultiplier = Mathf.Clamp(aimSpreadMultiplier, 0.1f, 1f);
            recoil = Mathf.Max(0f, recoil);
            noiseRadius = Mathf.Max(0f, noiseRadius);
            noiseIntensity = Mathf.Clamp(noiseIntensity, 0f, 2f);
        }
#endif
    }
}
