using System;
using TacticalEcho.Combat.Weapons;
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
        [Header("Starting Loadout")]
        [Tooltip("Placed into the slots on Awake. Each must be an ItemDefinition of type Weapon with a Weapon Definition assigned.")]
        [SerializeField] private ItemDefinition startingPrimaryWeapon;
        [SerializeField] private ItemDefinition startingSecondaryWeapon;
        [SerializeField] private EquipmentSlot startingSlot = EquipmentSlot.PrimaryWeapon;

        [Header("References")]
        [SerializeField] private InventoryController inventory;
        [SerializeField] private WeaponController weapon;




        private ItemInstance primaryWeapon;
        private ItemInstance secondaryWeapon;

        public event Action<EquipmentSlot, ItemInstance> Equipped;
        public event Action<EquipmentSlot> ActiveSlotChanged;

        public ItemInstance PrimaryWeapon => primaryWeapon;
        public ItemInstance SecondaryWeapon => secondaryWeapon;
        public EquipmentSlot ActiveSlot { get; private set; } = EquipmentSlot.PrimaryWeapon;
        public ItemInstance ActiveItem => GetItem(ActiveSlot);
        public bool HasItem(EquipmentSlot slot) => GetItem(slot) != null;

        private void Awake()
        {
            ResolveReferences();
            ApplyStartingLoadout();
        }

        public void Configure(WeaponController weaponController, InventoryController inventoryController)
        {
            weapon = weaponController;
            inventory = inventoryController;
            ResolveReferences();
            ApplyActiveWeapon();
        }

        public ItemInstance GetItem(EquipmentSlot slot)
        {
            return slot == EquipmentSlot.PrimaryWeapon ? primaryWeapon : secondaryWeapon;
        }

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

            Equipped?.Invoke(slot, item);

            if (slot == ActiveSlot)
            {
                ApplyActiveWeapon();
            }
        }





        public bool TrySetActiveSlot(EquipmentSlot slot)
        {
            if (slot == ActiveSlot || !HasItem(slot))
            {
                return false;
            }

            ActiveSlot = slot;
            ApplyActiveWeapon();
            ActiveSlotChanged?.Invoke(ActiveSlot);
            return true;
        }

        public bool ToggleWeaponSlot()
        {
            return TrySetActiveSlot(
                ActiveSlot == EquipmentSlot.PrimaryWeapon
                    ? EquipmentSlot.SecondaryWeapon
                    : EquipmentSlot.PrimaryWeapon);
        }

        private void ApplyStartingLoadout()
        {
            EquipStartingItem(EquipmentSlot.PrimaryWeapon, startingPrimaryWeapon);
            EquipStartingItem(EquipmentSlot.SecondaryWeapon, startingSecondaryWeapon);

            ActiveSlot = HasItem(startingSlot) ? startingSlot : EquipmentSlot.PrimaryWeapon;
            ApplyActiveWeapon();
        }

        private void EquipStartingItem(EquipmentSlot slot, ItemDefinition itemDefinition)
        {
            if (itemDefinition == null)
            {
                return;
            }

            if (!itemDefinition.IsWeapon)
            {
                Debug.LogWarning(
                    $"[Equipment] \"{itemDefinition.name}\" is assigned to {slot} but is not a Weapon item with a " +
                    "Weapon Definition, so that slot would have nothing to fire. Slot left empty.",
                    this);
                return;
            }

            ItemInstance item = new(itemDefinition);
            inventory?.Add(item);
            Equip(slot, item);
        }

        private void ApplyActiveWeapon()
        {
            if (weapon == null)
            {
                return;
            }

            ItemInstance active = ActiveItem;
            WeaponDefinition weaponDefinition = active?.Definition != null
                ? active.Definition.WeaponDefinition
                : null;

            if (weaponDefinition != null)
            {
                weapon.Equip(weaponDefinition);
            }
        }

        private void ResolveReferences()
        {
            if (inventory == null)
            {
                inventory = GetComponent<InventoryController>();
            }

            if (weapon == null)
            {
                weapon = GetComponentInChildren<WeaponController>(true);
            }
        }
    }
}
