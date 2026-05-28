using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Systems.GridInventory {
    public class GridInventoryController {
        static readonly List<GridInventoryController> activeControllers = new List<GridInventoryController>();

        readonly GridStorageView view;
        readonly GridInventoryModel model;
        
        public GridInventoryModel Model => model;

        public int width => model.Width;
        public int height => model.Height;
        int Capacity => width * height;
        readonly HashSet<ItemInstance> overlappingItems = new HashSet<ItemInstance>();
        readonly HashSet<ItemInstance> currentItemsInModel = new HashSet<ItemInstance>();
        readonly HashSet<ItemInstance> processedItems = new HashSet<ItemInstance>();
        readonly List<ItemInstance> itemsToRemove = new List<ItemInstance>();
        bool isDisposed;
        bool eventsSubscribed;

        public GridInventoryController(GridStorageView view, GridInventoryModel model, int initWidth, int initHeight) {
            Debug.Assert(view != null, "View is null");
            Debug.Assert(model != null, "Model is null");
            this.view = view;
            this.model = model;

            if (!activeControllers.Contains(this)) {
                activeControllers.Add(this);
            }

            view.StartCoroutine(Initialize());
        }
        
        IEnumerator Initialize() {
            yield return view.Initialize(Capacity, width);
            if (isDisposed) yield break;

            view.OnRouteDropRequested += HandleRouteDropRequested;
            if (view is GridInventoryView invView) {
                invView.OnSaveClicked += HandleSave;
            }
            model.OnModelChanged += HandleModelChanged;
            view.OnDragUpdate += HandleDragUpdate;
            view.OnDragEndEvent += HandleDragEnd;

            QuickslotUIController.OnItemDragUpdateGlobal += HandleQuickslotItemDragUpdate;
            
            QuickslotUIController.OnItemDragEndGlobal -= HandleDragEnd;
            QuickslotUIController.OnItemDragEndGlobal += HandleDragEnd;

            QuickslotUIController.OnQuickslotSplitRequested -= HandleQuickslotSplitRequestedToGrid;
            QuickslotUIController.OnQuickslotSplitRequested += HandleQuickslotSplitRequestedToGrid;
            eventsSubscribed = true;

            RefreshView();
        }

        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;

            activeControllers.Remove(this);
            view?.ResetAllSlotColors();

            if (!eventsSubscribed) return;

            view.OnRouteDropRequested -= HandleRouteDropRequested;
            if (view is GridInventoryView invView) {
                invView.OnSaveClicked -= HandleSave;
            }
            model.OnModelChanged -= HandleModelChanged;
            view.OnDragUpdate -= HandleDragUpdate;
            view.OnDragEndEvent -= HandleDragEnd;
            QuickslotUIController.OnItemDragUpdateGlobal -= HandleQuickslotItemDragUpdate;
            QuickslotUIController.OnItemDragEndGlobal -= HandleDragEnd;
            QuickslotUIController.OnQuickslotSplitRequested -= HandleQuickslotSplitRequestedToGrid;
            eventsSubscribed = false;
        }

        void HandleQuickslotItemDropped(ItemInstance item, int sourceQuickslotIndex, Vector2 screenPosition) {
            GridSlot slot = null;
            if (GridInventoryView.Instance != null && GridInventoryView.Instance.isActiveAndEnabled) {
                slot = GridInventoryView.Instance.GetGridSlotAtPosition(screenPosition);
            }
            if (slot == null && Systems.Loot.LootController.Instance != null && Systems.Loot.LootController.Instance.IsOpen) {
                slot = Systems.Loot.LootController.Instance.GetPlayerGridSlotAtPosition(screenPosition);
            }

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
                    return;
                }

            // NEW: Check if it was dropped over the Loot UI
            if (Systems.Loot.LootController.Instance != null && Systems.Loot.LootController.Instance.IsOpen) {
                var lootSlot = Systems.Loot.LootController.Instance.GetLootGridSlotAtPosition(screenPosition);
                if (lootSlot != null) {
                    // The LootController will handle placing it in the chest via its own event listener.
                    // We just need to return here so we don't drop it on the ground.
                    return;
                }
                
                // If it's over the Loot UI but not on a valid slot, we still shouldn't drop it on the ground.
                // It will just snap back to the quickslot.
                if (Systems.Loot.LootController.Instance.IsPositionInsideLootUI(screenPosition)) {
                    QuickslotUIController.Instance.RefreshSlotVisual(sourceQuickslotIndex);
                    return;
                }
            }

            // If it reaches here, it means it was dropped outside of quickslots and outside of the grid.
            // Drop it on the ground.
            if (LocalPlayerReferenceResolver.TryGetLocalPlayer(out PlayerController player)) {
                Vector2 randomOffset = UnityEngine.Random.insideUnitCircle * 0.5f;
                Vector3 dropPosition = player.transform.position + new Vector3(randomOffset.x, 0, randomOffset.y);
                
                QuickslotUIController.Instance.RemoveItemFromSlot(sourceQuickslotIndex);

                if (BackendPlayerNetworkSync.LocalInstance != null && BackendPlayerNetworkSync.LocalInstance.IsNetworkReady) {
                    BackendPlayerNetworkSync.LocalInstance.RequestDropItem(item.Data.itemID, item.currentStackCount, dropPosition);
                } else if (AuthSession.IsOffline) {
                    if (item.Data.pickupPrefab != null) {
                        var obj = UnityEngine.Object.Instantiate(item.Data.pickupPrefab, dropPosition, Quaternion.identity);
                        
                        // 바닥 높이 보정 로직
                        Collider col = obj.GetComponentInChildren<Collider>();
                        if (col != null) {
                            float bottomOffset = col.bounds.min.y - obj.transform.position.y;
                            obj.transform.position = new Vector3(dropPosition.x, dropPosition.y - bottomOffset, dropPosition.z);
                        }

                        var pickup = obj.GetComponent<ItemPickup>();
                        if (pickup != null) {
                            pickup.itemInstance = new ItemInstance(item.Data, item.currentStackCount);
                        }
                    }
                }
            }
        }


        void HandleDragEnd() {
            ResetAllGridDropHighlights();
        }

        void HandleRouteDropRequested(ItemInstance item, Vector2 position) {
            GlobalDragDropRouter.ProcessDrop(item, view.SourceType, -1, position, model);
        }

        void HandleQuickslotItemDragUpdate(ItemInstance sourceItem, Vector2 screenPosition) {
            UpdateAllGridDropHighlights(sourceItem, null, DragSource.Quickslot, screenPosition, null);
        }

        void HandleDragUpdate(GridItemView originalGridItemView, Vector2 screenPosition) {
            ItemInstance sourceItem = originalGridItemView?.ItemInst;
            if (sourceItem == null) return;

            UpdateAllGridDropHighlights(sourceItem, model, view.SourceType, screenPosition, this);
            QuickslotUIController.Instance?.UpdateDropHighlight(sourceItem, view.SourceType, -1, model, screenPosition);
            ShowQuickslotSwapReturnHighlight(sourceItem, screenPosition);
        }

        static void RemoveStaleControllers() {
            activeControllers.RemoveAll(controller => controller == null || controller.isDisposed || controller.view == null);
        }

        bool IsHighlightTargetActive => !isDisposed && view != null && view.IsHighlightTargetActive;

        static void ResetAllGridDropHighlights() {
            RemoveStaleControllers();
            foreach (var controller in activeControllers) {
                controller.view.ResetAllSlotColors();
            }

            QuickslotUIController.Instance?.ResetDropHighlights();
        }

        static void UpdateAllGridDropHighlights(ItemInstance sourceItem, GridInventoryModel sourceModel,
            DragSource source, Vector2 screenPosition, GridInventoryController sourceController) {
            RemoveStaleControllers();

            foreach (var controller in activeControllers) {
                controller.view.ResetAllSlotColors();
            }

            var highlightControllers = GetHighlightControllers(sourceController);
            foreach (var controller in highlightControllers) {
                controller.ShowGridDropHighlight(sourceItem, sourceModel, source, screenPosition, sourceController);
            }
        }

        static List<GridInventoryController> GetHighlightControllers(GridInventoryController sourceController) {
            List<GridInventoryController> controllers = new List<GridInventoryController>();
            foreach (var controller in activeControllers) {
                if (controller == null || !controller.IsHighlightTargetActive) {
                    continue;
                }

                int existingIndex = controllers.FindIndex(existing => ReferenceEquals(existing.model, controller.model));
                if (existingIndex < 0) {
                    controllers.Add(controller);
                    continue;
                }

                if (ShouldPreferHighlightController(controllers[existingIndex], controller, sourceController)) {
                    controllers[existingIndex] = controller;
                }
            }

            return controllers;
        }

        static bool ShouldPreferHighlightController(GridInventoryController current, GridInventoryController candidate,
            GridInventoryController sourceController) {
            if (candidate == null) return false;
            if (current == null) return true;
            if (candidate == sourceController) return true;
            if (current == sourceController) return false;

            bool lootOpen = Systems.Loot.LootController.Instance != null && Systems.Loot.LootController.Instance.IsOpen;
            if (lootOpen) {
                bool currentIsOriginalInventory = current.view is GridInventoryView;
                bool candidateIsOriginalInventory = candidate.view is GridInventoryView;
                if (currentIsOriginalInventory != candidateIsOriginalInventory) {
                    return currentIsOriginalInventory && !candidateIsOriginalInventory;
                }
            }

            return false;
        }

        static GridInventoryController FindControllerForModel(GridInventoryModel targetModel, GridInventoryController preferred) {
            if (targetModel == null) return null;
            if (preferred != null && ReferenceEquals(preferred.model, targetModel) && preferred.IsHighlightTargetActive) {
                return preferred;
            }

            RemoveStaleControllers();
            foreach (var controller in GetHighlightControllers(preferred)) {
                if (ReferenceEquals(controller.model, targetModel)) {
                    return controller;
                }
            }

            return null;
        }

        void ShowGridDropHighlight(ItemInstance sourceItem, GridInventoryModel sourceModel, DragSource source,
            Vector2 screenPosition, GridInventoryController sourceController) {
            if (sourceItem == null || sourceItem.Data == null || sourceItem.Data.gridShape == null) return;

            GridSlot closestGridSlot = view.GetGridSlotAtPosition(screenPosition);
            if (closestGridSlot == null) return;

            var targetCoords = model.GetCoordinates(closestGridSlot.Index);
            var aNew = new Vector2Int(targetCoords.x, targetCoords.y);
            var positions = sourceItem.Data.gridShape.GetRotatedPositions(sourceItem.currentRotation);

            if (IsSelfDropOnOriginalShape(sourceItem, sourceModel, model, aNew))
            {
                return;
            }

            Color defaultOccupiedColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            for (int i = 0; i < Capacity; i++) {
                var item = model.Get(i);
                if (item != null && item != sourceItem && HasRenderedItemView(item, false)) {
                    view.SetSlotColor(i, defaultOccupiedColor);
                }
            }

            overlappingItems.Clear();
            bool outOfBounds = false;

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
            Vector2Int? swapTargetNewPos = null;

            if (!outOfBounds) {
                if (overlappingItems.Count == 0) {
                    isValid = model.CanPlaceItem(sourceItem, aNew.x, aNew.y);
                } else if (overlappingItems.Count == 1) {
                    var targetItem = GetSingleItem(overlappingItems);
                    if (!HasRenderedItemView(targetItem, true)) {
                        targetItem = null;
                    }

                    if (targetItem == null) {
                        isValid = model.CanPlaceItem(sourceItem, aNew.x, aNew.y);
                    }
                    else if (sourceItem.Data == targetItem.Data && targetItem.Data.maxStackSize > 1) {
                        isValid = targetItem.currentStackCount < targetItem.Data.maxStackSize;
                    } else if (source == DragSource.Quickslot) {
                        swapTargetItem = targetItem;
                        isValid = model.CanPlaceItem(sourceItem, aNew.x, aNew.y, targetItem);
                    } else if (sourceModel != null) {
                        var sourcePos = sourceModel.GetItemAnchorPosition(sourceItem);
                        if (sourcePos.x != -1 && sourcePos.y != -1) {
                            swapTargetItem = targetItem;
                            var targetPos = model.GetItemAnchorPosition(targetItem);
                            var draggedOld = new Vector2Int(sourcePos.x, sourcePos.y);
                            var targetOld = new Vector2Int(targetPos.x, targetPos.y);
                            var targetNew = GetRelativeSwapPosition(draggedOld, aNew, targetOld);
                            swapTargetNewPos = targetNew;

                            bool sameModel = ReferenceEquals(sourceModel, model);
                            bool canPlaceDragged = model.CanPlaceItem(sourceItem, aNew.x, aNew.y, targetItem);
                            bool canPlaceTarget = sourceModel.CanPlaceItem(targetItem, targetNew.x, targetNew.y,
                                sourceItem, sameModel ? targetItem : null);
                            bool finalShapesOverlap = sameModel && ItemShapesOverlap(sourceItem, aNew, targetItem, targetNew);

                            isValid = canPlaceDragged && canPlaceTarget && !finalShapesOverlap;
                        }
                    }
                }
            }

            if (swapTargetItem != null && swapTargetNewPos.HasValue && sourceModel != null) {
                var targetController = FindControllerForModel(sourceModel, sourceController);
                if (targetController != null) {
                    var expectedColor = isValid
                        ? new Color(1f, 1f, 1f, 0.4f)
                        : new Color(1f, 0f, 0f, 0.25f);
                    targetController.HighlightShape(swapTargetItem, swapTargetNewPos.Value, expectedColor);
                }
            }

            Color highlightColor = isValid ? new Color(0f, 1f, 0f, 0.3f) : new Color(1f, 0f, 0f, 0.3f);
            HighlightShape(sourceItem, aNew, highlightColor);
        }

        void ShowQuickslotSwapReturnHighlight(ItemInstance sourceItem, Vector2 screenPosition) {
            var quickslotController = QuickslotUIController.Instance;
            if (quickslotController == null || sourceItem == null) return;

            int quickslotIndex = quickslotController.GetSlotIndexAtPosition(screenPosition);
            if (quickslotIndex < 0) return;

            ItemInstance quickslotItem = quickslotController.GetItem(quickslotIndex);
            if (quickslotItem == null || quickslotItem.Data == null) return;

            if (quickslotItem.Data == sourceItem.Data && sourceItem.Data.maxStackSize > 1) {
                return;
            }

            var sourceOldPos = model.GetItemAnchorPosition(sourceItem);
            if (sourceOldPos.x == -1 || sourceOldPos.y == -1) return;

            var highlightController = FindControllerForModel(model, this);
            if (highlightController == null) return;

            bool canPlaceQuickslotItem = model.CanPlaceItem(quickslotItem, sourceOldPos.x, sourceOldPos.y, sourceItem);
            Color returnColor = canPlaceQuickslotItem
                ? new Color(1f, 1f, 1f, 0.4f)
                : new Color(1f, 0f, 0f, 0.25f);

            highlightController.HighlightShape(quickslotItem, new Vector2Int(sourceOldPos.x, sourceOldPos.y), returnColor);
        }

        bool HasRenderedItemView(ItemInstance item, bool refreshIfMissing) {
            if (item == null) return false;

            bool HasAttachedView(out GridItemView itemView) {
                if (!itemViews.TryGetValue(item, out itemView) || itemView == null) {
                    return false;
                }

                return itemView.parent != null && itemView.panel != null;
            }

            if (HasAttachedView(out _)) {
                return true;
            }

            if (!refreshIfMissing) {
                return false;
            }

            RefreshView();
            return HasAttachedView(out _);
        }

        void HighlightShape(ItemInstance item, Vector2Int anchor, Color color) {
            if (item == null || item.Data == null || item.Data.gridShape == null) return;

            var positions = item.Data.gridShape.GetRotatedPositions(item.currentRotation);
            foreach (var pos in positions) {
                int checkX = anchor.x + pos.x;
                int checkY = anchor.y + pos.y;

                if (checkX >= 0 && checkY >= 0 && checkX < width && checkY < height) {
                    int slotIndex = model.GetIndex(checkX, checkY);
                    view.SetSlotColor(slotIndex, color);
                }
            }
        }

        // 테스트용 라운드 시간 단축 버튼으로 사용
        void HandleSave() {
            if (BackendRoundManager.Instance != null) {
                BackendRoundManager.Instance.ForceEndRoundSoon();
            }
        }

        /* 수동 저장/로드 주석 처리 (라운드 단위 자동 저장으로 변경)
        void HandleLoad() {
            if (AuthSession.IsOffline) {
                // 싱글플레이어 오프라인 모드에서는 로컬 불러오기
                GridInventorySaveSystem.LoadInventory(model);
            } else if (BackendPlayerNetworkSync.LocalInstance != null) {
                // 멀티플레이어 모드에서는 호스트가 전체 불러오기
                BackendPlayerNetworkSync.LocalInstance.HostInitiateLoadAll();
            }
            RefreshView();
        }
        */

        readonly Dictionary<ItemInstance, GridItemView> itemViews = new Dictionary<ItemInstance, GridItemView>();



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

        public void ReceiveDrop(ItemInstance item, DragSource source, int sourceIndex, GridSlot targetSlot, GridInventoryModel sourceModel)
        {
            var targetCoords = model.GetCoordinates(targetSlot.Index);
            var draggedNew = new Vector2Int(targetCoords.x, targetCoords.y);

            if (IsSelfDropOnOriginalShape(item, sourceModel, model, draggedNew))
            {
                GlobalDragDropRouter.RevertDrop(item, source, sourceIndex, sourceModel);
                return;
            }

            overlappingItems.Clear();
            var positions = item.Data.gridShape.GetRotatedPositions(item.currentRotation);
            bool outOfBounds = false;

            foreach (var pos in positions)
            {
                int checkX = draggedNew.x + pos.x;
                int checkY = draggedNew.y + pos.y;

                if (checkX < 0 || checkY < 0 || checkX >= width || checkY >= height)
                {
                    outOfBounds = true;
                    break;
                }

                var foundItem = model.Get(checkX, checkY);
                if (foundItem != null && foundItem != item)
                {
                    overlappingItems.Add(foundItem);
                }
            }

            if (outOfBounds || overlappingItems.Count > 1)
            {
                GlobalDragDropRouter.RevertDrop(item, source, sourceIndex, sourceModel);
                return;
            }

            var baseTargetItem = overlappingItems.Count == 1 ? GetSingleItem(overlappingItems) : null;

            // 겹쳤을 때 스택 합치기 로직
            if (baseTargetItem != null && baseTargetItem.Data == item.Data && item.Data.maxStackSize > 1)
            {
                int spaceLeft = item.Data.maxStackSize - baseTargetItem.currentStackCount;
                if (spaceLeft <= 0)
                {
                    GlobalDragDropRouter.RevertDrop(item, source, sourceIndex, sourceModel);
                    return;
                }

                int moveQty = Mathf.Min(item.currentStackCount, spaceLeft);
                baseTargetItem.currentStackCount += moveQty;
                item.currentStackCount -= moveQty;

                if (item.currentStackCount <= 0)
                {
                    if (source == DragSource.Quickslot) QuickslotUIController.Instance.RemoveItemFromSlot(sourceIndex);
                    else if (sourceModel != null) sourceModel.TryRemove(item);
                }
                else
                {
                    if (source == DragSource.Quickslot) QuickslotUIController.Instance.RefreshSlotVisual(sourceIndex);
                    else if (sourceModel != null)
                    {
                        sourceModel.Items.Invoke();
                        GlobalDragDropRouter.RevertDrop(item, source, sourceIndex, sourceModel);
                    }
                }

                model.Items.Invoke();
                return;
            }

            // Simple logic:
            if (baseTargetItem == null)
            {
                if (model.CanPlaceItem(item, targetCoords.x, targetCoords.y))
                {
                    var sourceOldPos = sourceModel != null ? sourceModel.GetItemAnchorPosition(item) : (x: -1, y: -1);
                    bool removedFromQuickslot = false;
                    bool removedFromSource = false;

                    if (source == DragSource.Quickslot) QuickslotUIController.Instance.RemoveItemFromSlot(sourceIndex);
                    if (source == DragSource.Quickslot) removedFromQuickslot = true;
                    else if (sourceModel != null) removedFromSource = sourceModel.TryRemove(item);

                    if (!model.PlaceItem(item, targetCoords.x, targetCoords.y))
                    {
                        if (removedFromQuickslot) QuickslotUIController.Instance.SetItemInSlot(sourceIndex, item);
                        if (removedFromSource && sourceOldPos.x != -1 && sourceOldPos.y != -1)
                            sourceModel.PlaceItem(item, sourceOldPos.x, sourceOldPos.y);
                        GlobalDragDropRouter.RevertDrop(item, source, sourceIndex, sourceModel);
                    }
                }
                else
                {
                    GlobalDragDropRouter.RevertDrop(item, source, sourceIndex, sourceModel);
                }
            }
            else
            {
                var baseTargetPos = model.GetItemAnchorPosition(baseTargetItem);

                if (source == DragSource.Quickslot)
                {
                    model.TryRemove(baseTargetItem);

                    if (model.CanPlaceItem(item, targetCoords.x, targetCoords.y))
                    {
                        QuickslotUIController.Instance.RemoveItemFromSlot(sourceIndex);
                        model.PlaceItem(item, targetCoords.x, targetCoords.y);

                        baseTargetItem.currentRotation = ItemRotation.Deg0;
                        QuickslotUIController.Instance.SetItemInSlot(sourceIndex, baseTargetItem);
                    }
                    else
                    {
                        model.PlaceItem(baseTargetItem, baseTargetPos.x, baseTargetPos.y);
                        GlobalDragDropRouter.RevertDrop(item, source, sourceIndex, sourceModel);
                    }

                    return;
                }

                if (sourceModel == null)
                {
                    GlobalDragDropRouter.RevertDrop(item, source, sourceIndex, sourceModel);
                    return;
                }

                var sourceOldPos = sourceModel.GetItemAnchorPosition(item);
                if (sourceOldPos.x == -1 || sourceOldPos.y == -1)
                {
                    GlobalDragDropRouter.RevertDrop(item, source, sourceIndex, sourceModel);
                    return;
                }

                bool sameModel = ReferenceEquals(sourceModel, model);
                var draggedOld = new Vector2Int(sourceOldPos.x, sourceOldPos.y);
                var targetOld = new Vector2Int(baseTargetPos.x, baseTargetPos.y);
                var targetNew = GetRelativeSwapPosition(draggedOld, draggedNew, targetOld);

                bool canPlaceDragged = model.CanPlaceItem(item, targetCoords.x, targetCoords.y, baseTargetItem);
                bool canPlaceTarget = sourceModel.CanPlaceItem(baseTargetItem, targetNew.x, targetNew.y, item, sameModel ? baseTargetItem : null);
                bool finalShapesOverlap = sameModel && ItemShapesOverlap(item, draggedNew, baseTargetItem, targetNew);

                if (!canPlaceDragged || !canPlaceTarget || finalShapesOverlap)
                {
                    GlobalDragDropRouter.RevertDrop(item, source, sourceIndex, sourceModel);
                    return;
                }

                bool removedTarget = model.TryRemove(baseTargetItem);
                bool removedDragged = sourceModel.TryRemove(item);

                if (!removedTarget || !removedDragged)
                {
                    if (removedTarget) model.PlaceItem(baseTargetItem, baseTargetPos.x, baseTargetPos.y);
                    if (removedDragged) sourceModel.PlaceItem(item, sourceOldPos.x, sourceOldPos.y);
                    GlobalDragDropRouter.RevertDrop(item, source, sourceIndex, sourceModel);
                    return;
                }

                bool placedDragged = model.PlaceItem(item, targetCoords.x, targetCoords.y);
                bool placedTarget = placedDragged && sourceModel.PlaceItem(baseTargetItem, targetNew.x, targetNew.y);

                if (!placedDragged || !placedTarget)
                {
                    if (placedDragged) model.TryRemove(item);
                    if (placedTarget) sourceModel.TryRemove(baseTargetItem);

                    model.PlaceItem(baseTargetItem, baseTargetPos.x, baseTargetPos.y);
                    sourceModel.PlaceItem(item, sourceOldPos.x, sourceOldPos.y);
                    GlobalDragDropRouter.RevertDrop(item, source, sourceIndex, sourceModel);
                }
            }
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


            onApplied?.Invoke();
        }

        void HandleModelChanged(IList<ItemInstance> items) => RefreshView();

        static ItemInstance GetSingleItem(HashSet<ItemInstance> items) {
            foreach (var item in items) {
                return item;
            }

            return null;
        }

        static Vector2Int GetRelativeSwapPosition(Vector2Int draggedOld, Vector2Int draggedNew, Vector2Int targetOld) {
            var delta = draggedNew - draggedOld;
            return targetOld - delta;
        }

        static bool IsSelfDropOnOriginalShape(ItemInstance item, GridInventoryModel sourceModel,
            GridInventoryModel targetModel, Vector2Int targetAnchor) {
            if (item == null || item.Data == null || item.Data.gridShape == null) return false;
            if (sourceModel == null || targetModel == null || !ReferenceEquals(sourceModel, targetModel)) return false;

            var sourceOld = sourceModel.GetItemAnchorPosition(item);
            if (sourceOld.x == -1 || sourceOld.y == -1) return false;

            ItemRotation originalRotation = item.currentRotation;
            var draggedView = GridStorageView.CurrentDraggedItemView;
            if (draggedView != null && draggedView.ItemInst == item) {
                originalRotation = draggedView.OriginalRotation;
            }

            if (item.currentRotation != originalRotation) return false;

            var oldAnchor = new Vector2Int(sourceOld.x, sourceOld.y);
            var originalPositions = item.Data.gridShape.GetRotatedPositions(originalRotation);
            bool targetAnchorIsOriginalCell = false;
            foreach (var pos in originalPositions) {
                if (targetAnchor == oldAnchor + pos) {
                    targetAnchorIsOriginalCell = true;
                    break;
                }
            }

            if (!targetAnchorIsOriginalCell) return false;

            var targetPositions = item.Data.gridShape.GetRotatedPositions(item.currentRotation);
            foreach (var pos in targetPositions) {
                int checkX = targetAnchor.x + pos.x;
                int checkY = targetAnchor.y + pos.y;

                if (checkX < 0 || checkY < 0 || checkX >= targetModel.Width || checkY >= targetModel.Height) {
                    return false;
                }

                var foundItem = targetModel.Get(checkX, checkY);
                if (foundItem != null && foundItem != item) {
                    return false;
                }
            }

            return true;
        }

        static bool ItemShapesOverlap(ItemInstance first, Vector2Int firstAnchor, ItemInstance second, Vector2Int secondAnchor) {
            if (first == null || second == null) return false;

            var firstPositions = first.Data.gridShape.GetRotatedPositions(first.currentRotation);
            var secondPositions = second.Data.gridShape.GetRotatedPositions(second.currentRotation);

            foreach (var firstPos in firstPositions) {
                var firstAbs = firstAnchor + firstPos;
                foreach (var secondPos in secondPositions) {
                    if (firstAbs == secondAnchor + secondPos) {
                        return true;
                    }
                }
            }

            return false;
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
                        itemView.RefreshVisuals();
                        itemView.SetQuantity(item.currentStackCount);
                        view.UpdateItemPosition(itemView, anchorIndex);
                        
                        if (!itemView.IsDraggingThis) {
                            itemView.style.visibility = UnityEngine.UIElements.Visibility.Visible;
                        }
                    }
                }
            }
        }

        public void RefreshAllItemsVisuals() {
            foreach (var kvp in itemViews) {
                var anchorPos = model.GetItemAnchorPosition(kvp.Key);
                int anchorIndex = model.GetIndex(anchorPos.x, anchorPos.y);
                view.UpdateItemPosition(kvp.Value, anchorIndex);
            }
        }

        #region Builder

        public class Builder {
            GridStorageView view;
            int width = 5;
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

