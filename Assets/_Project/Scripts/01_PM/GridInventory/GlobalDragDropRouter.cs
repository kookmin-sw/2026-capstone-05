using UnityEngine;
using Systems.Loot;

namespace Systems.GridInventory
{
    public enum DragSource
    {
        Inventory,
        Loot,
        Quickslot
    }

    public static class GlobalDragDropRouter
    {
        public static void ProcessDrop(ItemInstance item, DragSource source, int sourceIndex, Vector2 screenPos, GridInventoryModel sourceModel = null)
        {
            if (item == null) return;

            // 1. Check Quickslot
            if (QuickslotUIController.Instance != null)
            {
                int targetQuickslot = QuickslotUIController.Instance.GetSlotIndexAtPosition(screenPos);
                if (targetQuickslot >= 0)
                {
                    QuickslotUIController.Instance.ReceiveDrop(item, source, sourceIndex, targetQuickslot, sourceModel);
                    return;
                }
            }

            // 2. Check Loot
            if (LootController.Instance != null && LootController.Instance.IsOpen)
            {
                var lootSlot = LootController.Instance.GetLootGridSlotAtPosition(screenPos);
                if (lootSlot != null)
                {
                    LootController.Instance.ReceiveDrop(item, source, sourceIndex, lootSlot, sourceModel);
                    return;
                }
            }

            // 3. Check Inventory
            if (GridInventoryView.Instance != null && GridInventoryView.Instance.isActiveAndEnabled)
            {
                var invSlot = GridInventoryView.Instance.GetGridSlotAtPosition(screenPos);
                if (invSlot != null)
                {
                    GridInventory.Instance.Controller.ReceiveDrop(item, source, sourceIndex, invSlot, sourceModel);
                    return;
                }
            }

            // 4. Drop on Ground (if not dropped on any UI)
            DropOnGround(item, source, sourceIndex, sourceModel);
        }

        private static void DropOnGround(ItemInstance item, DragSource source, int sourceIndex, GridInventoryModel sourceModel)
        {
            if (!LocalPlayerReferenceResolver.TryGetLocalPlayer(out PlayerController player))
            {
                // Revert if player not found
                RevertDrop(item, source, sourceIndex, sourceModel);
                return;
            }

            Vector3 dropPosition = player.transform.position;

            // Remove from source
            if (source == DragSource.Quickslot)
            {
                QuickslotUIController.Instance.RemoveItemFromSlot(sourceIndex);
            }
            else if (sourceModel != null)
            {
                sourceModel.TryRemove(item);
                sourceModel.Items.Invoke();
            }

            // Spawn item
            if (BackendPlayerNetworkSync.LocalInstance != null && BackendPlayerNetworkSync.LocalInstance.IsNetworkReady)
            {
                BackendPlayerNetworkSync.LocalInstance.RequestDropItem(item.Data.itemID, item.currentStackCount, dropPosition);
            }
            else if (PlayerNetworkSetup.IsOfflineTestMode)
            {
                if (item.Data.pickupPrefab != null)
                {
                    var obj = UnityEngine.Object.Instantiate(item.Data.pickupPrefab, dropPosition, Quaternion.identity);

                    // 바닥 높이 보정 로직
                    Collider col = obj.GetComponentInChildren<Collider>();
                    if (col != null)
                    {
                        float bottomOffset = col.bounds.min.y - obj.transform.position.y;
                        obj.transform.position = new Vector3(dropPosition.x, dropPosition.y - bottomOffset, dropPosition.z);
                    }

                    var pickup = obj.GetComponent<ItemPickup>();
                    if (pickup != null)
                    {
                        pickup.itemInstance = new ItemInstance(item.Data, item.currentStackCount);
                    }
                }
            }
        }

        public static void RevertDrop(ItemInstance item, DragSource source, int sourceIndex, GridInventoryModel sourceModel)
        {
            if (source == DragSource.Quickslot)
            {
                QuickslotUIController.Instance.RefreshSlotVisual(sourceIndex);
            }
            else if (sourceModel != null)
            {
                // We don't need to do anything for grid models because the item was never removed during drag,
                // just hidden. We just need to make sure the view makes it visible again.
                // This is typically handled by the view when drag ends without a successful drop.
            }
        }
    }
}
