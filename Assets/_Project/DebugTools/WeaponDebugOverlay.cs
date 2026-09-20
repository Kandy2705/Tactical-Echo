using System.Text;
using TacticalEcho.Combat.Weapons;
using UnityEngine;

namespace TacticalEcho.DebugTools
{




    // Read-only view of one WeaponController: ammo, reload timing, and the spread cone (still/hip/aim/
    // moving rows) that actually drives shot direction in WeaponController.ApplySpread.
    public sealed class WeaponDebugOverlay : DebugOverlayBase
    {
        [Header("Observed")]
        [SerializeField] private WeaponController observedWeapon;
        [Tooltip("Movement used for the sampled effective spread rows. The weapon reads the real value from its firer.")]
        [SerializeField, Range(0f, 1f)] private float sampleMovement01 = 1f;

        protected override string OverlayName => "Weapon Debug";

        // Prefers a WeaponController on this object/its children (the intended placement - drop this on the
        // character that owns the weapon); falls back to a scene-wide search so it still works as a
        // standalone GameObject dropped anywhere in the scene.
        protected override void Awake()
        {
            base.Awake();
            if (observedWeapon == null)
            {
                observedWeapon = GetComponentInChildren<WeaponController>(true);
            }

            if (observedWeapon == null)
            {
                observedWeapon = FindFirstObjectByType<WeaponController>();
            }
        }

        public void Observe(WeaponController weapon)
        {
            observedWeapon = weapon;
        }

        protected override void BuildText(StringBuilder text)
        {
            if (observedWeapon == null)
            {
                text.AppendLine("no WeaponController observed");
                return;
            }

            WeaponDefinition definition = observedWeapon.Definition;
            WeaponRuntime runtime = observedWeapon.Runtime;

            if (definition == null || runtime == null)
            {
                text.AppendLine("weapon has no definition");
                return;
            }

            text.Append("Weapon: ").Append(definition.WeaponId)
                .Append("  [").Append(definition.FireMode).AppendLine("]");
            text.Append("Damage ").Append(definition.Damage.ToString("0.#"))
                .Append("   Range ").Append(definition.Range.ToString("0.#"))
                .Append("   RPS ").AppendLine(definition.FireRate.ToString("0.##"));

            text.Append("Ammo: ").Append(runtime.MagazineAmmo).Append(" / ").Append(definition.MagazineSize)
                .Append("   Reserve ").AppendLine(runtime.ReserveAmmo.ToString());

            if (runtime.IsReloading)
            {
                text.Append("Reloading, ").Append(Seconds(runtime.ReloadCompleteTime - Time.time)).AppendLine(" left");
            }
            else
            {
                text.Append("Can fire: ").AppendLine(runtime.CanFire(Time.time) ? "yes" : "no");
            }

            float cooldown = runtime.NextAllowedFireTime - Time.time;
            if (cooldown > 0f)
            {
                text.Append("Fire cooldown: ").AppendLine(Seconds(cooldown));
            }

            text.Append("Spread now: ").Append(runtime.CurrentSpread.ToString("0.000"))
                .Append("  (base ").Append(definition.BaseSpread.ToString("0.000"))
                .Append(", max ").Append(definition.MaxSpread.ToString("0.000")).AppendLine(")");
            text.Append("  still, hip: ").AppendLine(runtime.GetEffectiveSpread(0f, false).ToString("0.000"));
            text.Append("  still, aim: ").AppendLine(runtime.GetEffectiveSpread(0f, true).ToString("0.000"));
            text.Append("  moving ").Append(sampleMovement01.ToString("0.00")).Append(", hip: ")
                .AppendLine(runtime.GetEffectiveSpread(sampleMovement01, false).ToString("0.000"));
        }
    }
}
