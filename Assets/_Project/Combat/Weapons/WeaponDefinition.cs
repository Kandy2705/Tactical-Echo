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

        [Header("Handling - used by later shooting polish")]
        [SerializeField, Min(0f)] private float baseSpread = 0.25f;
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
            noiseRadius = Mathf.Max(0f, noiseRadius);
            noiseIntensity = Mathf.Clamp(noiseIntensity, 0f, 2f);
        }
#endif
    }
}
