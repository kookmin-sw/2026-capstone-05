using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Systems.GridInventory {
    public class GridInventoryController {
        readonly GridStorageView view;
        readonly GridInventoryModel model;
        
        public GridInventoryModel Model => model;

        readonly int width;
        readonly int height;
        int Capacity => width * height;
        readonly HashSet<ItemInstance> overlappingItems = new HashSet<ItemInstance>();
        readonly HashSet<ItemInstance> currentItemsInModel = new HashSet<ItemInstance>();
        readonly HashSet<ItemInstance> processedItems = new HashSet<ItemInstance>();
        readonly List<ItemInstance> itemsToRemove = new List<ItemInstance>();

        public GridInventoryController(GridStorageView view, GridInventoryModel model, int width, int height) {
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
            if (view is GridInventoryView invView) {
                invView.OnSaveClicked += HandleSave;
                invView.OnLoadClicked += HandleLoad;
            }
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

            QuickslotUIController.OnQuickslotSplitRequested -= HandleQuickslotSplitRequestedToGrid;
            QuickslotUIController.OnQuickslotSplitRequested += HandleQuickslotSplitRequestedToGrid;

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
            
            overlappingItems.Clear();
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
                    var targetItem = GetSingleItem(overlappingItems);
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

            overlappingItems.Clear();
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
                    var targetItem = GetSingleItem(overlappingItems);
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
            if (PlayerNetworkSetup.IsOfflineTestMode) {
                // 싱글플레이어 오프라인 모드에서는 로컬 저장
                GridInventorySaveSystem.SaveInventory(model);
                if (Systems.Loot.LootNetworkSync.Instance != null) {
                    Systems.Loot.LootNetworkSync.Instance.SaveAllLoots();
                }
            } else if (BackendPlayerNetworkSync.LocalInstance != null) {
                // 멀티플레이어 모드에서는 호스트가 전체 저장
                BackendPlayerNetworkSync.LocalInstance.HostInitiateSaveAll();
            }
        }

        void HandleLoad() {
            if (PlayerNetworkSetup.IsOfflineTestMode) {
                // 싱글플레이어 오프라인 모드에서는 로컬 불러오기
                GridInventorySaveSystem.LoadInventory(model);
            } else if (BackendPlayerNetworkSync.LocalInstance != null) {
                // 멀티플레이어 모드에서는 호스트가 전체 불러오기
                BackendPlayerNetworkSync.LocalInstance.HostInitiateLoadAll();
            }
            RefreshView();
        }

        readonly Dictionary<ItemInstance, GridItemView> itemViews = new Dictionary<ItemInstance, GridItemView>();

        public Action<ItemInstance, int, int, int, int, int> OnRequestInternalMove; // item, oldX, oldY, newX, newY, newRotation

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
            overlappingItems.Clear();
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
                if (OnRequestInternalMove != null) {
                    OnRequestInternalMove.Invoke(sourceItem, aOld.x, aOld.y, aNew.x, aNew.y, (int)sourceItem.currentRotation);
                }
                return;
            }

            if (overlappingItems.Count == 1) {
                var targetItem = GetSingleItem(overlappingItems);
                
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
                    if (OnRequestInternalMove != null) {
                        OnRequestInternalMove.Invoke(sourceItem, aOld.x, aOld.y, aNew.x, aNew.y, (int)sourceItem.currentRotation);
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
                    if (OnRequestInternalMove != null) {
                        OnRequestInternalMove.Invoke(sourceItem, aOld.x, aOld.y, aNew.x, aNew.y, (int)sourceItem.currentRotation);
                    }
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

        /// <summary>Inventory grid drag split: drops onto a quickslot (empty or stack merge).</summary>
        public bool TryConsumeGridSplitToQuickslot(GridItemView itemView, Vector2 screenPos) {
            QuantityPopupView.EnsureExists();
            if (!(view is GridInventoryView)) return false;

            var qsCtl = QuickslotUIController.Instance;
            if (qsCtl == null) return false;

            int qi = qsCtl.GetSlotIndexAtPosition(screenPos);
            if (qi < 0) return false;

            ItemInstance sourceItem = itemView?.ItemInst;
            if (sourceItem == null || sourceItem.currentStackCount <= 1) return false;

            var anchor = model.GetItemAnchorPosition(sourceItem);
            if (anchor.x < 0 || anchor.y < 0) return false;

            ItemInstance qsItem = qsCtl.GetItem(qi);
            int maxQuantity;
            if (qsItem == null) {
                maxQuantity = sourceItem.currentStackCount - 1;
            } else if (qsItem.Data == sourceItem.Data && sourceItem.Data.maxStackSize > 1) {
                int spaceLeft = qsItem.Data.maxStackSize - qsItem.currentStackCount;
                if (spaceLeft <= 0) return false;

                maxQuantity = Mathf.Min(sourceItem.currentStackCount - 1, spaceLeft);
            } else return false;

            if (maxQuantity < 1) return false;

            QuantityPopupView.Show(maxQuantity, qty => ExecuteGridSplitToQuickslot(itemView, qi, qty));
            return true;
        }

        void ExecuteGridSplitToQuickslot(GridItemView itemView, int quickslotIndex, int quantity) {
            ItemInstance sourceItem = itemView?.ItemInst;
            if (sourceItem == null || quantity < 1) return;

            int originalStack = sourceItem.currentStackCount;
            if (quantity >= originalStack) return;

            var anchor = model.GetItemAnchorPosition(sourceItem);
            if (anchor.x < 0 || anchor.y < 0) return;

            var qsCtl = QuickslotUIController.Instance;
            if (qsCtl == null) return;

            ItemInstance qsTarget = qsCtl.GetItem(quickslotIndex);

            model.TryRemove(sourceItem);

            bool mergeIntoQs = qsTarget != null && qsTarget.Data == sourceItem.Data && sourceItem.Data.maxStackSize > 1;
            if (mergeIntoQs) {
                int spaceLeftAfterRemove = qsTarget.Data.maxStackSize - qsTarget.currentStackCount;
                if (quantity > spaceLeftAfterRemove) {
                    sourceItem.currentStackCount = originalStack;
                    model.PlaceItem(sourceItem, anchor.x, anchor.y);
                    return;
                }

                qsTarget.currentStackCount += quantity;
                sourceItem.currentStackCount = originalStack - quantity;
                qsCtl.RefreshSlotVisual(quickslotIndex);
            } else if (qsTarget == null) {
                var fragment = new ItemInstance(sourceItem.Data, quantity);
                fragment.currentRotation = ItemRotation.Deg0;
                sourceItem.currentStackCount = originalStack - quantity;
                qsCtl.SetItemInSlot(quickslotIndex, fragment);
                qsCtl.RefreshSlotVisual(quickslotIndex);
            } else {
                sourceItem.currentStackCount = originalStack;
                model.PlaceItem(sourceItem, anchor.x, anchor.y);
                return;
            }

            model.PlaceItem(sourceItem, anchor.x, anchor.y);
            model.Items.Invoke();
        }

        void HandleQuickslotSplitRequestedToGrid(ItemInstance item, int qsIndex, Vector2 screenPos) {
            QuantityPopupView.EnsureExists();
            if (!(view is GridInventoryView)) return;
            if (GridInventoryView.Instance == null || !GridInventoryView.Instance.isActiveAndEnabled) return;

            var qsCtl = QuickslotUIController.Instance;
            if (qsCtl == null || qsCtl.GetItem(qsIndex) != item) return;

            GridSlot closest = GridInventoryView.Instance.GetGridSlotAtPosition(screenPos);
            if (closest == null) return;

            if (!TryEvaluateQuickslotStackSplitOntoGrid(item, closest, out int maxQuantity) || maxQuantity < 1) return;

            QuantityPopupView.Show(maxQuantity,
                qty => ExecuteQuickslotSplitToGridSlot(item, qsIndex, closest, qty));
        }

        bool TryEvaluateQuickslotStackSplitOntoGrid(ItemInstance qsSource, GridSlot closestGridSlot, out int maxQuantity) {
            maxQuantity = 0;

            var targetCoords = model.GetCoordinates(closestGridSlot.Index);
            var aNew = new Vector2Int(targetCoords.x, targetCoords.y);

            overlappingItems.Clear();
            var positions = qsSource.Data.gridShape.GetRotatedPositions(qsSource.currentRotation);
            bool outOfBounds = false;

            foreach (var pos in positions) {
                int checkX = aNew.x + pos.x;
                int checkY = aNew.y + pos.y;

                if (checkX < 0 || checkY < 0 || checkX >= width || checkY >= height) {
                    outOfBounds = true;
                    break;
                }

                var foundItem = model.Get(checkX, checkY);
                if (foundItem != null) overlappingItems.Add(foundItem);
            }

            if (outOfBounds || overlappingItems.Count > 1) return false;

            if (overlappingItems.Count == 0) {
                var probe = new ItemInstance(qsSource.Data, 1);
                probe.currentRotation = qsSource.currentRotation;
                if (!model.CanPlaceItem(probe, aNew.x, aNew.y)) return false;

                maxQuantity = qsSource.currentStackCount - 1;
                return maxQuantity >= 1;
            }

            var mergeTarget = GetSingleItem(overlappingItems);
            if (qsSource.Data != mergeTarget.Data || mergeTarget.Data.maxStackSize <= 1) return false;

            int space = mergeTarget.Data.maxStackSize - mergeTarget.currentStackCount;
            if (space <= 0) return false;

            maxQuantity = Mathf.Min(qsSource.currentStackCount - 1, space);
            return maxQuantity >= 1;
        }

        void ExecuteQuickslotSplitToGridSlot(ItemInstance qsSource, int qsIndex, GridSlot closestGridSlot,
            int quantity) {
            var qsCtl = QuickslotUIController.Instance;
            if (qsCtl == null || qsCtl.GetItem(qsIndex) != qsSource || quantity < 1) return;

            int qsOriginalStack = qsSource.currentStackCount;
            if (quantity >= qsOriginalStack) return;

            if (!TryEvaluateQuickslotStackSplitOntoGrid(qsSource, closestGridSlot, out int maxQty) ||
                quantity > maxQty) return;

            var targetCoords = model.GetCoordinates(closestGridSlot.Index);
            var aNew = new Vector2Int(targetCoords.x, targetCoords.y);

            overlappingItems.Clear();
            var positionsCheck = qsSource.Data.gridShape.GetRotatedPositions(qsSource.currentRotation);
            foreach (var pos in positionsCheck) {
                var foundItem = model.Get(aNew.x + pos.x, aNew.y + pos.y);
                if (foundItem != null) overlappingItems.Add(foundItem);
            }

            if (overlappingItems.Count > 1) return;

            if (overlappingItems.Count == 0) {
                var placed = new ItemInstance(qsSource.Data, quantity);
                placed.currentRotation = qsSource.currentRotation;
                if (!model.CanPlaceItem(placed, aNew.x, aNew.y)) return;

                qsSource.currentStackCount = qsOriginalStack - quantity;
                if (qsSource.currentStackCount <= 0)
                    qsCtl.RemoveItemFromSlot(qsIndex);
                else qsCtl.RefreshSlotVisual(qsIndex);

                model.PlaceItem(placed, aNew.x, aNew.y);
                model.Items.Invoke();
                return;
            }

            var mergeTarget = GetSingleItem(overlappingItems);
            if (qsSource.Data != mergeTarget.Data || mergeTarget.Data.maxStackSize <= 1) return;

            int spaceLeft = mergeTarget.Data.maxStackSize - mergeTarget.currentStackCount;
            int moveQty = Mathf.Min(quantity, spaceLeft);
            if (moveQty < 1) return;

            mergeTarget.currentStackCount += moveQty;
            qsSource.currentStackCount = qsOriginalStack - moveQty;

            if (qsSource.currentStackCount <= 0)
                qsCtl.RemoveItemFromSlot(qsIndex);
            else qsCtl.RefreshSlotVisual(qsIndex);

            model.Items.Invoke();
        }

        /// <summary>Right-click split: empty cell or compatible stack merge only (validated before popup).</summary>
        public void TrySplitAfterRightClick(GridItemView itemView, Vector2 screenPos, Action onApplied = null) {
            ItemInstance sourceItem = itemView?.ItemInst;
            if (sourceItem == null || sourceItem.currentStackCount <= 1) return;

            var sourcePos = model.GetItemAnchorPosition(sourceItem);
            if (sourcePos.x == -1 || sourcePos.y == -1) return;

            GridSlot closestGridSlot = view.GetGridSlotAtPosition(screenPos);
            if (closestGridSlot == null) return;

            var targetCoords = model.GetCoordinates(closestGridSlot.Index);
            var aOld = sourcePos;
            var aNew = new Vector2Int(targetCoords.x, targetCoords.y);

            if (aNew.x == aOld.x && aNew.y == aOld.y) return;

            model.TryRemove(sourceItem);

            overlappingItems.Clear();
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
                if (foundItem != null) overlappingItems.Add(foundItem);
            }

            if (outOfBounds || overlappingItems.Count > 1) {
                model.PlaceItem(sourceItem, aOld.x, aOld.y);
                return;
            }

            int maxQuantity;
            if (overlappingItems.Count == 0) {
                var probe = new ItemInstance(sourceItem.Data, 1);
                if (!model.CanPlaceItem(probe, aNew.x, aNew.y)) {
                    model.PlaceItem(sourceItem, aOld.x, aOld.y);
                    return;
                }

                maxQuantity = sourceItem.currentStackCount - 1;
            } else {
                var targetItem = GetSingleItem(overlappingItems);
                if (sourceItem.Data != targetItem.Data || targetItem.Data.maxStackSize <= 1) {
                    model.PlaceItem(sourceItem, aOld.x, aOld.y);
                    return;
                }

                int space = targetItem.Data.maxStackSize - targetItem.currentStackCount;
                if (space <= 0) {
                    model.PlaceItem(sourceItem, aOld.x, aOld.y);
                    return;
                }

                maxQuantity = Mathf.Min(sourceItem.currentStackCount - 1, space);
            }

            if (maxQuantity < 1) {
                model.PlaceItem(sourceItem, aOld.x, aOld.y);
                return;
            }

            model.PlaceItem(sourceItem, aOld.x, aOld.y);

            QuantityPopupView.Show(maxQuantity, qty =>
                ExecuteSplitMove(itemView, closestGridSlot, qty, onApplied));
        }

        void ExecuteSplitMove(GridItemView originalGridItemView, GridSlot closestGridSlot, int quantity,
            Action onApplied = null) {
            ItemInstance sourceItem = originalGridItemView.ItemInst;
            if (sourceItem == null || quantity < 1) return;

            int originalStack = sourceItem.currentStackCount;

            var sourcePos = model.GetItemAnchorPosition(sourceItem);
            if (sourcePos.x == -1 || sourcePos.y == -1) return;

            var targetCoords = model.GetCoordinates(closestGridSlot.Index);
            var aOld = sourcePos;
            var aNew = new Vector2Int(targetCoords.x, targetCoords.y);

            model.TryRemove(sourceItem);

            overlappingItems.Clear();
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
                if (foundItem != null) overlappingItems.Add(foundItem);
            }

            if (outOfBounds || overlappingItems.Count > 1) {
                sourceItem.currentStackCount = originalStack;
                model.PlaceItem(sourceItem, aOld.x, aOld.y);
                originalGridItemView.RevertRotation(originalGridItemView.OriginalRotation);
                return;
            }

            if (overlappingItems.Count == 0) {
                var placed = new ItemInstance(sourceItem.Data, quantity);
                if (!model.CanPlaceItem(placed, aNew.x, aNew.y)) {
                    sourceItem.currentStackCount = originalStack;
                    model.PlaceItem(sourceItem, aOld.x, aOld.y);
                    originalGridItemView.RevertRotation(originalGridItemView.OriginalRotation);
                    return;
                }

                sourceItem.currentStackCount = originalStack - quantity;
                model.PlaceItem(sourceItem, aOld.x, aOld.y);
                model.PlaceItem(placed, aNew.x, aNew.y);
                model.Items.Invoke();
                if (OnRequestInternalMove != null) {
                    OnRequestInternalMove.Invoke(placed, aOld.x, aOld.y, aNew.x, aNew.y,
                        (int)placed.currentRotation);
                }

                onApplied?.Invoke();
                return;
            }

            var mergeTarget = GetSingleItem(overlappingItems);
            if (sourceItem.Data != mergeTarget.Data || mergeTarget.Data.maxStackSize <= 1) {
                sourceItem.currentStackCount = originalStack;
                model.PlaceItem(sourceItem, aOld.x, aOld.y);
                originalGridItemView.RevertRotation(originalGridItemView.OriginalRotation);
                return;
            }

            int spaceLeft = mergeTarget.Data.maxStackSize - mergeTarget.currentStackCount;
            int moveQty = Mathf.Min(quantity, spaceLeft);
            if (moveQty < 1) {
                sourceItem.currentStackCount = originalStack;
                model.PlaceItem(sourceItem, aOld.x, aOld.y);
                originalGridItemView.RevertRotation(originalGridItemView.OriginalRotation);
                return;
            }

            mergeTarget.currentStackCount += moveQty;
            sourceItem.currentStackCount = originalStack - moveQty;
            model.PlaceItem(sourceItem, aOld.x, aOld.y);
            model.Items.Invoke();

            if (OnRequestInternalMove != null) {
                OnRequestInternalMove.Invoke(sourceItem, aOld.x, aOld.y, aNew.x, aNew.y,
                    (int)sourceItem.currentRotation);
            }

            onApplied?.Invoke();
        }

        void HandleModelChanged(IList<ItemInstance> items) => RefreshView();

        static ItemInstance GetSingleItem(HashSet<ItemInstance> items) {
            foreach (var item in items) {
                return item;
            }

            return null;
        }

        void RefreshView() {
            currentItemsInModel.Clear();

            for (int i = 0; i < Capacity; i++) {
                var item = model.Get(i);
                if (item != null) {
                    currentItemsInModel.Add(item);
                }
            }

            itemsToRemove.Clear();
            foreach (var kvp in itemViews) {
                if (!currentItemsInModel.Contains(kvp.Key)) {
                    if (kvp.Value.IsDraggingThis) {
                        view.ForceResetDrag();
                    }
                    view.RemoveItem(kvp.Value);
                    itemsToRemove.Add(kvp.Key);
                }
            }
            foreach (var r in itemsToRemove) {
                itemViews.Remove(r);
            }

            processedItems.Clear();
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
                        
                        if (!itemView.IsDraggingThis) {
                            itemView.style.visibility = UnityEngine.UIElements.Visibility.Visible;
                        }
                    }
                }
            }
        }

        #region Builder

        public class Builder {
            GridStorageView view;
            int width = 8;
            int height = 8;
            IEnumerable<GridInventory.StartingItem> startingItems;
            GridInventoryModel existingModel;

            public Builder(GridStorageView view) {
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
            
            public Builder WithExistingModel(GridInventoryModel model) {
                this.existingModel = model;
                return this;
            }

            public GridInventoryController Build() {
                GridInventoryModel model = existingModel ?? new GridInventoryModel(width, height);
                // Add initial items if provided and we created a new model
                if (existingModel == null && startingItems != null) {
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

