using System.Collections.Generic;

namespace Systems.GridInventory
{
    public static class PlayerItemInventoryQuery
    {
        private const int QuickslotCount = 4;

        public static int CountItem(ItemData itemData)
        {
            if (itemData == null)
                return 0;

            int count = 0;
            HashSet<ItemInstance> processedItems = new HashSet<ItemInstance>();

            AddGridItemCounts(itemData, processedItems, ref count);
            AddQuickslotItemCounts(itemData, processedItems, ref count);

            return count;
        }

        public static bool HasItem(ItemData itemData, int amount = 1)
        {
            if (amount <= 0)
                return true;

            return CountItem(itemData) >= amount;
        }

        public static bool TryConsumeItem(ItemData itemData, int amount = 1)
        {
            if (itemData == null)
                return false;

            if (amount <= 0)
                return true;

            if (!HasItem(itemData, amount))
                return false;

            int remaining = amount;
            HashSet<ItemInstance> processedItems = new HashSet<ItemInstance>();

            ConsumeFromGrid(itemData, processedItems, ref remaining);
            if (remaining > 0)
            {
                ConsumeFromQuickslots(itemData, processedItems, ref remaining);
            }

            return remaining <= 0;
        }

        public static Dictionary<ItemData, int> BuildItemCounts()
        {
            Dictionary<ItemData, int> itemCounts = new Dictionary<ItemData, int>();
            HashSet<ItemInstance> processedItems = new HashSet<ItemInstance>();

            AddGridItemCounts(processedItems, itemCounts);
            AddQuickslotItemCounts(processedItems, itemCounts);

            return itemCounts;
        }

        private static void AddGridItemCounts(ItemData targetData, HashSet<ItemInstance> processedItems, ref int count)
        {
            GridInventoryModel model = GridInventory.Instance?.Controller?.Model;
            if (model == null)
                return;

            for (int i = 0; i < model.Items.Length; i++)
            {
                ItemInstance item = model.Get(i);
                if (item == null || item.Data != targetData || !processedItems.Add(item))
                    continue;

                count += item.currentStackCount;
            }
        }

        private static void AddQuickslotItemCounts(ItemData targetData, HashSet<ItemInstance> processedItems, ref int count)
        {
            QuickslotUIController quickslot = QuickslotUIController.Instance;
            if (quickslot == null)
                return;

            for (int i = 0; i < QuickslotCount; i++)
            {
                ItemInstance item = quickslot.GetItem(i);
                if (item == null || item.Data != targetData || !processedItems.Add(item))
                    continue;

                count += item.currentStackCount;
            }
        }

        private static void AddGridItemCounts(HashSet<ItemInstance> processedItems, Dictionary<ItemData, int> itemCounts)
        {
            GridInventoryModel model = GridInventory.Instance?.Controller?.Model;
            if (model == null)
                return;

            for (int i = 0; i < model.Items.Length; i++)
            {
                AddItemCount(model.Get(i), processedItems, itemCounts);
            }
        }

        private static void AddQuickslotItemCounts(HashSet<ItemInstance> processedItems, Dictionary<ItemData, int> itemCounts)
        {
            QuickslotUIController quickslot = QuickslotUIController.Instance;
            if (quickslot == null)
                return;

            for (int i = 0; i < QuickslotCount; i++)
            {
                AddItemCount(quickslot.GetItem(i), processedItems, itemCounts);
            }
        }

        private static void AddItemCount(ItemInstance item, HashSet<ItemInstance> processedItems, Dictionary<ItemData, int> itemCounts)
        {
            if (item == null || item.Data == null || !processedItems.Add(item))
                return;

            if (!itemCounts.ContainsKey(item.Data))
            {
                itemCounts[item.Data] = 0;
            }

            itemCounts[item.Data] += item.currentStackCount;
        }

        private static void ConsumeFromGrid(ItemData itemData, HashSet<ItemInstance> processedItems, ref int remaining)
        {
            GridInventoryModel model = GridInventory.Instance?.Controller?.Model;
            if (model == null)
                return;

            for (int i = 0; i < model.Items.Length && remaining > 0; i++)
            {
                ItemInstance item = model.Get(i);
                if (item == null || item.Data != itemData || !processedItems.Add(item))
                    continue;

                int consumed = System.Math.Min(remaining, item.currentStackCount);
                item.currentStackCount -= consumed;
                remaining -= consumed;

                if (item.currentStackCount <= 0)
                {
                    model.TryRemove(item);
                }
                else
                {
                    model.Items.Invoke();
                }

                RefreshQuickslotReferences(item);
            }
        }

        private static void ConsumeFromQuickslots(ItemData itemData, HashSet<ItemInstance> processedItems, ref int remaining)
        {
            QuickslotUIController quickslot = QuickslotUIController.Instance;
            if (quickslot == null)
                return;

            for (int i = 0; i < QuickslotCount && remaining > 0; i++)
            {
                ItemInstance item = quickslot.GetItem(i);
                if (item == null || item.Data != itemData || !processedItems.Add(item))
                    continue;

                int consumed = System.Math.Min(remaining, item.currentStackCount);
                item.currentStackCount -= consumed;
                remaining -= consumed;

                RefreshQuickslotReferences(item);
            }
        }

        private static void RefreshQuickslotReferences(ItemInstance item)
        {
            QuickslotUIController quickslot = QuickslotUIController.Instance;
            if (quickslot == null || item == null)
                return;

            for (int i = 0; i < QuickslotCount; i++)
            {
                if (quickslot.GetItem(i) != item)
                    continue;

                if (item.currentStackCount <= 0)
                {
                    quickslot.RemoveItemFromSlot(i);
                }
                else
                {
                    quickslot.RefreshSlotVisual(i);
                }
            }
        }
    }
}
