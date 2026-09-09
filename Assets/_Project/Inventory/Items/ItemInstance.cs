using System;

namespace TacticalEcho.Inventory.Items
{
    [Serializable]
    public sealed class ItemInstance
    {
        public ItemInstance(ItemDefinition definition, int quantity = 1)
        {
            InstanceId = Guid.NewGuid().ToString("N");
            Definition = definition;
            Quantity = quantity;
        }

        public string InstanceId;
        public ItemDefinition Definition;
        public int Quantity;
    }
}
