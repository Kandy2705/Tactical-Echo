using System;
using UnityEngine;

namespace TacticalEcho.Combat.Weapons
{
    public sealed class WeaponRuntime
    {
        public WeaponRuntime(WeaponDefinition definition)
        {
            Definition = definition != null ? definition : throw new ArgumentNullException(nameof(definition));
            MagazineAmmo = definition.MagazineSize;
            ReserveAmmo = definition.StartingReserveAmmo;
        }

        public WeaponDefinition Definition { get; }
        public int MagazineAmmo { get; private set; }
        public int ReserveAmmo { get; private set; }
        public bool IsReloading { get; private set; }
        public float NextAllowedFireTime { get; private set; }
        public float AmmoRatio => Definition.MagazineSize <= 0 ? 0f : (float)MagazineAmmo / Definition.MagazineSize;

        public bool CanFire(float currentTime)
        {
            return !IsReloading && MagazineAmmo > 0 && currentTime >= NextAllowedFireTime;
        }

        public bool TryConsumeShot(float currentTime)
        {
            if (!CanFire(currentTime))
            {
                return false;
            }

            MagazineAmmo--;
            NextAllowedFireTime = currentTime + 1f / Mathf.Max(0.01f, Definition.FireRate);
            return true;
        }

        public bool TryBeginReload()
        {
            if (IsReloading || MagazineAmmo >= Definition.MagazineSize || ReserveAmmo <= 0)
            {
                return false;
            }

            IsReloading = true;
            return true;
        }

        public void CompleteReload()
        {
            if (!IsReloading)
            {
                return;
            }

            int needed = Definition.MagazineSize - MagazineAmmo;
            int transferred = Mathf.Min(needed, ReserveAmmo);
            MagazineAmmo += transferred;
            ReserveAmmo -= transferred;
            IsReloading = false;
        }

        public void CancelReload()
        {
            IsReloading = false;
        }
    }
}
