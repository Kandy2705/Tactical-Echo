using TacticalEcho.Combat.Weapons;
using UnityEngine;

namespace TacticalEcho.Inventory.Items
{
    public enum ItemType
    {
        Weapon,
        Ammo,
        Attachment,
        Utility
    }

    [CreateAssetMenu(menuName = "Tactical Echo/Inventory/Item Definition", fileName = "ItemDefinition")]
    public sealed class ItemDefinition : ScriptableObject
    {
        [SerializeField] private string itemId = "item";
        [SerializeField] private string displayName = "Item";
        [SerializeField] private ItemType itemType;
        [SerializeField, Min(1)] private int maxStack = 1;
        [SerializeField] private Sprite icon;

        [Header("Weapon")]
        [Tooltip("Required for Weapon items. The combat configuration WeaponController fires when this item is the active equipment slot.")]
        [SerializeField] private WeaponDefinition weaponDefinition;

        public string ItemId => itemId;
        public string DisplayName => displayName;
        public ItemType ItemType => itemType;
        public int MaxStack => maxStack;
        public Sprite Icon => icon;

        /// <summary>
        /// The combat side of a weapon item. Inventory owns what the item *is*;
        /// <see cref="WeaponDefinition"/> owns how it shoots. This reference is the single
        /// link between the two, so equipping never duplicates ballistics data.
        /// </summary>
        public WeaponDefinition WeaponDefinition => weaponDefinition;

        public bool IsWeapon => itemType == ItemType.Weapon && weaponDefinition != null;
    }
}
