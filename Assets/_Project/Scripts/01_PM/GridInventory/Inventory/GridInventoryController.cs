using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

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
            view.OnDragUpdate += HandleDragUpdate;
            view.OnDragEndEvent += HandleDragEnd;

            // 정적 이벤트를 활용해 객체 생성(순서) 여부와 무관하게 즉시 구독
            QuickslotUIController.OnItemDroppedGlobal -= HandleQuickslotItemDropped;
            QuickslotUIController.OnItemDroppedGlobal += HandleQuickslotItemDropped;
            
            QuickslotUIController.OnItemDragUpdateGlobal -= HandleQuickslotItemDragUpdate;
            QuickslotUIController.OnItemDragUpdateGlobal += HandleQuickslotItemDragUpdate;
            
            QuickslotUIController.OnItemDragEndGlobal -= HandleDragEnd;
            QuickslotUIController.OnItemDragEndGlobal += HandleDragEnd;

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
                    
                    // 겹쳤을 때 스택 합치기 로직
                    if (baseTargetItem != null && baseTargetItem.Data == item.Data && item.Data.maxStackSize > 1) {
                        int total = item.currentStackCount + baseTargetItem.currentStackCount;
                        if (total <= item.Data.maxStackSize) {
                            baseTargetItem.currentStackCount = total;
                            QuickslotUIController.Instance.RemoveItemFromSlot(sourceQuickslotIndex);
                            model.Items.Invoke();
                            return;
                        } else {
                            baseTargetItem.currentStackCount = item.Data.maxStackSize;
                            item.currentStackCount = total - item.Data.maxStackSize;
                            QuickslotUIController.Instance.RefreshSlotVisual(sourceQuickslotIndex);
                            model.Items.Invoke();
                            return;
                        }
                    }
                    
                    // Simple logic:
                    if (baseTargetItem == null) {
                        if (model.CanPlaceItem(item, targetCoords.x, targetCoords.y)) {
                            QuickslotUIController.Instance.RemoveItemFromSlot(sourceQuickslotIndex);
                            model.PlaceItem(item, targetCoords.x, targetCoords.y);
                        } else {
                            // Revert on fail
                            QuickslotUIController.Instance.RefreshSlotVisual(sourceQuickslotIndex); // For visual consistency
                        }
                    } else {
                        // For swapping, figure out its start pos
                        var baseTargetPos = model.GetItemAnchorPosition(baseTargetItem);
                        model.TryRemove(baseTargetItem); // Try taking out the grid item
                        
                        if (model.CanPlaceItem(item, targetCoords.x, targetCoords.y)) {
                            QuickslotUIController.Instance.RemoveItemFromSlot(sourceQuickslotIndex);
                            model.PlaceItem(item, targetCoords.x, targetCoords.y);
                            
                            // 퀵슬롯에 들어가는 아이템의 회전각은 0도로 초기화
                            baseTargetItem.currentRotation = ItemRotation.Deg0;
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
            
            // 겹쳤을 때 스택 합치기 (동일 아이템이고, 스택 가능할 때)
            if (targetItem != null && targetItem.Data == sourceItem.Data && targetItem.Data.maxStackSize > 1) {
                int total = sourceItem.currentStackCount + targetItem.currentStackCount;
                if (total <= targetItem.Data.maxStackSize) {
                    targetItem.currentStackCount = total;
                    model.TryRemove(sourceItem); // 인벤토리의 소스 아이템 데이터 완벽히 파괴
                    QuickslotUIController.Instance.RefreshSlotVisual(quickslotIndex);
                    model.Items.Invoke();
                    return;
                } else {
                    originalGridItemView.RevertRotation(originalGridItemView.OriginalRotation);
                    targetItem.currentStackCount = targetItem.Data.maxStackSize;
                    sourceItem.currentStackCount = total - targetItem.Data.maxStackSize;
                    QuickslotUIController.Instance.RefreshSlotVisual(quickslotIndex);
                    model.Items.Invoke();
                    return; // 소스 아이템은 스택이 줄어든 채로 인벤토리에 남음
                }
            }
            
            model.TryRemove(sourceItem); // Pull out from grid
            
            // 퀵슬롯에 새로 들어가는 아이템의 회전각은 0도로 초기화
            ItemRotation oldRotation = sourceItem.currentRotation;
            sourceItem.currentRotation = ItemRotation.Deg0;
            
            if (targetItem != null) {
                // The Quickslot item needs to be unequipped/removed from quickslot before we put it in grid
                QuickslotUIController.Instance.RemoveItemFromSlot(quickslotIndex); // remove first
                
                if (model.CanPlaceItem(targetItem, sourcePos.x, sourcePos.y)) {
                    model.PlaceItem(targetItem, sourcePos.x, sourcePos.y);
                } else {
                    // Try auto layout or fallback?
                    if (!model.TryAdd(targetItem)) {
                        // Undo everything if it completely fails to fit
                        sourceItem.currentRotation = oldRotation;
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

        void HandleDragEnd() {
            view.ResetAllSlotColors();
        }

        void HandleQuickslotItemDragUpdate(ItemInstance sourceItem, Vector2 screenPosition) {
            if (GridInventoryView.Instance == null || !GridInventoryView.Instance.isActiveAndEnabled) return;
            
            view.ResetAllSlotColors();
            if (sourceItem == null) return;
            
            GridSlot closestGridSlot = view.GetGridSlotAtPosition(screenPosition);
            if (closestGridSlot == null) return;

            var targetCoords = model.GetCoordinates(closestGridSlot.Index);
            var aNew = new Vector2Int(targetCoords.x, targetCoords.y);
            
            HashSet<ItemInstance> overlappingItems = new HashSet<ItemInstance>();
            var positions = sourceItem.Data.gridShape.GetRotatedPositions(sourceItem.currentRotation);
            bool outOfBounds = false;

            Color defaultOccupiedColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            for (int i = 0; i < Capacity; i++) {
                var item = model.Get(i);
                if (item != null && item != sourceItem) {
                    view.SetSlotColor(i, defaultOccupiedColor);
                }
            }

            foreach (var pos in positions) {
                int checkX = aNew.x + pos.x;
                int checkY = aNew.y + pos.y;
                
                if (checkX < 0 || checkY < 0 || checkX >= width || checkY >= height) {
                    outOfBounds = true;
                    break;
                }
                
                var foundItem = model.Get(checkX, checkY);
                if (foundItem != null) {
                    overlappingItems.Add(foundItem);
                }
            }

            bool isValid = false;

            if (!outOfBounds) {
                if (overlappingItems.Count == 0) {
                    isValid = true;
                } else if (overlappingItems.Count == 1) {
                    var targetItem = overlappingItems.First();
                    if (sourceItem.Data == targetItem.Data && targetItem.Data.maxStackSize > 1) {
                        isValid = true; 
                    } else {
                        // 1:1 스왑
                        // 퀵슬롯이므로 항상 targetItem 1개를 가져올 공간은 있음 (현재 퀵슬롯 빈자리와 무관하게 허용하도록 설계됨)
                        bool canPlaceA = model.CanPlaceItem(sourceItem, aNew.x, aNew.y, targetItem);
                        if (canPlaceA) {
                            isValid = true;
                        }
                    }
                }
            }

            Color highlightColor = isValid ? new Color(0f, 1f, 0f, 0.3f) : new Color(1f, 0f, 0f, 0.3f);

            foreach (var pos in positions) {
                int checkX = aNew.x + pos.x;
                int checkY = aNew.y + pos.y;

                if (checkX >= 0 && checkY >= 0 && checkX < width && checkY < height) {
                    int slotIndex = model.GetIndex(checkX, checkY);
                    view.SetSlotColor(slotIndex, highlightColor);
                }
            }
        }

        void HandleDragUpdate(GridItemView originalGridItemView, Vector2 screenPosition) {
            view.ResetAllSlotColors();

            ItemInstance sourceItem = originalGridItemView?.ItemInst;
            if (sourceItem == null) return;

            GridSlot closestGridSlot = view.GetGridSlotAtPosition(screenPosition);
            if (closestGridSlot == null) return;

            var targetCoords = model.GetCoordinates(closestGridSlot.Index);
            
            var sourcePos = model.GetItemAnchorPosition(sourceItem);
            var aOld = sourcePos;
            var aNew = new Vector2Int(targetCoords.x, targetCoords.y);

            HashSet<ItemInstance> overlappingItems = new HashSet<ItemInstance>();
            var positions = sourceItem.Data.gridShape.GetRotatedPositions(sourceItem.currentRotation);
            bool outOfBounds = false;

            Color defaultOccupiedColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            for (int i = 0; i < Capacity; i++) {
                var item = model.Get(i);
                if (item != null && item != sourceItem) {
                    view.SetSlotColor(i, defaultOccupiedColor);
                }
            }

            foreach (var pos in positions) {
                int checkX = aNew.x + pos.x;
                int checkY = aNew.y + pos.y;
                
                if (checkX < 0 || checkY < 0 || checkX >= width || checkY >= height) {
                    outOfBounds = true;
                    break;
                }
                
                var foundItem = model.Get(checkX, checkY);
                if (foundItem != null && foundItem != sourceItem) {
                    overlappingItems.Add(foundItem);
                }
            }

            bool isValid = false;
            ItemInstance swapTargetItem = null;
            Vector2Int? bExpectedPos = null;

            if (!outOfBounds) {
                if (overlappingItems.Count == 0) {
                    // 빈 공간
                    isValid = true;
                } else if (overlappingItems.Count == 1) {
                    var targetItem = overlappingItems.First();
                    // 스택 확인
                    if (sourceItem.Data == targetItem.Data && targetItem.Data.maxStackSize > 1) {
                        isValid = true; // 스택 가능
                    } else if (aOld.x != -1) { 
                        // 1:1 스왑 확인
                        swapTargetItem = targetItem;
                        var delta = aNew - new Vector2Int(aOld.x, aOld.y);
                        var bOld = model.GetItemAnchorPosition(targetItem);
                        var bNew = new Vector2Int(bOld.x - delta.x, bOld.y - delta.y);
                        bExpectedPos = bNew;
                        
                        bool canPlaceA = model.CanPlaceItem(sourceItem, aNew.x, aNew.y, targetItem);
                        bool canPlaceB = model.CanPlaceItem(targetItem, bNew.x, bNew.y, targetItem, sourceItem);
                        
                        bool overlapEachOther = false;
                        var aPositions = sourceItem.Data.gridShape.GetRotatedPositions(sourceItem.currentRotation);
                        var bPositionsTarget = targetItem.Data.gridShape.GetRotatedPositions(targetItem.currentRotation);
                        foreach (var a in aPositions) {
                            var absA = new Vector2Int(aNew.x + a.x, aNew.y + a.y);
                            foreach (var b in bPositionsTarget) {
                                var absB = new Vector2Int(bNew.x + b.x, bNew.y + b.y);
                                if (absA == absB) {
                                    overlapEachOther = true;
                                    break;
                                }
                            }
                            if (overlapEachOther) break;
                        }
                        
                        if (!overlapEachOther && canPlaceA && canPlaceB) {
                            isValid = true;
                        }
                    }
                }
            }

            if (swapTargetItem != null && bExpectedPos.HasValue) {
                Color whiteHighlight = new Color(1f, 1f, 1f, 0.4f);
                var bPositionsTarget = swapTargetItem.Data.gridShape.GetRotatedPositions(swapTargetItem.currentRotation);
                
                foreach (var bPos in bPositionsTarget) {
                    int bCheckX = bExpectedPos.Value.x + bPos.x;
                    int bCheckY = bExpectedPos.Value.y + bPos.y;
                    
                    if (bCheckX >= 0 && bCheckY >= 0 && bCheckX < width && bCheckY < height) {
                        int slotIndex = model.GetIndex(bCheckX, bCheckY);
                        view.SetSlotColor(slotIndex, whiteHighlight);
                    }
                }
            }

            // 하이라이트 색상 결정
            Color highlightColor = isValid ? new Color(0f, 1f, 0f, 0.3f) : new Color(1f, 0f, 0f, 0.3f);

            // 해당 슬롯들에 색상 적용
            foreach (var pos in positions) {
                int checkX = aNew.x + pos.x;
                int checkY = aNew.y + pos.y;

                if (checkX >= 0 && checkY >= 0 && checkX < width && checkY < height) {
                    int slotIndex = model.GetIndex(checkX, checkY);
                    view.SetSlotColor(slotIndex, highlightColor);
                }
            }
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
            var aOld = sourcePos;
            var aNew = new Vector2Int(targetCoords.x, targetCoords.y);
            
            // 1. Remove sourceItem from grid momentarily for collision check
            model.TryRemove(sourceItem);

            // 2. Discover all overlapping items at the new position
            HashSet<ItemInstance> overlappingItems = new HashSet<ItemInstance>();
            var positions = sourceItem.Data.gridShape.GetRotatedPositions(sourceItem.currentRotation);
            bool outOfBounds = false;
            
            foreach (var pos in positions) {
                int checkX = aNew.x + pos.x;
                int checkY = aNew.y + pos.y;
                
                if (checkX < 0 || checkY < 0 || checkX >= width || checkY >= height) {
                    outOfBounds = true;
                    break;
                }
                
                var foundItem = model.Get(checkX, checkY);
                if (foundItem != null) {
                    overlappingItems.Add(foundItem);
                }
            }

            if (outOfBounds) {
                originalGridItemView.RevertRotation(originalGridItemView.OriginalRotation);
                model.PlaceItem(sourceItem, aOld.x, aOld.y);
                return;
            }

            if (overlappingItems.Count == 0) {
                // Free space!
                model.PlaceItem(sourceItem, aNew.x, aNew.y);
                return;
            }

            if (overlappingItems.Count == 1) {
                var targetItem = overlappingItems.First();
                
                // Stack Combine logic
                if (sourceItem.Data == targetItem.Data && targetItem.Data.maxStackSize > 1) {
                    int total = sourceItem.currentStackCount + targetItem.currentStackCount;
                    if (total <= targetItem.Data.maxStackSize) {
                        targetItem.currentStackCount = total;
                        model.Items.Invoke();
                    } else {
                        originalGridItemView.RevertRotation(originalGridItemView.OriginalRotation); 
                        targetItem.currentStackCount = targetItem.Data.maxStackSize;
                        sourceItem.currentStackCount = total - targetItem.Data.maxStackSize;
                        model.PlaceItem(sourceItem, aOld.x, aOld.y); // Return remaining to original
                    }
                    return;
                }
                
                // 1:1 Swap Logic
                var delta = aNew - new Vector2Int(aOld.x, aOld.y);
                var bOld = model.GetItemAnchorPosition(targetItem);
                var bNew = new Vector2Int(bOld.x - delta.x, bOld.y - delta.y);
                
                // Safely check if we can place BOTH items without removing targetItem yet!
                // aNew might overlap with targetItem's old position, so we ignore targetItem.
                // bNew might overlap with targetItem's old position, so we ignore targetItem.
                // sourceItem is already removed from grid, so it won't block anything.
                bool canPlaceA = model.CanPlaceItem(sourceItem, aNew.x, aNew.y, targetItem);
                bool canPlaceB = model.CanPlaceItem(targetItem, bNew.x, bNew.y, targetItem);
                
                // Intersect check for swap overlapping each other at their new positions
                bool overlapEachOther = false;
                var aPositions = sourceItem.Data.gridShape.GetRotatedPositions(sourceItem.currentRotation);
                var bPositionsTarget = targetItem.Data.gridShape.GetRotatedPositions(targetItem.currentRotation);
                foreach (var a in aPositions) {
                    var absA = new Vector2Int(aNew.x + a.x, aNew.y + a.y);
                    foreach (var b in bPositionsTarget) {
                        var absB = new Vector2Int(bNew.x + b.x, bNew.y + b.y);
                        if (absA == absB) {
                            overlapEachOther = true;
                            break;
                        }
                    }
                    if (overlapEachOther) break;
                }
                
                if (!overlapEachOther && canPlaceA && canPlaceB) {
                    model.TryRemove(targetItem); // Remove target only if successful
                    model.PlaceItem(sourceItem, aNew.x, aNew.y);
                    model.PlaceItem(targetItem, bNew.x, bNew.y);
                } else {
                    originalGridItemView.RevertRotation(originalGridItemView.OriginalRotation);
                    model.PlaceItem(sourceItem, aOld.x, aOld.y); // Revert source item
                }
                return;
            }

            // More than 1 item overlapping = fail: rollback
            originalGridItemView.RevertRotation(originalGridItemView.OriginalRotation);
            model.PlaceItem(sourceItem, aOld.x, aOld.y);
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
                        itemView.style.opacity = 1f;
                    }
                }
            }

            PruneOrphanItemViews();
        }

        void PruneOrphanItemViews() {
            var trackedViews = new HashSet<GridItemView>(itemViews.Values);
            var orphanViews = view.Container?.Q<VisualElement>("itemsContainer")?.Children()
                .OfType<GridItemView>()
                .Where(itemView => !trackedViews.Contains(itemView))
                .ToList();

            if (orphanViews == null) return;

            foreach (var orphanView in orphanViews) {
                view.RemoveItem(orphanView);
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

