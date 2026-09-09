using TacticalEcho.Inventory.Items;
using UnityEngine;

namespace TacticalEcho.Inventory.Equipment
{
    public enum EquipmentSlot
    {
        PrimaryWeapon,
        SecondaryWeapon
    }

    public sealed class EquipmentController : MonoBehaviour
    {
        [SerializeField] private ItemInstance primaryWeapon;
        [SerializeField] private ItemInstance secondaryWeapon;

        public ItemInstance PrimaryWeapon => primaryWeapon;
        public ItemInstance SecondaryWeapon => secondaryWeapon;

        public void Equip(EquipmentSlot slot, ItemInstance item)
        {
            if (item == null)
            {
                return;
            }

            if (slot == EquipmentSlot.PrimaryWeapon)
            {
                primaryWeapon = item;
            }
            else
            {
                secondaryWeapon = item;
            }
        }
    }
}
