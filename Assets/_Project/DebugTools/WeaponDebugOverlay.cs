using System.Text;
using TacticalEcho.Combat.Weapons;
using UnityEngine;

namespace TacticalEcho.DebugTools
{




    public sealed class WeaponDebugOverlay : DebugOverlayBase
    {
        [Header("Observed")]
        [SerializeField] private WeaponController observedWeapon;
        [Tooltip("Movement used for the sampled effective spread rows. The weapon reads the real value from its firer.")]
        [SerializeField, Range(0f, 1f)] private float sampleMovement01 = 1f;

        protected override string OverlayName => "Weapon Debug";

        protected override void Awake()
        {
            base.Awake();
            if (observedWeapon == null)
            {
                observedWeapon = GetComponentInChildren<WeaponController>(true);
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
