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
        public float ReloadCompleteTime { get; private set; }
        public float AmmoRatio => Definition.MagazineSize <= 0 ? 0f : (float)MagazineAmmo / Definition.MagazineSize;
        public bool IsMagazineEmpty => MagazineAmmo <= 0;
        public bool HasReserveAmmo => ReserveAmmo > 0;

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

        public bool TryBeginReload(float currentTime)
        {
            if (IsReloading || MagazineAmmo >= Definition.MagazineSize || ReserveAmmo <= 0)
            {
                return false;
            }

            IsReloading = true;
            ReloadCompleteTime = currentTime + Definition.ReloadTime;
            return true;
        }

        public bool ShouldCompleteReload(float currentTime)
        {
            return IsReloading && currentTime >= ReloadCompleteTime;
        }

        public bool CompleteReload()
        {
            if (!IsReloading)
            {
                return false;
            }

            int needed = Definition.MagazineSize - MagazineAmmo;
            int transferred = Mathf.Min(needed, ReserveAmmo);
            MagazineAmmo += transferred;
            ReserveAmmo -= transferred;
            IsReloading = false;
            ReloadCompleteTime = 0f;
            return true;
        }

        public void CancelReload()
        {
            IsReloading = false;
            ReloadCompleteTime = 0f;
        }
    }
}
