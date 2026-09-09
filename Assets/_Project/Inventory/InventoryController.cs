using System.Collections.Generic;
using TacticalEcho.Inventory.Items;
using UnityEngine;

namespace TacticalEcho.Inventory
{
    public sealed class InventoryController : MonoBehaviour
    {
        [SerializeField] private List<ItemInstance> items = new();

        public IReadOnlyList<ItemInstance> Items => items;

        public void Add(ItemInstance item)
        {
            if (item != null)
            {
                items.Add(item);
            }
        }

        public bool Remove(ItemInstance item)
        {
            return item != null && items.Remove(item);
        }
    }
}
