using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.UIElements;
using Systems.GridInventory;
using GridInventoryClass = Systems.GridInventory.GridInventory;

namespace Systems.Loot
{
    public class LootController : MonoBehaviour
    {
        public static LootController Instance { get; private set; }
        public bool IsOpen { get; private set; }
        public bool IsInventoryOnlyOpen => IsOpen && isInventoryOnlyMode;
        private const int PlayerInventoryColumns = 5;
        private const int PlayerInventoryRows = 8;

        [SerializeField] private LootView lootView;

        // Dummy view for the loot grid so GridInventoryController can work with it
        private DummyLootGridView dummyLootGridView;
        private GridInventoryController lootInventoryController;

        // Dummy view and controller for the player inventory side
        private DummyLootGridView dummyPlayerGridView;
        private GridInventoryController dummyPlayerInventoryController;

        private InteractableLoot currentLootSource;
        private string currentStorageId;
        private LootNetworkSync currentNetworkSync;
        private bool isInventoryOnlyMode;

        private UnityEngine.UIElements.VisualElement originalInventoryParent;

        private UnityEngine.UIElements.VisualElement originalInventoryGhostIcon;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public GridSlot GetLootGridSlotAtPosition(Vector2 screenPosition)
        {
            if (!IsOpen || dummyLootGridView == null) return null;
            return dummyLootGridView.GetGridSlotAtPosition(screenPosition);
        }

        public GridSlot GetPlayerGridSlotAtPosition(Vector2 screenPosition)
        {
            if (!IsOpen || dummyPlayerGridView == null) return null;
            return dummyPlayerGridView.GetGridSlotAtPosition(screenPosition);
        }

        public bool IsPositionInsideLootUI(Vector2 screenPosition)
        {
            if (!IsOpen || lootView == null) return false;
            var root = lootView.GetRootVisualElement();
            if (root == null || root.style.display == DisplayStyle.None) return false;

            // Check if the position is inside the loot panel or player inventory panel within the loot view
            var lootPanel = root.Q<VisualElement>(className: "loot-left-panel");
            var playerPanel = root.Q<VisualElement>(className: "loot-right-panel");

            if (lootPanel != null && lootPanel.worldBound.Contains(screenPosition)) return true;
            if (playerPanel != null && playerPanel.worldBound.Contains(screenPosition)) return true;

            return false;
        }

        private float lastSnapshotTime;
        private const float SnapshotCooldown = 0.1f;
        private bool snapshotPending;

        private void Start() 
        {
            lootView.Initialize();

            lootView.OnCloseClicked += CloseLoot;

            GridItemView.OnItemSplitDroppedGlobal -= HandleItemSplitDroppedGlobal;
            GridItemView.OnItemSplitDroppedGlobal += HandleItemSplitDroppedGlobal;

            QuickslotUIController.OnQuickslotSplitRequested -= HandleQuickslotSplitRequestedToLoot;
            QuickslotUIController.OnQuickslotSplitRequested += HandleQuickslotSplitRequestedToLoot;
        }

        private void OnDestroy()
        {
            GridItemView.OnItemSplitDroppedGlobal -= HandleItemSplitDroppedGlobal;

            QuickslotUIController.OnQuickslotSplitRequested -= HandleQuickslotSplitRequestedToLoot;
        }

        private InteractableLoot pendingLootSource;
        private string pendingStorageId;
        private string pendingTitle;
        private LootNetworkSync pendingNetworkSync;

        /// <summary>
        /// RPC 없이 즉시 열어도 되는 세션(Fusion 단일 피어, 런너 없음 등).
        /// GameMode.Single에서 Rpc_ApproveOpenLoot 타깃 미수신 문제를 피함.
        /// </summary>
        static bool ShouldOpenLootImmediately(LootNetworkSync networkSync)
        {
            if (AuthSession.IsOffline)
                return true;

            var runner = networkSync != null ? networkSync.Runner : null;
            if (runner == null || !runner.IsRunning)
                return true;

            return runner.GameMode == GameMode.Single;
        }

        public void RequestOpenLoot(InteractableLoot source, string storageId, string title, LootNetworkSync networkSync)
        {
            if (IsOpen) return;
            
            pendingLootSource = source;
            pendingStorageId = storageId;
            pendingTitle = title;
            pendingNetworkSync = networkSync;

            if (ShouldOpenLootImmediately(networkSync))
            {
                HandleOpenApproved(storageId);
            }
            else if (networkSync != null && networkSync.Runner != null)
            {
                networkSync.Rpc_RequestOpenLoot(networkSync.Runner.LocalPlayer, storageId);
            }
            else
            {
                HandleOpenApproved(storageId);
            }
        }

        public void HandleOpenApproved(string storageId)
        {
            if (pendingStorageId != storageId) return;
            OpenLoot(pendingLootSource, pendingStorageId, pendingTitle, pendingNetworkSync);
            
            pendingLootSource = null;
            pendingStorageId = null;
            pendingTitle = null;
            pendingNetworkSync = null;
        }

        public void HandleOpenDenied(string storageId)
        {
            if (pendingStorageId == storageId)
            {
                Debug.Log($"[LootController] 상자 열기 거부됨 (이미 다른 플레이어가 사용 중): {storageId}");
                pendingLootSource = null;
                pendingStorageId = null;
                pendingTitle = null;
                pendingNetworkSync = null;
            }
        }

        private float openedTime;

        public void OpenInventoryOnly()
        {
            if (IsOpen) return;
            if (lootView == null || GridInventoryClass.Instance == null || GridInventoryClass.Instance.Controller == null)
            {
                return;
            }

            IsOpen = true;
            isInventoryOnlyMode = true;
            openedTime = Time.time;
            lootView.Initialize();

            if (GridInventoryView.Instance != null)
            {
                originalInventoryGhostIcon = GridInventoryView.Instance.GhostIcon;
                GridInventoryView.Instance.GhostIcon = lootView.GhostIcon;
            }

            lootView.SetInventoryOnlyMode(true);
            lootView.Show();
            AttachPlayerInventory();

            GridInventoryView.IsAnyInventoryOpen = true;
            PauseMenuManager.UpdateCursorAndInputState();
        }

        private void OpenLoot(InteractableLoot source, string storageId, string title, LootNetworkSync networkSync)
        {
            if (IsOpen) return;
            IsOpen = true;
            isInventoryOnlyMode = false;
            openedTime = Time.time;
            currentLootSource = source;
            currentStorageId = storageId;
            currentNetworkSync = networkSync;
            lootView.Initialize();

            // Share ghost icon
            if (GridInventoryView.Instance != null)
            {
                originalInventoryGhostIcon = GridInventoryView.Instance.GhostIcon;
                GridInventoryView.Instance.GhostIcon = lootView.GhostIcon;
            }

            // 1. Create a dummy view to run the grid logic for the loot box
            dummyLootGridView = gameObject.AddComponent<DummyLootGridView>();
            dummyLootGridView.Init(lootView.GetLootScrollView(), source.Width, lootView.GhostIcon);

            // 2. Build the Loot Inventory Controller
            var model = networkSync.GetOrCreateModel(storageId, source.Width, source.Height);

            lootInventoryController = new GridInventoryController.Builder(dummyLootGridView)
                .WithDimensions(source.Width, source.Height)
                .WithExistingModel(model)
                .Build();


            lootInventoryController.Model.OnModelChanged -= HandleModelChanged;
            lootInventoryController.Model.OnModelChanged += HandleModelChanged;

            lootView.SetLootMode();
            lootView.SetLootHeader(title);
            lootView.Show();

            // 3. Move Player Inventory to Right Panel
            AttachPlayerInventory();

            UpdateCapacities();
            
            PauseMenuManager.UpdateCursorAndInputState();
        }

        public void CloseLoot()
        {
            if (!IsOpen) return;
            IsOpen = false;

            if (lootInventoryController != null && lootInventoryController.Model != null)
            {
                lootInventoryController.Model.OnModelChanged -= HandleModelChanged;
            }

            SubmitSnapshot(force: true);

            if (currentNetworkSync != null && !AuthSession.IsOffline && currentNetworkSync.Runner != null)
            {
                currentNetworkSync.Rpc_NotifyCloseLoot(currentNetworkSync.Runner.LocalPlayer, currentStorageId);
            }

            // Restore ghost icon
            if (GridInventoryView.Instance != null && originalInventoryGhostIcon != null)
            {
                GridInventoryView.Instance.GhostIcon = originalInventoryGhostIcon;
                originalInventoryGhostIcon = null;
            }

            DetachPlayerInventory();
            lootView.SetLootMode();
            lootView.Hide();

            if (dummyLootGridView != null)
            {
                Destroy(dummyLootGridView);
            }
            lootInventoryController = null;
            currentLootSource = null;
            currentStorageId = null;
            currentNetworkSync = null;
            isInventoryOnlyMode = false;
            GridInventoryView.IsAnyInventoryOpen = false;

            PauseMenuManager.UpdateCursorAndInputState();
        }

        private void Update()
        {
            if (IsOpen && Time.time - openedTime > 0.1f && UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame)
            {
                if (PauseMenuManager.isPaused) return; // 일시정지 중 무시
                PauseMenuManager.CloseOpenInGameUI();
            }

            if (snapshotPending && Time.time - lastSnapshotTime >= SnapshotCooldown)
            {
                SubmitSnapshot(force: true);
            }
        }

        private void HandleModelChanged(IList<ItemInstance> items)
        {
            if (!IsOpen) return;
            // 서버 권위적 모델이므로 클라이언트가 모델 변경 시 스냅샷을 전송하지 않음
            // SubmitSnapshot(force: false);
        }

        private void AttachPlayerInventory()
        {
            if (GridInventoryClass.Instance == null || GridInventoryClass.Instance.Controller == null) return;
            var playerModel = GridInventoryClass.Instance.Controller.Model;
            if (playerModel == null) return;
            playerModel.EnsureDimensions(PlayerInventoryColumns, PlayerInventoryRows);

            var container = lootView.GetPlayerInventoryContainer();
            if (container == null) return;
            container.Clear();

            // 1. 스크롤 뷰 동적 생성
            var scrollView = new UnityEngine.UIElements.ScrollView();
            scrollView.name = "player-scroll-view";
            scrollView.AddToClassList("slots-scroll-view");
            scrollView.style.flexGrow = 1;
            scrollView.style.width = new UnityEngine.UIElements.StyleLength(new UnityEngine.UIElements.Length(100, UnityEngine.UIElements.LengthUnit.Percent));
            scrollView.style.height = new UnityEngine.UIElements.StyleLength(new UnityEngine.UIElements.Length(100, UnityEngine.UIElements.LengthUnit.Percent));
            scrollView.style.display = UnityEngine.UIElements.DisplayStyle.Flex;
            scrollView.horizontalScrollerVisibility = UnityEngine.UIElements.ScrollerVisibility.Hidden;
            scrollView.verticalScrollerVisibility = UnityEngine.UIElements.ScrollerVisibility.Hidden;

            var slotsContainer = new UnityEngine.UIElements.VisualElement();
            slotsContainer.name = "lootSlotsContainer";
            slotsContainer.AddToClassList("slotsContainer");
            slotsContainer.style.marginTop = 12f;
            slotsContainer.style.marginBottom = 12f;
            
            // 핵심 수정: FlexLayout 속성 명시적 지정
            slotsContainer.style.flexDirection = UnityEngine.UIElements.FlexDirection.Row;
            slotsContainer.style.flexWrap = UnityEngine.UIElements.Wrap.Wrap;
            slotsContainer.style.width = (PlayerInventoryColumns * 100f) + 20f + 4f; // visualCols * SlotTotalSize + padding + margin
            slotsContainer.style.height = (PlayerInventoryRows * 100f) + 20f + 4f; // visualRows * SlotTotalSize + padding + margin
            
            scrollView.Add(slotsContainer);
            container.Add(scrollView);

            // 3. 기존 GridInventory의 스타일 시트 복사
            if (GridInventoryView.Instance != null)
            {
                var gridStyleSheet = GridInventoryView.Instance.GridStyleSheet;
                if (gridStyleSheet != null && !lootView.GetRootVisualElement().styleSheets.Contains(gridStyleSheet))
                {
                    lootView.GetRootVisualElement().styleSheets.Add(gridStyleSheet);
                }
            }

            // 4. Dummy View 초기화 (이제 ScrollView를 넘겨주므로 에러 없음)
            dummyPlayerGridView = gameObject.AddComponent<DummyLootGridView>();
            dummyPlayerGridView.Init(scrollView, PlayerInventoryColumns, lootView.GhostIcon, Systems.GridInventory.DragSource.Inventory);
            
            // 코루틴 수동 호출로 GridStorageView.InitializeView를 동기식으로 실행 (슬롯 생성 등 보장)
            var initRoutine = dummyPlayerGridView.InitializeView(playerModel.Items.Length);
            while (initRoutine.MoveNext()) { }
            
            dummyPlayerInventoryController = new GridInventoryController.Builder(dummyPlayerGridView)
                .WithDimensions(PlayerInventoryColumns, PlayerInventoryRows)
                .WithExistingModel(playerModel)
                .Build();
        }

        private void DetachPlayerInventory()
        {
            if (dummyPlayerInventoryController != null)
            {
                dummyPlayerInventoryController = null;
            }
            if (dummyPlayerGridView != null)
            {
                var container = lootView.GetPlayerInventoryContainer();
                if (container != null)
                {
                    container.Clear(); // Removes the dynamically created scroll view
                }
                Destroy(dummyPlayerGridView);
                dummyPlayerGridView = null;
            }
        }



        private void HandleQuickslotSplitRequestedToLoot(ItemInstance item, int qsIndex, Vector2 screenPos)
        {
            if (!IsOpen || dummyLootGridView == null || lootInventoryController == null || currentNetworkSync == null) return;

            var qsCtl = QuickslotUIController.Instance;
            if (qsCtl == null || qsCtl.GetItem(qsIndex) != item) return;

            GridSlot lootSlot = dummyLootGridView.GetGridSlotAtPosition(screenPos);
            if (lootSlot == null) return;

            var lootModel = lootInventoryController.Model;
            var targetCoords = lootModel.GetCoordinates(lootSlot.Index);
            var draggedNew = new Vector2Int(targetCoords.x, targetCoords.y);
            var overlappingItems = new HashSet<ItemInstance>();
            bool outOfBounds = false;

            foreach (var pos in item.Data.gridShape.GetRotatedPositions(item.currentRotation))
            {
                int checkX = draggedNew.x + pos.x;
                int checkY = draggedNew.y + pos.y;

                if (checkX < 0 || checkY < 0 || checkX >= lootModel.Width || checkY >= lootModel.Height)
                {
                    outOfBounds = true;
                    break;
                }

                var foundItem = lootModel.Get(checkX, checkY);
                if (foundItem != null && foundItem != item)
                {
                    overlappingItems.Add(foundItem);
                }
            }

            if (outOfBounds || overlappingItems.Count > 1) return;

            var baseTargetItem = overlappingItems.Count == 1 ? GetSingleItem(overlappingItems) : null;

            bool isSwap = baseTargetItem != null && baseTargetItem.Data != item.Data;
            if (isSwap) return;

            int maxQty;
            if (baseTargetItem == null)
            {
                var probe = new ItemInstance(item.Data, 1);
                probe.currentRotation = item.currentRotation;
                if (!lootModel.CanPlaceItem(probe, targetCoords.x, targetCoords.y)) return;
                maxQty = item.currentStackCount - 1;
            }
            else
            {
                int space = baseTargetItem.Data.maxStackSize - baseTargetItem.currentStackCount;
                if (space <= 0) return;
                maxQty = Mathf.Min(item.currentStackCount - 1, space);
            }

            if (maxQty < 1) return;

            QuantityPopupView.EnsureExists();
            QuantityPopupView.Show(maxQty,
                qty => ExecuteQuickslotSplitToLootSlot(item, qsIndex, lootSlot, qty));
        }

        private void ExecuteQuickslotSplitToLootSlot(ItemInstance item, int qsIndex, GridSlot lootSlot, int qty)
        {
            var qsCtl = QuickslotUIController.Instance;
            if (qsCtl == null || qsCtl.GetItem(qsIndex) != item || qty < 1) return;

            int stack = item.currentStackCount;
            if (qty >= stack) return;

            var lootModel = lootInventoryController.Model;
            var targetCoords = lootModel.GetCoordinates(lootSlot.Index);
            var draggedNew = new Vector2Int(targetCoords.x, targetCoords.y);
            var overlappingItems = new HashSet<ItemInstance>();
            bool outOfBounds = false;

            foreach (var pos in item.Data.gridShape.GetRotatedPositions(item.currentRotation))
            {
                int checkX = draggedNew.x + pos.x;
                int checkY = draggedNew.y + pos.y;

                if (checkX < 0 || checkY < 0 || checkX >= lootModel.Width || checkY >= lootModel.Height)
                {
                    outOfBounds = true;
                    break;
                }

                var foundItem = lootModel.Get(checkX, checkY);
                if (foundItem != null && foundItem != item)
                {
                    overlappingItems.Add(foundItem);
                }
            }

            if (outOfBounds || overlappingItems.Count > 1) return;

            var baseTargetItem = overlappingItems.Count == 1 ? GetSingleItem(overlappingItems) : null;

            bool isHost = currentNetworkSync.HasStateAuthority;
            bool isOffline = AuthSession.IsOffline;

            if (baseTargetItem != null && baseTargetItem.Data == item.Data && item.Data.maxStackSize > 1)
            {
                int space = baseTargetItem.Data.maxStackSize - baseTargetItem.currentStackCount;
                int moveQty = Mathf.Min(qty, space);
                if (moveQty < 1) return;

                item.currentStackCount = stack - moveQty;
                qsCtl.RefreshSlotVisual(qsIndex);

                if (!isHost || isOffline)
                {
                    baseTargetItem.currentStackCount += moveQty;
                    lootModel.Items.Invoke();
                }

                if (!isOffline && currentNetworkSync.Runner != null)
                    currentNetworkSync.Rpc_RequestPutItem(currentNetworkSync.Runner.LocalPlayer, currentStorageId,
                        item.Data.itemID, 0, lootSlot.Index, moveQty, (int)item.currentRotation);
                SubmitSnapshot(force: true);
            }
            else if (baseTargetItem == null)
            {
                var placed = new ItemInstance(item.Data, qty);
                placed.currentRotation = item.currentRotation;

                if (!lootModel.CanPlaceItem(placed, targetCoords.x, targetCoords.y)) return;

                item.currentStackCount = stack - qty;
                qsCtl.RefreshSlotVisual(qsIndex);

                if (!isHost || isOffline)
                {
                    lootModel.PlaceItem(placed, targetCoords.x, targetCoords.y);
                    lootModel.Items.Invoke();
                }

                if (!isOffline && currentNetworkSync.Runner != null)
                    currentNetworkSync.Rpc_RequestPutItem(currentNetworkSync.Runner.LocalPlayer, currentStorageId,
                        item.Data.itemID, 0, lootSlot.Index, qty, (int)item.currentRotation);
                SubmitSnapshot(force: true);
            }
        }

        public void ReceivePlayerDrop(ItemInstance item, Systems.GridInventory.DragSource source, int sourceIndex, GridSlot playerSlot, GridInventoryModel sourceModel)
        {
            if (dummyPlayerInventoryController != null)
            {
                dummyPlayerInventoryController.ReceiveDrop(item, source, sourceIndex, playerSlot, sourceModel);
            }
        }

        public void NotifyLootModelChangedFromExternalDrop(GridInventoryModel sourceModel)
        {
            if (!IsOpen || lootInventoryController == null || !ReferenceEquals(sourceModel, lootInventoryController.Model))
            {
                return;
            }

            SubmitSnapshot(force: true);
        }

        public void ReceiveQuickslotDrop(ItemInstance item, int quickslotIndex)
        {
            if (!IsOpen || lootInventoryController == null || currentNetworkSync == null || item == null)
            {
                Systems.GridInventory.GlobalDragDropRouter.RevertDrop(item, Systems.GridInventory.DragSource.Loot, -1,
                    lootInventoryController?.Model);
                return;
            }

            var qs = QuickslotUIController.Instance;
            if (qs == null)
            {
                Systems.GridInventory.GlobalDragDropRouter.RevertDrop(item, Systems.GridInventory.DragSource.Loot, -1,
                    lootInventoryController.Model);
                return;
            }

            var lootModel = lootInventoryController.Model;
            var sourcePos = lootModel.GetItemAnchorPosition(item);
            if (sourcePos.x == -1 || sourcePos.y == -1)
            {
                Systems.GridInventory.GlobalDragDropRouter.RevertDrop(item, Systems.GridInventory.DragSource.Loot, -1, lootModel);
                return;
            }

            int sourceIndex = lootModel.GetIndex(sourcePos.x, sourcePos.y);
            ItemInstance quickslotItem = qs.GetItem(quickslotIndex);

            if (quickslotItem != null && quickslotItem.Data == item.Data && item.Data.maxStackSize > 1)
            {
                int spaceLeft = item.Data.maxStackSize - quickslotItem.currentStackCount;
                if (spaceLeft <= 0)
                {
                    Systems.GridInventory.GlobalDragDropRouter.RevertDrop(item, Systems.GridInventory.DragSource.Loot, -1, lootModel);
                    return;
                }

                int moveQty = Mathf.Min(item.currentStackCount, spaceLeft);
                quickslotItem.currentStackCount += moveQty;
                item.currentStackCount -= moveQty;

                if (item.currentStackCount <= 0)
                {
                    lootModel.TryRemove(item);
                }
                else
                {
                    lootModel.Items.Invoke();
                }

                qs.RefreshSlotVisual(quickslotIndex);
                RequestLootTake(sourceIndex, moveQty);
                SubmitSnapshot(force: true);
                return;
            }

            if (quickslotItem == null)
            {
                int movedCount = item.currentStackCount;
                lootModel.TryRemove(item);

                item.currentRotation = ItemRotation.Deg0;
                qs.SetItemInSlot(quickslotIndex, item);
                qs.RefreshSlotVisual(quickslotIndex);

                RequestLootTake(sourceIndex, movedCount);
                SubmitSnapshot(force: true);
                return;
            }

            ItemRotation quickslotOriginalRotation = quickslotItem.currentRotation;
            quickslotItem.currentRotation = ItemRotation.Deg0;

            lootModel.TryRemove(item);
            if (!lootModel.CanPlaceItem(quickslotItem, sourcePos.x, sourcePos.y))
            {
                quickslotItem.currentRotation = quickslotOriginalRotation;
                lootModel.PlaceItem(item, sourcePos.x, sourcePos.y);
                Systems.GridInventory.GlobalDragDropRouter.RevertDrop(item, Systems.GridInventory.DragSource.Loot, -1, lootModel);
                return;
            }

            int movedItemCount = item.currentStackCount;
            int swapItemCount = quickslotItem.currentStackCount;
            lootModel.PlaceItem(quickslotItem, sourcePos.x, sourcePos.y);

            item.currentRotation = ItemRotation.Deg0;
            qs.SetItemInSlot(quickslotIndex, item);
            qs.RefreshSlotVisual(quickslotIndex);

            RequestLootTakeWithSwap(sourceIndex, movedItemCount, quickslotItem.Data.itemID, swapItemCount);
            SubmitSnapshot(force: true);
        }

        private ItemRotation GetTargetLogicalRotation(ItemInstance item, Systems.GridInventory.GridStorageView targetView) {
            return item.currentRotation;
        }

        public void ReceiveDrop(ItemInstance item, Systems.GridInventory.DragSource source, int sourceIndex, GridSlot lootSlot, GridInventoryModel sourceModel)
        {
            var originalRotation = item.currentRotation;
            item.currentRotation = GetTargetLogicalRotation(item, dummyLootGridView);

            if (!IsOpen || lootInventoryController == null || currentNetworkSync == null) 
            {
                item.currentRotation = originalRotation;
                return;
            }

            var lootModel = lootInventoryController.Model;
            var targetCoords = lootModel.GetCoordinates(lootSlot.Index);
            var draggedNew = new Vector2Int(targetCoords.x, targetCoords.y);
            var overlappingItems = new HashSet<ItemInstance>();
            bool outOfBounds = false;
            bool sameLootModel = source == Systems.GridInventory.DragSource.Loot && ReferenceEquals(sourceModel, lootModel);

            foreach (var pos in item.Data.gridShape.GetRotatedPositions(item.currentRotation))
            {
                int checkX = draggedNew.x + pos.x;
                int checkY = draggedNew.y + pos.y;

                if (checkX < 0 || checkY < 0 || checkX >= lootModel.Width || checkY >= lootModel.Height)
                {
                    outOfBounds = true;
                    break;
                }

                var foundItem = lootModel.Get(checkX, checkY);
                if (foundItem != null && foundItem != item)
                {
                    overlappingItems.Add(foundItem);
                }
            }

            if (outOfBounds || overlappingItems.Count > 1)
            {
                item.currentRotation = originalRotation;
                Systems.GridInventory.GlobalDragDropRouter.RevertDrop(item, source, sourceIndex, sourceModel);
                return;
            }

            var baseTargetItem = overlappingItems.Count == 1 ? GetSingleItem(overlappingItems) : null;

            bool isHost = currentNetworkSync.HasStateAuthority;
            bool isOffline = AuthSession.IsOffline;

            if (sameLootModel)
            {
                MoveWithinOpenLoot(item, originalRotation, lootSlot, targetCoords, baseTargetItem);
                return;
            }

            if (baseTargetItem != null && baseTargetItem.Data == item.Data && item.Data.maxStackSize > 1)
            {
                // 스택 병합
                int spaceLeft = item.Data.maxStackSize - baseTargetItem.currentStackCount;
                if (spaceLeft <= 0)
                {
                    item.currentRotation = originalRotation;
                    Systems.GridInventory.GlobalDragDropRouter.RevertDrop(item, source, sourceIndex, sourceModel);
                    return;
                }

                int moveQty = Mathf.Min(item.currentStackCount, spaceLeft);
                item.currentStackCount -= moveQty;

                if (item.currentStackCount <= 0)
                {
                    if (source == Systems.GridInventory.DragSource.Quickslot) QuickslotUIController.Instance.RemoveItemFromSlot(sourceIndex);
                    else if (sourceModel != null) sourceModel.TryRemove(item);
                }
                else
                {
                    if (source == Systems.GridInventory.DragSource.Quickslot) QuickslotUIController.Instance.RefreshSlotVisual(sourceIndex);
                    else if (sourceModel != null)
                    {
                        sourceModel.Items.Invoke();
                        Systems.GridInventory.GlobalDragDropRouter.RevertDrop(item, source, sourceIndex, sourceModel);
                    }
                }

                if (!isHost || isOffline)
                {
                    baseTargetItem.currentStackCount += moveQty;
                    lootModel.Items.Invoke();
                }

                if (moveQty > 0)
                {
                    if (!isOffline && currentNetworkSync.Runner != null)
                        currentNetworkSync.Rpc_RequestPutItem(currentNetworkSync.Runner.LocalPlayer, currentStorageId,
                            item.Data.itemID, 0, lootSlot.Index, moveQty, (int)item.currentRotation);
                    SubmitSnapshot(force: true);
                }
            }
            else if (baseTargetItem == null)
            {
                // 빈 공간 배치
                if (lootModel.CanPlaceItem(item, targetCoords.x, targetCoords.y))
                {
                    int oldIndex = -1;
                    if (source == Systems.GridInventory.DragSource.Loot && sourceModel == lootModel)
                    {
                        var oldPos = sourceModel.GetItemAnchorPosition(item);
                        if (oldPos.x != -1) oldIndex = sourceModel.GetIndex(oldPos.x, oldPos.y);
                    }

                    if (source == Systems.GridInventory.DragSource.Quickslot) QuickslotUIController.Instance.RemoveItemFromSlot(sourceIndex);
                    else if (sourceModel != null) sourceModel.TryRemove(item);

                    if (!isHost || isOffline)
                    {
                        lootModel.PlaceItem(item, targetCoords.x, targetCoords.y);
                        lootModel.Items.Invoke();
                    }

                    if (!isOffline && currentNetworkSync.Runner != null)
                    {
                        if (source == Systems.GridInventory.DragSource.Loot && sourceModel == lootModel && oldIndex != -1)
                        {
                            currentNetworkSync.Rpc_RequestMoveItem(currentNetworkSync.Runner.LocalPlayer, currentStorageId, oldIndex, lootSlot.Index, (int)item.currentRotation);
                        }
                        else
                        {
                            currentNetworkSync.Rpc_RequestPutItem(currentNetworkSync.Runner.LocalPlayer, currentStorageId,
                                item.Data.itemID, 0, lootSlot.Index, item.currentStackCount, (int)item.currentRotation);
                        }
                    }
                    SubmitSnapshot(force: true);
                }
                else
                {
                    item.currentRotation = originalRotation;
                    Systems.GridInventory.GlobalDragDropRouter.RevertDrop(item, source, sourceIndex, sourceModel);
                }
            }
            else
            {
                // 1:1 스왑
                var baseTargetPos = lootModel.GetItemAnchorPosition(baseTargetItem);
                var sourceOld = sourceModel != null ? sourceModel.GetItemAnchorPosition(item) : (x: -1, y: -1);
                var sourceOldPos = new Vector2Int(sourceOld.x, sourceOld.y);
                bool sameModel = sourceModel != null && ReferenceEquals(sourceModel, lootModel);
                int oldIndex = sameModel && sourceOldPos.x != -1 ? sourceModel.GetIndex(sourceOldPos.x, sourceOldPos.y) : -1;
                var draggedOld = new Vector2Int(sourceOldPos.x, sourceOldPos.y);
                var targetOld = new Vector2Int(baseTargetPos.x, baseTargetPos.y);
                var targetNew = GetRelativeSwapPosition(draggedOld, draggedNew, targetOld);
                bool canSwapTarget = source == Systems.GridInventory.DragSource.Quickslot ||
                    (sourceModel != null && sourceOldPos.x != -1 && sourceOldPos.y != -1 &&
                     sourceModel.CanPlaceItem(baseTargetItem, targetNew.x, targetNew.y, item, sameModel ? baseTargetItem : null));
                bool finalShapesOverlap = sameModel && ItemShapesOverlap(item, draggedNew, baseTargetItem, targetNew);

                if (lootModel.CanPlaceItem(item, targetCoords.x, targetCoords.y, baseTargetItem) && canSwapTarget && !finalShapesOverlap)
                {
                    if (sameModel && isHost && !isOffline)
                    {
                        if (currentNetworkSync.Runner != null && oldIndex != -1)
                        {
                            currentNetworkSync.Rpc_RequestMoveItem(currentNetworkSync.Runner.LocalPlayer, currentStorageId,
                                oldIndex, lootSlot.Index, (int)item.currentRotation);
                        }
                        SubmitSnapshot(force: true);
                        return;
                    }

                    lootModel.TryRemove(baseTargetItem);

                    if (source == Systems.GridInventory.DragSource.Quickslot) QuickslotUIController.Instance.RemoveItemFromSlot(sourceIndex);
                    else if (sourceModel != null) sourceModel.TryRemove(item);

                    if (source == Systems.GridInventory.DragSource.Quickslot)
                    {
                        baseTargetItem.currentRotation = ItemRotation.Deg0;
                    }
                    
                    if (source == Systems.GridInventory.DragSource.Quickslot)
                    {
                        QuickslotUIController.Instance.SetItemInSlot(sourceIndex, baseTargetItem);
                    }
                    else if (sourceModel != null)
                    {
                        if (sourceModel.CanPlaceItem(baseTargetItem, targetNew.x, targetNew.y, item, sameModel ? baseTargetItem : null))
                        {
                            sourceModel.PlaceItem(baseTargetItem, targetNew.x, targetNew.y);
                        }
                        else
                        {
                            if (!sourceModel.TryAdd(baseTargetItem))
                            {
                                Systems.GridInventory.GlobalDragDropRouter.ProcessDrop(baseTargetItem, Systems.GridInventory.DragSource.Loot, -1, Vector2.zero, null);
                            }
                        }
                    }

                    if (!isHost || isOffline)
                    {
                        lootModel.PlaceItem(item, targetCoords.x, targetCoords.y);
                        lootModel.Items.Invoke();
                    }
                    else
                    {
                        lootModel.PlaceItem(baseTargetItem, baseTargetPos.x, baseTargetPos.y);
                    }

                    if (!isOffline && currentNetworkSync.Runner != null)
                    {
                        if (source == Systems.GridInventory.DragSource.Loot && sourceModel == lootModel && oldIndex != -1)
                        {
                            currentNetworkSync.Rpc_RequestMoveItem(currentNetworkSync.Runner.LocalPlayer, currentStorageId,
                                oldIndex, lootSlot.Index, (int)item.currentRotation);
                        }
                        else
                        {
                            // External swap
                            currentNetworkSync.Rpc_RequestPutItemWithSwap(currentNetworkSync.Runner.LocalPlayer, currentStorageId,
                                item.Data.itemID, 0, lootSlot.Index, item.currentStackCount, (int)item.currentRotation,
                                baseTargetItem.Data.itemID, baseTargetItem.currentStackCount);
                        }
                    }
                    SubmitSnapshot(force: true);
                }
                else
                {
                    item.currentRotation = originalRotation;
                    Systems.GridInventory.GlobalDragDropRouter.RevertDrop(item, source, sourceIndex, sourceModel);
                }
            }
        }

        private void MoveWithinOpenLoot(ItemInstance item, ItemRotation originalRotation, GridSlot lootSlot,
            (int x, int y) targetCoords, ItemInstance baseTargetItem)
        {
            var lootModel = lootInventoryController.Model;
            var sourceOld = lootModel.GetItemAnchorPosition(item);
            if (sourceOld.x == -1 || sourceOld.y == -1)
            {
                item.currentRotation = originalRotation;
                Systems.GridInventory.GlobalDragDropRouter.RevertDrop(item, Systems.GridInventory.DragSource.Loot, -1, lootModel);
                return;
            }

            int oldIndex = lootModel.GetIndex(sourceOld.x, sourceOld.y);
            if (oldIndex == lootSlot.Index && item.currentRotation == originalRotation)
            {
                Systems.GridInventory.GlobalDragDropRouter.RevertDrop(item, Systems.GridInventory.DragSource.Loot, -1, lootModel);
                return;
            }

            var draggedNew = new Vector2Int(targetCoords.x, targetCoords.y);
            var draggedOld = new Vector2Int(sourceOld.x, sourceOld.y);

            if (baseTargetItem != null && baseTargetItem.Data == item.Data && item.Data.maxStackSize > 1)
            {
                int spaceLeft = item.Data.maxStackSize - baseTargetItem.currentStackCount;
                if (spaceLeft <= 0)
                {
                    item.currentRotation = originalRotation;
                    Systems.GridInventory.GlobalDragDropRouter.RevertDrop(item, Systems.GridInventory.DragSource.Loot, -1, lootModel);
                    return;
                }

                int moveQty = Mathf.Min(item.currentStackCount, spaceLeft);
                lootModel.TryRemove(item);
                baseTargetItem.currentStackCount += moveQty;
                item.currentStackCount -= moveQty;

                if (item.currentStackCount > 0)
                {
                    lootModel.PlaceItem(item, sourceOld.x, sourceOld.y);
                }

                lootModel.Items.Invoke();
                RequestLootMove(oldIndex, lootSlot.Index, item.currentRotation);
                SubmitSnapshot(force: true);
                return;
            }

            if (baseTargetItem == null)
            {
                lootModel.TryRemove(item);
                if (!lootModel.PlaceItem(item, targetCoords.x, targetCoords.y))
                {
                    item.currentRotation = originalRotation;
                    lootModel.PlaceItem(item, sourceOld.x, sourceOld.y);
                    Systems.GridInventory.GlobalDragDropRouter.RevertDrop(item, Systems.GridInventory.DragSource.Loot, -1, lootModel);
                    return;
                }

                RequestLootMove(oldIndex, lootSlot.Index, item.currentRotation);
                SubmitSnapshot(force: true);
                return;
            }

            var baseTargetPos = lootModel.GetItemAnchorPosition(baseTargetItem);
            var targetOld = new Vector2Int(baseTargetPos.x, baseTargetPos.y);
            var targetNew = GetRelativeSwapPosition(draggedOld, draggedNew, targetOld);

            bool canPlaceDragged = lootModel.CanPlaceItem(item, targetCoords.x, targetCoords.y, baseTargetItem);
            bool canPlaceTarget = lootModel.CanPlaceItem(baseTargetItem, targetNew.x, targetNew.y, item, baseTargetItem);
            bool finalShapesOverlap = ItemShapesOverlap(item, draggedNew, baseTargetItem, targetNew);

            if (!canPlaceDragged || !canPlaceTarget || finalShapesOverlap)
            {
                item.currentRotation = originalRotation;
                Systems.GridInventory.GlobalDragDropRouter.RevertDrop(item, Systems.GridInventory.DragSource.Loot, -1, lootModel);
                return;
            }

            lootModel.TryRemove(item);
            lootModel.TryRemove(baseTargetItem);

            bool placedItem = lootModel.PlaceItem(item, targetCoords.x, targetCoords.y);
            bool placedTarget = placedItem && lootModel.PlaceItem(baseTargetItem, targetNew.x, targetNew.y);

            if (!placedItem || !placedTarget)
            {
                if (placedItem) lootModel.TryRemove(item);
                if (placedTarget) lootModel.TryRemove(baseTargetItem);

                item.currentRotation = originalRotation;
                lootModel.PlaceItem(item, sourceOld.x, sourceOld.y);
                lootModel.PlaceItem(baseTargetItem, baseTargetPos.x, baseTargetPos.y);
                Systems.GridInventory.GlobalDragDropRouter.RevertDrop(item, Systems.GridInventory.DragSource.Loot, -1, lootModel);
                return;
            }

            RequestLootMove(oldIndex, lootSlot.Index, item.currentRotation);
            SubmitSnapshot(force: true);
        }

        private void RequestLootMove(int oldIndex, int targetIndex, ItemRotation rotation)
        {
            if (!AuthSession.IsOffline && currentNetworkSync != null && currentNetworkSync.Runner != null)
            {
                currentNetworkSync.Rpc_RequestMoveItem(currentNetworkSync.Runner.LocalPlayer, currentStorageId,
                    oldIndex, targetIndex, (int)rotation);
            }
        }

        private void RequestLootTake(int sourceIndex, int quantity)
        {
            if (!AuthSession.IsOffline && currentNetworkSync != null && currentNetworkSync.Runner != null)
            {
                currentNetworkSync.Rpc_RequestTakeItem(currentNetworkSync.Runner.LocalPlayer, currentStorageId,
                    sourceIndex, 0, quantity);
            }
        }

        private void RequestLootTakeWithSwap(int sourceIndex, int takeQuantity, string swapItemId, int swapQuantity)
        {
            if (!AuthSession.IsOffline && currentNetworkSync != null && currentNetworkSync.Runner != null)
            {
                currentNetworkSync.Rpc_RequestTakeItemWithSwap(currentNetworkSync.Runner.LocalPlayer, currentStorageId,
                    swapItemId, sourceIndex, 0, takeQuantity, swapQuantity);
            }
        }

        private static Vector2Int GetRelativeSwapPosition(Vector2Int draggedOld, Vector2Int draggedNew, Vector2Int targetOld)
        {
            var delta = draggedNew - draggedOld;
            return targetOld - delta;
        }

        private static bool ItemShapesOverlap(ItemInstance first, Vector2Int firstAnchor, ItemInstance second, Vector2Int secondAnchor)
        {
            if (first == null || second == null) return false;

            var firstPositions = first.Data.gridShape.GetRotatedPositions(first.currentRotation);
            var secondPositions = second.Data.gridShape.GetRotatedPositions(second.currentRotation);

            foreach (var firstPos in firstPositions)
            {
                var firstAbs = firstAnchor + firstPos;
                foreach (var secondPos in secondPositions)
                {
                    if (firstAbs == secondAnchor + secondPos)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static ItemInstance GetSingleItem(HashSet<ItemInstance> items)
        {
            foreach (var item in items)
            {
                return item;
            }

            return null;
        }

        private void HandleItemSplitDroppedGlobal(GridItemView itemView, Vector2 screenPos)
        {
            QuantityPopupView.EnsureExists();

            ItemInstance item = itemView.ItemInst;
            if (item == null || item.currentStackCount <= 1) return;

            GridInventoryController playerCtrl = GridInventoryClass.Instance?.Controller;

            if (!IsOpen)
            {
                if (playerCtrl != null && playerCtrl.TryConsumeGridSplitToQuickslot(itemView, screenPos))
                    return;

                playerCtrl?.TrySplitAfterRightClick(itemView, screenPos);
                return;
            }

            if (isInventoryOnlyMode)
            {
                if (playerCtrl != null && playerCtrl.TryConsumeGridSplitToQuickslot(itemView, screenPos))
                    return;

                playerCtrl?.TrySplitAfterRightClick(itemView, screenPos);
                return;
            }

            if (lootInventoryController == null || dummyLootGridView == null ||
                GridInventoryView.Instance == null || currentNetworkSync == null)
                return;

            var lootModel = lootInventoryController.Model;
            var playerModel = playerCtrl?.Model;

            GridInventoryModel sourceModel = null;
            if (lootModel.GetItemAnchorPosition(item).x != -1) sourceModel = lootModel;
            else if (playerModel != null && playerModel.GetItemAnchorPosition(item).x != -1) sourceModel = playerModel;

            if (sourceModel == null) return;

            bool srcLoot = ReferenceEquals(sourceModel, lootModel);

            if (!srcLoot && playerCtrl != null)
            {
                if (playerCtrl.TryConsumeGridSplitToQuickslot(itemView, screenPos))
                    return;
            }

            if (srcLoot && QuickslotUIController.Instance != null)
            {
                int qi = QuickslotUIController.Instance.GetSlotIndexAtPosition(screenPos);
                if (qi >= 0)
                {
                    TryPromptLootSplitToQuickslot(item, qi);
                    return;
                }
            }

            GridSlot lootSlot = dummyLootGridView.GetGridSlotAtPosition(screenPos);
            GridSlot playerSlot = GetPlayerGridSlotAtPosition(screenPos);

            if (lootSlot != null && srcLoot)
            {
                lootInventoryController.TrySplitAfterRightClick(itemView, screenPos, SubmitSnapshotAfterSplit);
                return;
            }

            if (playerSlot != null && !srcLoot && playerCtrl != null)
            {
                playerCtrl.TrySplitAfterRightClick(itemView, screenPos);
                return;
            }

            if (!srcLoot && lootSlot != null)
            {
                TryPromptCrossPutSplit(item, lootSlot);
                return;
            }

            if (srcLoot && playerSlot != null)
            {
                TryPromptCrossTakeSplit(item, playerSlot);
            }
        }

        private void SubmitSnapshotAfterSplit()
        {
            SubmitSnapshot(force: true);
        }

        private void TryPromptCrossPutSplit(ItemInstance item, GridSlot lootSlot)
        {
            var playerModel = GridInventoryClass.Instance.Controller.Model;
            var lootModel = lootInventoryController.Model;
            var (sx, sy) = playerModel.GetItemAnchorPosition(item);
            if (sx == -1) return;

            var targetCoords = lootModel.GetCoordinates(lootSlot.Index);
            var baseTarget = lootModel.Get(targetCoords.x, targetCoords.y);

            bool isSwap = baseTarget != null && baseTarget.Data != item.Data;
            if (isSwap) return;

            int maxQty;
            if (baseTarget == null)
            {
                var probe = new ItemInstance(item.Data, 1);
                if (!lootModel.CanPlaceItem(probe, targetCoords.x, targetCoords.y)) return;
                maxQty = item.currentStackCount - 1;
            }
            else
            {
                int space = baseTarget.Data.maxStackSize - baseTarget.currentStackCount;
                if (space <= 0) return;
                maxQty = Mathf.Min(item.currentStackCount - 1, space);
            }

            int sourceInventoryIndex = playerModel.GetIndex(sx, sy);
            QuantityPopupView.Show(maxQty,
                qty => ApplyCrossPutSplitPredicted(item, qty, sx, sy, targetCoords.x, targetCoords.y,
                    lootSlot.Index, sourceInventoryIndex));
        }

        private void ApplyCrossPutSplitPredicted(ItemInstance item, int qty, int playerOldX, int playerOldY, int lootX,
            int lootY, int lootSlotIndex, int sourceInventorySlotIndex)
        {
            bool isHost = currentNetworkSync.HasStateAuthority;
            bool isOffline = AuthSession.IsOffline;

            var playerModel = GridInventoryClass.Instance.Controller.Model;
            var lootModel = lootInventoryController.Model;

            int stack = item.currentStackCount;
            if (qty < 1 || qty >= stack) return;

            playerModel.TryRemove(item);
            item.currentStackCount = stack - qty;
            playerModel.PlaceItem(item, playerOldX, playerOldY);

            var baseTarget = lootModel.Get(lootX, lootY);
            if (baseTarget != null && baseTarget.Data == item.Data && item.Data.maxStackSize > 1)
            {
                if (!isHost || isOffline) baseTarget.currentStackCount += qty;
                lootModel.Items.Invoke();
            }
            else
            {
                var placed = new ItemInstance(item.Data, qty);
                if (!isHost || isOffline)
                {
                    lootModel.PlaceItem(placed, lootX, lootY);
                    lootModel.Items.Invoke();
                }
            }

            playerModel.Items.Invoke();

            if (!isOffline && currentNetworkSync.Runner != null)
                currentNetworkSync.Rpc_RequestPutItem(currentNetworkSync.Runner.LocalPlayer, currentStorageId,
                    item.Data.itemID, sourceInventorySlotIndex, lootSlotIndex, qty, (int)item.currentRotation);

            SubmitSnapshot(force: true);
        }

        private void TryPromptCrossTakeSplit(ItemInstance item, GridSlot playerSlot)
        {
            var playerModel = GridInventoryClass.Instance.Controller.Model;
            var lootModel = lootInventoryController.Model;

            var (lx, ly) = lootModel.GetItemAnchorPosition(item);
            if (lx == -1) return;

            var targetCoords = playerModel.GetCoordinates(playerSlot.Index);
            var baseTarget = playerModel.Get(targetCoords.x, targetCoords.y);

            bool isSwap = baseTarget != null && baseTarget.Data != item.Data;
            if (isSwap) return;

            int maxQty;
            if (baseTarget == null)
            {
                var probe = new ItemInstance(item.Data, 1);
                if (!playerModel.CanPlaceItem(probe, targetCoords.x, targetCoords.y)) return;
                maxQty = item.currentStackCount - 1;
            }
            else if (baseTarget.Data == item.Data && item.Data.maxStackSize > 1)
            {
                int space = baseTarget.Data.maxStackSize - baseTarget.currentStackCount;
                if (space <= 0) return;
                maxQty = Mathf.Min(item.currentStackCount - 1, space);
            }
            else
            {
                return;
            }

            int lootSourceIndex = lootModel.GetIndex(lx, ly);
            int targetInvIndex = playerSlot.Index;

            QuantityPopupView.Show(maxQty,
                qty => ApplyCrossTakeSplitPredicted(item, qty, lx, ly, targetCoords.x, targetCoords.y,
                    lootSourceIndex, targetInvIndex));
        }

        private void ApplyCrossTakeSplitPredicted(ItemInstance item, int qty, int lootOldX, int lootOldY, int px, int py,
            int lootSourceIndex, int targetInventorySlotIndex)
        {
            bool isHost = currentNetworkSync.HasStateAuthority;
            bool isOffline = AuthSession.IsOffline;

            var playerModel = GridInventoryClass.Instance.Controller.Model;
            var lootModel = lootInventoryController.Model;

            int stack = item.currentStackCount;
            if (qty < 1 || qty >= stack) return;

            var baseTarget = playerModel.Get(px, py);

            if (baseTarget != null && baseTarget.Data == item.Data && item.Data.maxStackSize > 1)
            {
                if (!isHost || isOffline) baseTarget.currentStackCount += qty;
                playerModel.Items.Invoke();

                lootModel.TryRemove(item);
                item.currentStackCount = stack - qty;
                lootModel.PlaceItem(item, lootOldX, lootOldY);
                lootModel.Items.Invoke();
            }
            else if (baseTarget == null)
            {
                var placed = new ItemInstance(item.Data, qty);
                if (!playerModel.CanPlaceItem(placed, px, py)) return;

                if (!isHost || isOffline) playerModel.PlaceItem(placed, px, py);
                playerModel.Items.Invoke();

                lootModel.TryRemove(item);
                item.currentStackCount = stack - qty;
                lootModel.PlaceItem(item, lootOldX, lootOldY);
                lootModel.Items.Invoke();
            }
            else return;

            if (!isOffline && currentNetworkSync.Runner != null)
                currentNetworkSync.Rpc_RequestTakeItem(currentNetworkSync.Runner.LocalPlayer, currentStorageId,
                    lootSourceIndex, targetInventorySlotIndex, qty);

            SubmitSnapshot(force: true);
        }

        private void TryPromptLootSplitToQuickslot(ItemInstance item, int quickslotIndex)
        {
            if (lootInventoryController == null || currentNetworkSync == null)
                return;

            var lootModel = lootInventoryController.Model;
            var (lx, ly) = lootModel.GetItemAnchorPosition(item);
            if (lx == -1) return;

            var qs = QuickslotUIController.Instance;
            if (qs == null) return;

            ItemInstance qsTarget = qs.GetItem(quickslotIndex);
            int stack = item.currentStackCount;

            int maxQty;
            if (qsTarget == null)
            {
                maxQty = stack - 1;
            }
            else if (qsTarget.Data == item.Data && item.Data.maxStackSize > 1)
            {
                int space = qsTarget.Data.maxStackSize - qsTarget.currentStackCount;
                if (space <= 0) return;
                maxQty = Mathf.Min(stack - 1, space);
            }
            else
            {
                return;
            }

            if (maxQty < 1) return;

            int lootSourceIndex = lootModel.GetIndex(lx, ly);
            QuantityPopupView.Show(maxQty,
                qty => ApplyLootSplitToQuickslotPredicted(item, quickslotIndex, qty, lx, ly, lootSourceIndex));
        }

        private void ApplyLootSplitToQuickslotPredicted(ItemInstance item, int quickslotIndex, int qty, int lootOldX,
            int lootOldY, int lootSourceIndex)
        {
            bool isHost = currentNetworkSync.HasStateAuthority;
            bool isOffline = AuthSession.IsOffline;

            var lootModel = lootInventoryController.Model;
            var qs = QuickslotUIController.Instance;
            if (qs == null || currentNetworkSync == null)
                return;

            int stack = item.currentStackCount;
            if (qty < 1 || qty >= stack) return;

            ItemInstance qsExisting = qs.GetItem(quickslotIndex);

            lootModel.TryRemove(item);
            item.currentStackCount = stack - qty;
            lootModel.PlaceItem(item, lootOldX, lootOldY);
            lootModel.Items.Invoke();

            if (qsExisting != null && qsExisting.Data == item.Data && item.Data.maxStackSize > 1)
            {
                if (!isHost || isOffline)
                    qsExisting.currentStackCount += qty;

                qs.RefreshSlotVisual(quickslotIndex);
            }
            else if (qsExisting == null)
            {
                var moved = new ItemInstance(item.Data, qty);
                moved.currentRotation = ItemRotation.Deg0;

                if (!isHost || isOffline)
                    qs.SetItemInSlot(quickslotIndex, moved);

                qs.RefreshSlotVisual(quickslotIndex);
            }
            else
            {
                lootModel.TryRemove(item);
                item.currentStackCount = stack;
                lootModel.PlaceItem(item, lootOldX, lootOldY);
                lootModel.Items.Invoke();
                return;
            }

            if (!isOffline && currentNetworkSync.Runner != null)
                currentNetworkSync.Rpc_RequestTakeItem(currentNetworkSync.Runner.LocalPlayer, currentStorageId,
                    lootSourceIndex, 0, qty);

            SubmitSnapshot(force: true);
        }



        private void UpdateCapacities()
        {
            if (lootInventoryController == null || GridInventoryClass.Instance == null) return;
            // Optional: calculate actual used slots and max slots
            SubmitSnapshot(force: true);
        }

        private void SubmitSnapshot(bool force = false)
        {
            if (currentNetworkSync == null || currentStorageId == null || lootInventoryController == null) return;
            if (!currentNetworkSync.HasStateAuthority && !AuthSession.IsOffline) return;

            if (!force && Time.time - lastSnapshotTime < SnapshotCooldown)
            {
                snapshotPending = true;
                return;
            }

            snapshotPending = false;
            lastSnapshotTime = Time.time;

            currentNetworkSync.SubmitLootSnapshot(currentStorageId, 
                LootGridSerializer.ToSaveData(currentStorageId, lootInventoryController.Model));
        }
    }

    // Dummy View to reuse GridInventory logic for the Loot Box
    public class DummyLootGridView : GridStorageView
    {
        public void Init(UnityEngine.UIElements.ScrollView targetScrollView, int cols, UnityEngine.UIElements.VisualElement sharedGhostIcon,
            Systems.GridInventory.DragSource sourceType = Systems.GridInventory.DragSource.Loot)
        {
            this.currentColumns = cols;
            this.ModelColumns = cols;
            this.SourceType = sourceType;
            this.container = targetScrollView;
            this.itemsContainer = targetScrollView.Q<UnityEngine.UIElements.VisualElement>("lootSlotsContainer");
            
            // DummyLootGridView는 InitializeView를 돌려서 슬롯들을 생성하므로,
            // 이 시점에서는 itemsContainer의 부모(slotsContainer)가 세팅되어야 SetRotated가 제대로 먹힙니다.
            // 위에서 itemsContainer에 "slotsContainer" 클래스가 있으니 그것을 활용합니다.
            
            GhostIcon = sharedGhostIcon;
        }

        public override System.Collections.IEnumerator InitializeView(int size = 20)
        {
            Slots = new GridSlot[size];
            itemsContainer.Clear();
            
            float containerPaddingTotal = 20f; // 10f left/top + 10f right/bottom padding

            itemsContainer.style.flexDirection = UnityEngine.UIElements.FlexDirection.Row;
            itemsContainer.style.flexWrap = UnityEngine.UIElements.Wrap.Wrap;
            itemsContainer.style.width = (currentColumns * SlotTotalSize) + containerPaddingTotal + 4f;
            int rowsForUpdate = Mathf.CeilToInt((float)size / currentColumns);
            itemsContainer.style.height = (rowsForUpdate * SlotTotalSize) + containerPaddingTotal + 4f;

            for (int i = 0; i < size; i++)
            {
                var slot = new GridSlot();
                slot.name = "slot";
                slot.AddToClassList("slot");
                itemsContainer.Add(slot);
                Slots[i] = slot;
            }
            yield return null;
        }


        public void BindItemDirect(GridItemView itemView, int slotIndex)
        {
            BindItem(itemView, slotIndex);
        }
    }
}
