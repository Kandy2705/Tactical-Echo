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

    /// <summary>
    /// Owns the primary/secondary weapon slots and which of them is currently in hand.
    /// Equipping decides which <see cref="WeaponDefinition"/> the character's
    /// <see cref="WeaponController"/> fires; the weapon controller still owns firing, ammo
    /// and reload behaviour itself, so this type never touches ballistics.
    /// </summary>
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

        // Runtime only, deliberately not serialized: Unity never deserializes a custom
        // [Serializable] class as null, so a serialized slot would always look occupied and
        // an empty slot could be switched to. The authored loadout above is the data.
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

        /// <summary>
        /// Brings the given slot into hand. Refuses an empty slot so a character can never
        /// end up holding nothing because of a stray input.
        /// </summary>
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
