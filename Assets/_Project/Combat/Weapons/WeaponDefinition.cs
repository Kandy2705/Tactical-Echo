using UnityEngine;

namespace TacticalEcho.Combat.Weapons
{
    [CreateAssetMenu(menuName = "Tactical Echo/Combat/Weapon Definition", fileName = "WeaponDefinition")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string weaponId = "rifle";

        [Header("Ballistics")]
        [SerializeField, Min(0f)] private float damage = 20f;
        [SerializeField, Min(0.1f)] private float range = 100f;
        [SerializeField, Min(0.01f)] private float fireRate = 8f;
        [SerializeField] private FireMode fireMode = FireMode.Automatic;

        [Header("Ammo")]
        [SerializeField, Min(1)] private int magazineSize = 30;
        [SerializeField, Min(0)] private int startingReserveAmmo = 90;
        [SerializeField, Min(0.01f)] private float reloadTime = 2f;

        [Header("Handling")]
        [SerializeField, Min(0f)] private float baseSpread = 0.25f;
        [SerializeField, Min(0f)] private float recoil = 1f;

        [Header("AI Hearing")]
        [SerializeField, Min(0f)] private float noiseRadius = 30f;

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
    }
}
