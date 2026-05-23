using UnityEngine;
using Systems.Loot;
using System.Collections.Generic;

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
        private const float GroundDropScatterRadius = 0.5f;
        private const int QuickslotCount = 4;

        public static void ProcessDrop(ItemInstance item, DragSource source, int sourceIndex, Vector2 screenPos, GridInventoryModel sourceModel = null)
        {
            if (item == null) return;

            // 1. Check Quickslot
            if (QuickslotUIController.Instance != null)
            {
                int targetQuickslot = QuickslotUIController.Instance.GetSlotIndexAtPosition(screenPos);
                if (targetQuickslot >= 0)
                {
                    if (source == DragSource.Loot && LootController.Instance != null && LootController.Instance.IsOpen)
                    {
                        LootController.Instance.ReceiveQuickslotDrop(item, targetQuickslot);
                        return;
                    }

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

                var playerSlot = LootController.Instance.GetPlayerGridSlotAtPosition(screenPos);
                if (playerSlot != null)
                {
                    LootController.Instance.ReceivePlayerDrop(item, source, sourceIndex, playerSlot, sourceModel);
                    return;
                }

            }

            // 3. Check Inventory
            if (GridInventoryView.Instance != null && GridInventoryView.Instance.isActiveAndEnabled && GridInventoryView.Instance.IsOpen)
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

        public static void DropAllPlayerItemsAt(Vector3 originPosition)
        {
            var droppedItems = new HashSet<ItemInstance>();

            DropQuickslotItems(originPosition, droppedItems);
            DropInventoryGridItems(originPosition, droppedItems);
        }

        public static void ClearAllPlayerItemsWithoutDrop()
        {
            ClearQuickslotItemsWithoutDrop();
            ClearInventoryGridItemsWithoutDrop();
        }

        public static bool TryQuickMoveInventoryItemToQuickslot(GridItemView itemView)
        {
            if (!IsInventoryQuickMoveModeOpen() || itemView == null || itemView.ItemInst == null)
                return false;

            GridInventoryModel inventoryModel = GridInventory.Instance?.Controller?.Model;
            QuickslotUIController quickslot = QuickslotUIController.Instance;
            ItemInstance item = itemView.ItemInst;
            if (inventoryModel == null || quickslot == null || !IsStoredInModel(inventoryModel, item))
                return false;

            if (TryStackIntoQuickslot(item, inventoryModel, quickslot))
                return true;

            int targetIndex = FindEmptyQuickslotIndex(quickslot);
            if (targetIndex < 0)
                return false;

            var sourcePos = inventoryModel.GetItemAnchorPosition(item);
            if (sourcePos.x < 0 || sourcePos.y < 0 || !inventoryModel.TryRemove(item))
                return false;

            item.currentRotation = ItemRotation.Deg0;
            quickslot.SetItemInSlot(targetIndex, item);
            return true;
        }

        public static bool TryQuickMoveQuickslotItemToInventory(int quickslotIndex)
        {
            if (!IsInventoryQuickMoveModeOpen())
                return false;

            QuickslotUIController quickslot = QuickslotUIController.Instance;
            GridInventoryModel inventoryModel = GridInventory.Instance?.Controller?.Model;
            ItemInstance item = quickslot != null ? quickslot.GetItem(quickslotIndex) : null;
            if (quickslot == null || inventoryModel == null || item == null || item.Data == null)
                return false;

            if (!TryFindFirstFreePlacement(inventoryModel, item, out Vector2Int targetPos))
                return false;

            quickslot.RemoveItemFromSlot(quickslotIndex);
            if (inventoryModel.PlaceItem(item, targetPos.x, targetPos.y))
                return true;

            quickslot.SetItemInSlot(quickslotIndex, item);
            return false;
        }

        public static void DropItemOnGround(ItemInstance item, Vector3 originPosition)
        {
            if (item == null || item.Data == null)
                return;

            Vector2 randomOffset = UnityEngine.Random.insideUnitCircle * GroundDropScatterRadius;
            Vector3 dropPosition = originPosition + new Vector3(randomOffset.x, 0, randomOffset.y);

            SpawnDroppedItem(item, dropPosition);
        }

        private static void DropOnGround(ItemInstance item, DragSource source, int sourceIndex, GridInventoryModel sourceModel)
        {
            if (!LocalPlayerReferenceResolver.TryGetLocalPlayer(out PlayerController player))
            {
                // Revert if player not found
                RevertDrop(item, source, sourceIndex, sourceModel);
                return;
            }

            if (item.Data == null || item.Data.pickupPrefab == null)
            {
                RevertDrop(item, source, sourceIndex, sourceModel);
                return;
            }

            // Remove from source
            if (source == DragSource.Quickslot)
            {
                QuickslotUIController.Instance.RemoveItemFromSlot(sourceIndex);
            }
            else if (sourceModel != null)
            {
                sourceModel.TryRemove(item);
                sourceModel.Items.Invoke();

                if (source == DragSource.Loot && LootController.Instance != null)
                {
                    LootController.Instance.NotifyLootModelChangedFromExternalDrop(sourceModel);
                }
            }

            DropItemOnGround(item, player.transform.position);
        }

        private static void DropQuickslotItems(Vector3 originPosition, HashSet<ItemInstance> droppedItems)
        {
            if (QuickslotUIController.Instance == null)
                return;

            for (int i = 0; i < QuickslotCount; i++)
            {
                ItemInstance item = QuickslotUIController.Instance.GetItem(i);
                if (item == null || item.Data == null)
                    continue;

                QuickslotUIController.Instance.RemoveItemFromSlot(i);
                if (droppedItems.Add(item))
                {
                    DropItemOnGround(item, originPosition);
                }
            }
        }

        private static void ClearQuickslotItemsWithoutDrop()
        {
            if (QuickslotUIController.Instance == null)
                return;

            for (int i = 0; i < QuickslotCount; i++)
            {
                QuickslotUIController.Instance.RemoveItemFromSlot(i);
            }
        }

        private static void DropInventoryGridItems(Vector3 originPosition, HashSet<ItemInstance> droppedItems)
        {
            GridInventoryModel model = GridInventory.Instance?.Controller?.Model;
            if (model == null)
                return;

            var items = new List<ItemInstance>();
            var uniqueItems = new HashSet<ItemInstance>();

            for (int i = 0; i < model.Items.Length; i++)
            {
                ItemInstance item = model.Get(i);
                if (item != null && item.Data != null && uniqueItems.Add(item))
                {
                    items.Add(item);
                }
            }

            foreach (ItemInstance item in items)
            {
                model.TryRemove(item);
                if (droppedItems.Add(item))
                {
                    DropItemOnGround(item, originPosition);
                }
            }
        }

        private static void ClearInventoryGridItemsWithoutDrop()
        {
            GridInventoryModel model = GridInventory.Instance?.Controller?.Model;
            if (model == null)
                return;

            model.Clear();
        }

        private static bool IsLootTransferModeOpen()
        {
            var lootController = Systems.Loot.LootController.Instance;
            return lootController != null && lootController.IsOpen && !lootController.IsInventoryOnlyOpen;
        }

        private static bool IsInventoryQuickMoveModeOpen()
        {
            if (IsLootTransferModeOpen())
                return false;

            return GridInventoryView.IsAnyInventoryOpen;
        }

        private static bool IsStoredInModel(GridInventoryModel model, ItemInstance item)
        {
            if (model == null || item == null)
                return false;

            var pos = model.GetItemAnchorPosition(item);
            return pos.x >= 0 && pos.y >= 0;
        }

        private static bool TryStackIntoQuickslot(ItemInstance item, GridInventoryModel inventoryModel, QuickslotUIController quickslot)
        {
            if (item == null || item.Data == null || item.Data.maxStackSize <= 1)
                return false;

            for (int i = 0; i < QuickslotCount; i++)
            {
                ItemInstance target = quickslot.GetItem(i);
                if (target == null || target.Data != item.Data || target.currentStackCount >= target.Data.maxStackSize)
                    continue;

                int moveQuantity = Mathf.Min(item.currentStackCount, target.Data.maxStackSize - target.currentStackCount);
                if (moveQuantity <= 0)
                    continue;

                target.currentStackCount += moveQuantity;
                item.currentStackCount -= moveQuantity;
                quickslot.RefreshSlotVisual(i);

                if (item.currentStackCount <= 0)
                {
                    inventoryModel.TryRemove(item);
                }
                else
                {
                    inventoryModel.Items.Invoke();
                }

                return true;
            }

            return false;
        }

        private static int FindEmptyQuickslotIndex(QuickslotUIController quickslot)
        {
            if (quickslot == null)
                return -1;

            for (int i = 0; i < QuickslotCount; i++)
            {
                if (quickslot.GetItem(i) == null)
                    return i;
            }

            return -1;
        }

        private static bool TryFindFirstFreePlacement(GridInventoryModel model, ItemInstance item, out Vector2Int position)
        {
            position = default;
            if (model == null || item == null)
                return false;

            for (int y = 0; y < model.Height; y++)
            {
                for (int x = 0; x < model.Width; x++)
                {
                    if (model.CanPlaceItem(item, x, y))
                    {
                        position = new Vector2Int(x, y);
                        return true;
                    }
                }
            }

            return false;
        }

        private static void SpawnDroppedItem(ItemInstance item, Vector3 dropPosition)
        {
            if (BackendPlayerNetworkSync.LocalInstance != null && BackendPlayerNetworkSync.LocalInstance.IsNetworkReady)
            {
                BackendPlayerNetworkSync.LocalInstance.RequestDropItem(item.Data.itemID, item.currentStackCount, dropPosition);
            }
            else if (AuthSession.IsOffline)
            {
                if (item.Data.pickupPrefab == null)
                    return;

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

        public static void RevertDrop(ItemInstance item, DragSource source, int sourceIndex, GridInventoryModel sourceModel)
        {
            if (source == DragSource.Quickslot)
            {
                QuickslotUIController.Instance.RefreshSlotVisual(sourceIndex);
            }
            else if (sourceModel != null)
            {
                var draggedView = GridStorageView.CurrentDraggedItemView;
                if (draggedView != null && draggedView.ItemInst == item)
                {
                    draggedView.RevertRotation(draggedView.OriginalRotation);
                    draggedView.style.visibility = UnityEngine.UIElements.Visibility.Visible;
                }
            }
        }
    }
}
