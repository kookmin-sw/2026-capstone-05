using System.Collections.Generic;
using Systems.GridInventory;
using UnityEngine;

namespace Systems.Loot
{
    public static class LootGridSerializer
    {
        public static LootSaveData ToSaveData(string lootId, GridInventoryModel model)
        {
            LootSaveData data = LootSaveManager.CreateEmpty(lootId, model.Width, model.Height);
            HashSet<ItemInstance> processed = new HashSet<ItemInstance>();

            for (int i = 0; i < model.Items.Length; i++)
            {
                ItemInstance item = model.Get(i);
                if (item == null || item.Data == null || processed.Contains(item))
                {
                    continue;
                }

                processed.Add(item);
                (int x, int y) = model.GetItemAnchorPosition(item);
                if (x < 0 || y < 0)
                {
                    continue;
                }

                data.items.Add(new LootItemData
                {
                    slotIndex = model.GetIndex(x, y),
                    itemId = item.Data.itemID,
                    itemCount = item.currentStackCount,
                    itemRotation = (int)item.currentRotation,
                    itemDurability = 0f
                });
            }

            return data;
        }

        public static void ApplyToModel(LootSaveData data, GridInventoryModel model)
        {
            if (data == null || model == null)
            {
                return;
            }

            model.Clear();
            if (data.items == null)
            {
                return;
            }

            foreach (LootItemData itemData in data.items)
            {
                ItemData itemDefinition = ItemDataRegistry.Find(itemData.itemId);
                if (itemDefinition == null)
                {
                    Debug.LogWarning($"[LootSystem] ItemData not found for '{itemData.itemId}'.");
                    continue;
                }

                ItemInstance item = new ItemInstance(itemDefinition, Mathf.Max(1, itemData.itemCount));
                item.currentRotation = (ItemRotation)Mathf.Clamp(itemData.itemRotation, 0, 3);
                (int x, int y) = model.GetCoordinates(itemData.slotIndex);
                if (!model.PlaceItem(item, x, y))
                {
                    Debug.LogWarning($"[LootSystem] Failed to place item '{itemData.itemId}' in loot '{data.lootId}' slot {itemData.slotIndex}.");
                }
            }
        }
    }
}
