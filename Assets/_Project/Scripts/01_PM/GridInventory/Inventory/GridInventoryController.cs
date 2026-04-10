using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Systems.GridInventory {
    public class GridInventoryController {
        readonly GridInventoryView view;
        readonly GridInventoryModel model;
        
        public GridInventoryModel Model => model;

        readonly int width;
        readonly int height;
        int Capacity => width * height;

        public GridInventoryController(GridInventoryView view, GridInventoryModel model, int width, int height) {
            Debug.Assert(view != null, "View is null");
            Debug.Assert(model != null, "Model is null");
            Debug.Assert(width > 0 && height > 0, "Dimensions are less than 1");
            this.view = view;
            this.model = model;
            this.width = width;
            this.height = height;

            view.StartCoroutine(Initialize());
        }
        
        IEnumerator Initialize() {
            yield return view.Initialize(Capacity, width);

            view.OnDrop += HandleDrop;
            view.OnDropToQuickslot += HandleDropToQuickslot;
            view.OnSaveClicked += HandleSave;
            view.OnLoadClicked += HandleLoad;
            model.OnModelChanged += HandleModelChanged;

            if (QuickslotUIController.Instance != null) {
                QuickslotUIController.Instance.OnItemDropped += HandleQuickslotItemDropped;
            }

            RefreshView();
        }

        void HandleQuickslotItemDropped(ItemInstance item, int sourceQuickslotIndex, Vector2 screenPosition) {
            if (GridInventoryView.Instance != null && GridInventoryView.Instance.isActiveAndEnabled) {
                var slot = GridInventoryView.Instance.GetGridSlotAtPosition(screenPosition);
                if (slot != null) {
                    var targetCoords = model.GetCoordinates(slot.Index);
                    // Check if the place target has an item
                    var baseTargetItem = model.Get(targetCoords.x, targetCoords.y);
                    
                    if (baseTargetItem == item) return;
                    
                    // Simple logic:
                    if (baseTargetItem == null) {
                        if (model.CanPlaceItem(item, targetCoords.x, targetCoords.y)) {
                            QuickslotUIController.Instance.RemoveItemFromSlot(sourceQuickslotIndex);
                            model.PlaceItem(item, targetCoords.x, targetCoords.y);
                        }
                    } else {
                        // For swapping, figure out its start pos
                        var baseTargetPos = model.GetItemAnchorPosition(baseTargetItem);
                        model.TryRemove(baseTargetItem); // Try taking out the grid item
                        
                        if (model.CanPlaceItem(item, targetCoords.x, targetCoords.y)) {
                            QuickslotUIController.Instance.RemoveItemFromSlot(sourceQuickslotIndex);
                            model.PlaceItem(item, targetCoords.x, targetCoords.y);
                            
                            // Successful swap, push the grid item into the quickslot
                            QuickslotUIController.Instance.SetItemInSlot(sourceQuickslotIndex, baseTargetItem);
                        } else {
                            // Revert taking out the grid item
                            model.PlaceItem(baseTargetItem, baseTargetPos.x, baseTargetPos.y);
                        }
                    }
                }
            }
        }

        void HandleDropToQuickslot(GridItemView originalGridItemView, int quickslotIndex) {
            ItemInstance sourceItem = originalGridItemView.ItemInst;
            if (sourceItem == null) return;
            
            var sourcePos = model.GetItemAnchorPosition(sourceItem);
            if (sourcePos.x == -1 || sourcePos.y == -1) return; // not in model somehow?
            
            // Check if there's already an item in quickslot
            ItemInstance targetItem = QuickslotUIController.Instance.GetItem(quickslotIndex);
            
            if (targetItem == sourceItem) {
                // If it's literally the same item, just reset view
                originalGridItemView.style.visibility = UnityEngine.UIElements.Visibility.Visible;
                return;
            }
            
            model.TryRemove(sourceItem); // Pull out from grid
            
            if (targetItem != null) {
                // Swap logic: We need to place targetItem at sourcePos
                // The Quickslot item needs to be unequipped/removed from quickslot before we put it in grid
                QuickslotUIController.Instance.RemoveItemFromSlot(quickslotIndex); // remove first
                
                if (model.CanPlaceItem(targetItem, sourcePos.x, sourcePos.y)) {
                    model.PlaceItem(targetItem, sourcePos.x, sourcePos.y);
                } else {
                    // Try auto layout or fallback? Quickslot items are 1x1 or their original size
                    // Since it replaces the big item, it should fit in most cases unless quickslot item is HUGE
                    if (!model.TryAdd(targetItem)) {
                        // Undo everything if it completely fails to fit
                        model.TryRemove(targetItem);
                        model.PlaceItem(sourceItem, sourcePos.x, sourcePos.y);
                        QuickslotUIController.Instance.SetItemInSlot(quickslotIndex, targetItem);
                        return; // swap failed
                    }
                }
            }
            
            // Success, place sourceItem into quickslot
            QuickslotUIController.Instance.SetItemInSlot(quickslotIndex, sourceItem);
            
            // We trigger model update just in case
            model.Items.Invoke();
        }

        void HandleSave() {
            GridInventorySaveSystem.SaveInventory(model);
        }

        void HandleLoad() {
            GridInventorySaveSystem.LoadInventory(model);
            RefreshView();
        }

        readonly Dictionary<ItemInstance, GridItemView> itemViews = new Dictionary<ItemInstance, GridItemView>();

        void HandleDrop(GridItemView originalGridItemView, GridSlot closestGridSlot) {
            ItemInstance sourceItem = originalGridItemView.ItemInst;

            if (sourceItem == null) return;

            var sourcePos = model.GetItemAnchorPosition(sourceItem);
            
            if (sourcePos.x == -1 || sourcePos.y == -1) return;
            
            var targetCoords = model.GetCoordinates(closestGridSlot.Index);

            var targetItem = model.Get(targetCoords.x, targetCoords.y);

            // 동일한 아이템인 경우 무시 (자기 자신의 다른 슬롯에 떨군 경우 Swap 내부에서 처리하거나 이쪽에서 거름)
            if(targetItem == sourceItem) {
                targetItem = null;
            }

            if (targetItem == null) {
                // Remove first
                model.TryRemove(sourceItem);
                
                if (model.CanPlaceItem(sourceItem, targetCoords.x, targetCoords.y)) {
                    model.PlaceItem(sourceItem, targetCoords.x, targetCoords.y);
                } else {
                    originalGridItemView.RevertRotation(originalGridItemView.OriginalRotation); // Undo rotation if failed
                    model.PlaceItem(sourceItem, sourcePos.x, sourcePos.y); // Undo 
                }
                return;
            }

            if (sourceItem.Data == targetItem.Data && targetItem.Data.maxStackSize > 1) {
                // Stack Combine logic
                int total = sourceItem.currentStackCount + targetItem.currentStackCount;
                if (total <= targetItem.Data.maxStackSize) {
                    targetItem.currentStackCount = total;
                    model.TryRemove(sourceItem);
                    model.Items.Invoke();
                } else {
                    originalGridItemView.RevertRotation(originalGridItemView.OriginalRotation); 
                    // 부분 합치기 이후 남은 수량을 소스 아이템에 보존
                    targetItem.currentStackCount = targetItem.Data.maxStackSize;
                    sourceItem.currentStackCount = total - targetItem.Data.maxStackSize;
                    model.Items.Invoke();
                }
            } else {
                // Swap logic implementation needs to calculate correctly with shapes
                var targetPos = model.GetItemAnchorPosition(targetItem);
                model.TryRemove(sourceItem);
                model.TryRemove(targetItem);
                
                bool placedFirst = false;
                bool swapSuccess = false;

                if (model.CanPlaceItem(sourceItem, targetPos.x, targetPos.y)) {
                    model.PlaceItem(sourceItem, targetPos.x, targetPos.y);
                    placedFirst = true;

                    if (model.CanPlaceItem(targetItem, sourcePos.x, sourcePos.y)) {
                        model.PlaceItem(targetItem, sourcePos.x, sourcePos.y);
                        swapSuccess = true;
                    }
                }

                if (!swapSuccess) {
                    if (placedFirst) {
                        model.TryRemove(sourceItem);
                    }
                    originalGridItemView.RevertRotation(originalGridItemView.OriginalRotation); // Undo rotation on fail
                    model.PlaceItem(sourceItem, sourcePos.x, sourcePos.y);
                    model.PlaceItem(targetItem, targetPos.x, targetPos.y);
                }
            }
        }

        void HandleModelChanged(IList<ItemInstance> items) => RefreshView();

        void RefreshView() {
            var currentItemsInModel = new HashSet<ItemInstance>();

            for (int i = 0; i < Capacity; i++) {
                var item = model.Get(i);
                if (item != null) {
                    currentItemsInModel.Add(item);
                }
            }

            var toRemove = new List<ItemInstance>();
            foreach (var kvp in itemViews) {
                if (!currentItemsInModel.Contains(kvp.Key)) {
                    view.RemoveItem(kvp.Value);
                    toRemove.Add(kvp.Key);
                }
            }
            foreach (var r in toRemove) {
                itemViews.Remove(r);
            }

            var processedItems = new HashSet<ItemInstance>();
            for (int i = 0; i < Capacity; i++) {
                var item = model.Get(i);
                if (item == null) continue;

                if (!processedItems.Contains(item)) {
                    processedItems.Add(item);

                    var anchorPos = model.GetItemAnchorPosition(item);
                    int anchorIndex = model.GetIndex(anchorPos.x, anchorPos.y);

                    if (!itemViews.TryGetValue(item, out var itemView)) {
                        itemView = new GridItemView(item);
                        itemViews[item] = itemView;
                        view.BindItem(itemView, anchorIndex);
                    } else {
                        itemView.SetQuantity(item.currentStackCount);
                        view.UpdateItemPosition(itemView, anchorIndex);
                        itemView.style.visibility = UnityEngine.UIElements.Visibility.Visible;
                    }
                }
            }
        }

        #region Builder

        public class Builder {
            GridInventoryView view;
            int width = 10;
            int height = 5;
            IEnumerable<GridInventory.StartingItem> startingItems;

            public Builder(GridInventoryView view) {
                this.view = view;
            }

            public Builder WithStartingItems(IEnumerable<GridInventory.StartingItem> items) {
                this.startingItems = items;
                return this;
            }

            public Builder WithDimensions(int width, int height) {
                this.width = width;
                this.height = height;
                return this;
            }

            public GridInventoryController Build() {
                GridInventoryModel model = new GridInventoryModel(width, height);
                // Add initial items if provided
                if (startingItems != null) {
                    foreach (var item in startingItems) {
                        int amount = item.quantity;
                        while (amount > 0) {
                            int addAmount = Math.Min(amount, item.itemData.maxStackSize);
                            var newItem = new ItemInstance(item.itemData, addAmount);
                            model.TryAdd(newItem);
                            amount -= addAmount;
                        }
                    }
                }
                return new GridInventoryController(view, model, width, height);
            }
        }

        #endregion Builder
    }
}

