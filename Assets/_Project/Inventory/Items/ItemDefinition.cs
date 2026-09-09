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

        public string ItemId => itemId;
        public string DisplayName => displayName;
        public ItemType ItemType => itemType;
        public int MaxStack => maxStack;
        public Sprite Icon => icon;
    }
}
