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

        [SerializeField] private LootView lootView;

        // Dummy view for the loot grid so GridInventoryController can work with it
        private DummyLootGridView dummyLootGridView;
        private GridInventoryController lootInventoryController;
        private InteractableLoot currentLootSource;
        private string currentStorageId;
        private LootNetworkSync currentNetworkSync;

        private UnityEngine.UIElements.VisualElement originalInventoryParent;

        private UnityEngine.UIElements.VisualElement originalInventoryGhostIcon;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private float lastSnapshotTime;
        private const float SnapshotCooldown = 0.1f;
        private bool snapshotPending;

        private void Start() 
        {
            lootView.Initialize();

            lootView.OnCloseClicked += CloseLoot;

            GridItemView.OnItemDroppedGlobal -= HandleItemDroppedGlobal;
            GridItemView.OnItemDroppedGlobal += HandleItemDroppedGlobal;

            GridItemView.OnItemSplitDroppedGlobal -= HandleItemSplitDroppedGlobal;
            GridItemView.OnItemSplitDroppedGlobal += HandleItemSplitDroppedGlobal;

            QuickslotUIController.OnItemDroppedGlobal -= HandleQuickslotItemDropped;
            QuickslotUIController.OnItemDroppedGlobal += HandleQuickslotItemDropped;

            QuickslotUIController.OnQuickslotSplitRequested -= HandleQuickslotSplitRequestedToLoot;
            QuickslotUIController.OnQuickslotSplitRequested += HandleQuickslotSplitRequestedToLoot;
        }

        private void OnDestroy()
        {
            GridItemView.OnItemSplitDroppedGlobal -= HandleItemSplitDroppedGlobal;
            GridItemView.OnItemDroppedGlobal -= HandleItemDroppedGlobal;

            QuickslotUIController.OnItemDroppedGlobal -= HandleQuickslotItemDropped;
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
            if (PlayerNetworkSetup.IsOfflineTestMode)
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

        private void OpenLoot(InteractableLoot source, string storageId, string title, LootNetworkSync networkSync)
        {
            if (IsOpen) return;
            IsOpen = true;
            openedTime = Time.time;
            currentLootSource = source;
            currentStorageId = storageId;
            currentNetworkSync = networkSync;

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

            lootInventoryController.OnRequestInternalMove = HandleLootInternalMove;

            lootInventoryController.Model.OnModelChanged -= HandleModelChanged;
            lootInventoryController.Model.OnModelChanged += HandleModelChanged;

            lootView.SetLootHeader(title);
            lootView.Show();

            // 3. Move Player Inventory to Right Panel
            AttachPlayerInventory();

            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
            
            PlayerInputHandler playerInput = FindAnyObjectByType<PlayerInputHandler>();
            if (playerInput != null) playerInput.SetInputActive(false);
            
            UpdateCapacities();
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

            if (currentNetworkSync != null && !PlayerNetworkSetup.IsOfflineTestMode && currentNetworkSync.Runner != null)
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
            lootView.Hide();

            if (dummyLootGridView != null)
            {
                Destroy(dummyLootGridView);
            }
            lootInventoryController = null;
            currentLootSource = null;
            currentStorageId = null;
            currentNetworkSync = null;

            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            UnityEngine.Cursor.visible = false;
            
            PlayerInputHandler playerInput = FindAnyObjectByType<PlayerInputHandler>();
            if (playerInput != null) playerInput.SetInputActive(true);
        }

        private void Update()
        {
            if (IsOpen && Time.time - openedTime > 0.1f && UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame)
            {
                CloseLoot();
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
            if (GridInventoryView.Instance != null && GridInventoryView.Instance.Container != null)
            {
                var invContainer = GridInventoryView.Instance.Container;
                invContainer.style.display = UnityEngine.UIElements.DisplayStyle.Flex;
                
                var inventoryWindow = invContainer.Q<UnityEngine.UIElements.VisualElement>("inventory-window");
                if (inventoryWindow != null) inventoryWindow.style.display = UnityEngine.UIElements.DisplayStyle.None;

                var gridStyleSheet = GridInventoryView.Instance.GridStyleSheet;
                if (gridStyleSheet != null && !lootView.GetRootVisualElement().styleSheets.Contains(gridStyleSheet))
                {
                    lootView.GetRootVisualElement().styleSheets.Add(gridStyleSheet);
                }

                var scrollView = invContainer.Q<UnityEngine.UIElements.ScrollView>(className: "slots-scroll-view");
                if (scrollView != null)
                {
                    originalInventoryParent = scrollView.parent;
                    lootView.GetPlayerInventoryContainer().Add(scrollView);
                    
                    scrollView.style.flexGrow = 1;
                    scrollView.style.width = new UnityEngine.UIElements.StyleLength(UnityEngine.UIElements.StyleKeyword.Auto);
                    scrollView.style.height = new UnityEngine.UIElements.StyleLength(UnityEngine.UIElements.StyleKeyword.Auto);
                    scrollView.style.display = UnityEngine.UIElements.DisplayStyle.Flex;

                    var innerSlotsContainer = scrollView.Q<UnityEngine.UIElements.VisualElement>(name: "slotsContainer");
                    if (innerSlotsContainer != null)
                    {
                        // Removed alignSelf = Center to prevent left-side clipping when content is large
                        innerSlotsContainer.style.alignSelf = UnityEngine.UIElements.StyleKeyword.Null;
                        innerSlotsContainer.style.marginTop = 12f;
                        innerSlotsContainer.style.marginBottom = 12f;
                    }
                }
            }
        }

        private void DetachPlayerInventory()
        {
            if (GridInventoryView.Instance != null && GridInventoryView.Instance.Container != null)
            {
                var invContainer = GridInventoryView.Instance.Container;
                invContainer.style.display = UnityEngine.UIElements.DisplayStyle.None;
                
                var inventoryWindow = invContainer.Q<UnityEngine.UIElements.VisualElement>("inventory-window");
                if (inventoryWindow != null) inventoryWindow.style.display = UnityEngine.UIElements.DisplayStyle.Flex;

                var scrollView = lootView.GetPlayerInventoryContainer().Q<UnityEngine.UIElements.ScrollView>(className: "slots-scroll-view");
                if (scrollView != null && originalInventoryParent != null)
                {
                    scrollView.style.flexGrow = UnityEngine.UIElements.StyleKeyword.Null;
                    scrollView.style.width = UnityEngine.UIElements.StyleKeyword.Null;
                    scrollView.style.height = UnityEngine.UIElements.StyleKeyword.Null;
                    scrollView.style.alignSelf = UnityEngine.UIElements.StyleKeyword.Null;
                    
                    var innerSlotsContainer = scrollView.Q<UnityEngine.UIElements.VisualElement>(name: "slotsContainer");
                    if (innerSlotsContainer != null)
                    {
                        innerSlotsContainer.style.alignSelf = UnityEngine.UIElements.StyleKeyword.Null;
                        innerSlotsContainer.style.marginTop = UnityEngine.UIElements.StyleKeyword.Null;
                        innerSlotsContainer.style.marginBottom = UnityEngine.UIElements.StyleKeyword.Null;
                    }

                    originalInventoryParent.Add(scrollView);
                }
                originalInventoryParent = null;

                var gridStyleSheet = GridInventoryView.Instance.GridStyleSheet;
                if (gridStyleSheet != null && lootView.GetRootVisualElement().styleSheets.Contains(gridStyleSheet))
                {
                    lootView.GetRootVisualElement().styleSheets.Remove(gridStyleSheet);
                }
            }
        }


        private void HandleQuickslotItemDropped(ItemInstance item, int sourceQuickslotIndex, Vector2 screenPosition)
        {
            if (!IsOpen || dummyLootGridView == null || lootInventoryController == null || currentNetworkSync == null) return;

            var lootSlot = dummyLootGridView.GetGridSlotAtPosition(screenPosition);
            if (lootSlot == null) return;

            var lootModel = lootInventoryController.Model;
            var targetCoords = lootModel.GetCoordinates(lootSlot.Index);
            var baseTargetItem = lootModel.Get(targetCoords.x, targetCoords.y);

            bool isHost = currentNetworkSync.HasStateAuthority;
            bool isOffline = PlayerNetworkSetup.IsOfflineTestMode;

            if (baseTargetItem != null && baseTargetItem.Data == item.Data && item.Data.maxStackSize > 1)
            {
                // 스택 병합
                int total = item.currentStackCount + baseTargetItem.currentStackCount;
                int moveQty = 0;
                if (total <= item.Data.maxStackSize)
                {
                    moveQty = item.currentStackCount;
                    QuickslotUIController.Instance.RemoveItemFromSlot(sourceQuickslotIndex);
                    if (!isHost || isOffline)
                    {
                        baseTargetItem.currentStackCount = total;
                        lootModel.Items.Invoke();
                    }
                }
                else
                {
                    moveQty = item.Data.maxStackSize - baseTargetItem.currentStackCount;
                    item.currentStackCount = total - item.Data.maxStackSize;
                    QuickslotUIController.Instance.RefreshSlotVisual(sourceQuickslotIndex);
                    if (!isHost || isOffline)
                    {
                        baseTargetItem.currentStackCount = item.Data.maxStackSize;
                        lootModel.Items.Invoke();
                    }
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
                    QuickslotUIController.Instance.RemoveItemFromSlot(sourceQuickslotIndex);
                if (!isHost || isOffline)
                {
                    lootModel.PlaceItem(item, targetCoords.x, targetCoords.y);
                    lootModel.Items.Invoke();
                }

                if (!isOffline && currentNetworkSync.Runner != null)
                    currentNetworkSync.Rpc_RequestPutItem(currentNetworkSync.Runner.LocalPlayer, currentStorageId,
                        item.Data.itemID, 0, lootSlot.Index, item.currentStackCount, (int)item.currentRotation);
                SubmitSnapshot(force: true);
                }
                else
                {
                    QuickslotUIController.Instance.RefreshSlotVisual(sourceQuickslotIndex);
                }
            }
            else
            {
                // 1:1 스왑
                var baseTargetPos = lootModel.GetItemAnchorPosition(baseTargetItem);
                lootModel.TryRemove(baseTargetItem);

                if (lootModel.CanPlaceItem(item, targetCoords.x, targetCoords.y))
                {
                    QuickslotUIController.Instance.RemoveItemFromSlot(sourceQuickslotIndex);
                    baseTargetItem.currentRotation = ItemRotation.Deg0;
                    QuickslotUIController.Instance.SetItemInSlot(sourceQuickslotIndex, baseTargetItem);

                    if (!isHost || isOffline)
                    {
                        lootModel.PlaceItem(item, targetCoords.x, targetCoords.y);
                        lootModel.Items.Invoke();
                    }
                    else
                    {
                        // 호스트는 RPC에서 처리하므로 원상복구
                        lootModel.PlaceItem(baseTargetItem, baseTargetPos.x, baseTargetPos.y);
                    }

                    // Rpc_RequestPutItem은 스왑도 처리함 (서버에서 existingItem이 있고 다르면 스왑으로 처리됨)
                    if (!isOffline && currentNetworkSync.Runner != null)
                        currentNetworkSync.Rpc_RequestPutItem(currentNetworkSync.Runner.LocalPlayer, currentStorageId,
                            item.Data.itemID, 0, lootSlot.Index, item.currentStackCount, (int)item.currentRotation);
                    SubmitSnapshot(force: true);
                }
                else
                {
                    lootModel.PlaceItem(baseTargetItem, baseTargetPos.x, baseTargetPos.y);
                    QuickslotUIController.Instance.RefreshSlotVisual(sourceQuickslotIndex);
                }
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
            var baseTargetItem = lootModel.Get(targetCoords.x, targetCoords.y);

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
            var baseTargetItem = lootModel.Get(targetCoords.x, targetCoords.y);

            bool isHost = currentNetworkSync.HasStateAuthority;
            bool isOffline = PlayerNetworkSetup.IsOfflineTestMode;

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
            GridSlot playerSlot = GridInventoryView.Instance.GetGridSlotAtPosition(screenPos);

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
            bool isOffline = PlayerNetworkSetup.IsOfflineTestMode;

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
            bool isOffline = PlayerNetworkSetup.IsOfflineTestMode;

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
            bool isOffline = PlayerNetworkSetup.IsOfflineTestMode;

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

        private void HandleLootInternalMove(ItemInstance item, int oldX, int oldY, int newX, int newY, int newRotation)
        {
            if (currentNetworkSync == null || currentStorageId == null) return;
            
            int oldIndex = lootInventoryController.Model.GetIndex(oldX, oldY);
            int newIndex = lootInventoryController.Model.GetIndex(newX, newY);
            
            if (!PlayerNetworkSetup.IsOfflineTestMode && currentNetworkSync.Runner != null)
                currentNetworkSync.Rpc_RequestMoveItem(currentNetworkSync.Runner.LocalPlayer, currentStorageId, oldIndex, newIndex, newRotation);
        }

        private void HandleItemDroppedGlobal(GridItemView itemView, Vector2 screenPos)
        {
            if (!IsOpen) return;
            
            ItemInstance item = itemView.ItemInst;
            
            // Determine if dropped on Loot view or Player view
            var lootSlot = dummyLootGridView.GetGridSlotAtPosition(screenPos);
            var playerSlot = GridInventoryView.Instance.GetGridSlotAtPosition(screenPos);

            GridInventoryModel sourceModel = null;
            if (lootInventoryController.Model.GetItemAnchorPosition(item).x != -1) sourceModel = lootInventoryController.Model;
            else if (GridInventoryClass.Instance.Controller.Model.GetItemAnchorPosition(item).x != -1) sourceModel = GridInventoryClass.Instance.Controller.Model;

            if (sourceModel == null) return;

            GridInventoryModel targetModel = null;
            int targetX = -1, targetY = -1;
            int targetIndex = -1;

            if (lootSlot != null && sourceModel != lootInventoryController.Model)
            {
                targetModel = lootInventoryController.Model;
                var coords = targetModel.GetCoordinates(lootSlot.Index);
                targetX = coords.x; targetY = coords.y;
                targetIndex = lootSlot.Index;
            }
            else if (playerSlot != null && sourceModel != GridInventoryClass.Instance.Controller.Model)
            {
                targetModel = GridInventoryClass.Instance.Controller.Model;
                var coords = targetModel.GetCoordinates(playerSlot.Index);
                targetX = coords.x; targetY = coords.y;
                targetIndex = playerSlot.Index;
            }

            if (targetModel != null)
            {
                int newRot = (int)item.currentRotation;
                var sourcePos = sourceModel.GetItemAnchorPosition(item);
                int sourceIndex = sourceModel.GetIndex(sourcePos.x, sourcePos.y);
                int originalQuantity = item.currentStackCount;

                bool isHost = currentNetworkSync != null && currentNetworkSync.HasStateAuthority;
                bool isOffline = PlayerNetworkSetup.IsOfflineTestMode;

                var baseTargetItem = targetModel.Get(targetX, targetY);
                bool isSwap = baseTargetItem != null && baseTargetItem.Data != item.Data;
                string swapItemId = isSwap ? baseTargetItem.Data.itemID : string.Empty;
                int swapQuantity = isSwap ? baseTargetItem.currentStackCount : 0;

                // 로컬 즉시 적용 (플레이어 인벤토리 및 클라이언트 예측)
                // 호스트의 경우, Loot 상자 모델은 RPC에서 업데이트하므로 여기서는 플레이어 인벤토리만 업데이트해야 합니다.
                // 클라이언트의 경우, 렉 방지를 위해 Loot 상자 모델도 임시로 업데이트합니다 (이후 서버 스냅샷으로 덮어씌워짐).
                
                if (targetModel == lootInventoryController.Model)
                {
                    // Player -> Loot (Put)
                    if (isSwap)
                    {
                        // 스왑
                        var bOldPos = targetModel.GetItemAnchorPosition(baseTargetItem);
                        targetModel.TryRemove(baseTargetItem);
                        
                        if (targetModel.CanPlaceItem(item, targetX, targetY) && sourceModel.CanPlaceItem(baseTargetItem, sourcePos.x, sourcePos.y))
                        {
                            sourceModel.TryRemove(item);
                            sourceModel.PlaceItem(baseTargetItem, sourcePos.x, sourcePos.y); // 스왑된 아이템을 인벤토리에 넣음
                            
                            if (!isHost || isOffline)
                            {
                                targetModel.PlaceItem(item, targetX, targetY);
                            }
                            else
                            {
                                // 호스트는 RPC에서 처리할 것이므로 다시 원상복구 (Loot 상자만)
                                targetModel.PlaceItem(baseTargetItem, bOldPos.x, bOldPos.y);
                            }
                        }
                        else
                        {
                            targetModel.PlaceItem(baseTargetItem, bOldPos.x, bOldPos.y);
                            itemView.RevertRotation(itemView.OriginalRotation);
                            return; // 실패
                        }
                    }
                    else
                    {
                        // 단순 이동 또는 병합
                        if (baseTargetItem != null && baseTargetItem.Data == item.Data && item.Data.maxStackSize > 1)
                        {
                            int total = item.currentStackCount + baseTargetItem.currentStackCount;
                            if (total <= item.Data.maxStackSize)
                            {
                                sourceModel.TryRemove(item);
                                if (!isHost || isOffline)
                                {
                                    baseTargetItem.currentStackCount = total;
                                    targetModel.Items.Invoke();
                                }
                            }
                            else
                            {
                                item.currentStackCount = total - item.Data.maxStackSize;
                                sourceModel.Items.Invoke();
                                if (!isHost || isOffline)
                                {
                                    baseTargetItem.currentStackCount = item.Data.maxStackSize;
                                    targetModel.Items.Invoke();
                                }
                            }
                        }
                        else
                        {
                            if (targetModel.CanPlaceItem(item, targetX, targetY))
                            {
                                sourceModel.TryRemove(item);
                                if (!isHost || isOffline)
                                {
                                    targetModel.PlaceItem(item, targetX, targetY);
                                }
                            }
                            else
                            {
                                itemView.RevertRotation(itemView.OriginalRotation);
                                return; // 실패
                            }
                        }
                    }
                }
                else if (targetModel == GridInventoryClass.Instance.Controller.Model)
                {
                    // Loot -> Player (Take)
                    if (isSwap)
                    {
                        // 스왑
                        var bOldPos = targetModel.GetItemAnchorPosition(baseTargetItem);
                        targetModel.TryRemove(baseTargetItem);
                        
                        if (targetModel.CanPlaceItem(item, targetX, targetY) && sourceModel.CanPlaceItem(baseTargetItem, sourcePos.x, sourcePos.y))
                        {
                            targetModel.PlaceItem(item, targetX, targetY); // 가져온 아이템을 인벤토리에 넣음
                            
                            if (!isHost || isOffline)
                            {
                                sourceModel.TryRemove(item);
                                sourceModel.PlaceItem(baseTargetItem, sourcePos.x, sourcePos.y);
                            }
                            else
                            {
                                // 호스트는 RPC에서 처리할 것이므로 Loot 상자는 원상복구 안 해도 됨 (어차피 안 건드렸음)
                            }
                        }
                        else
                        {
                            targetModel.PlaceItem(baseTargetItem, bOldPos.x, bOldPos.y);
                            itemView.RevertRotation(itemView.OriginalRotation);
                            return; // 실패
                        }
                    }
                    else
                    {
                        // 단순 이동 또는 병합
                        if (baseTargetItem != null && baseTargetItem.Data == item.Data && item.Data.maxStackSize > 1)
                        {
                            int total = item.currentStackCount + baseTargetItem.currentStackCount;
                            if (total <= item.Data.maxStackSize)
                            {
                                targetModel.TryRemove(baseTargetItem); // 기존 템 지우고 (스택 합치기 위해)
                                baseTargetItem.currentStackCount = total;
                                targetModel.PlaceItem(baseTargetItem, targetX, targetY);
                                
                                if (!isHost || isOffline)
                                {
                                    sourceModel.TryRemove(item);
                                }
                            }
                            else
                            {
                                targetModel.TryRemove(baseTargetItem);
                                baseTargetItem.currentStackCount = item.Data.maxStackSize;
                                targetModel.PlaceItem(baseTargetItem, targetX, targetY);
                                
                                if (!isHost || isOffline)
                                {
                                    item.currentStackCount = total - item.Data.maxStackSize;
                                    sourceModel.Items.Invoke();
                                }
                            }
                        }
                        else
                        {
                            if (targetModel.CanPlaceItem(item, targetX, targetY))
                            {
                                targetModel.PlaceItem(item, targetX, targetY);
                                if (!isHost || isOffline)
                                {
                                    sourceModel.TryRemove(item);
                                }
                            }
                            else
                            {
                                itemView.RevertRotation(itemView.OriginalRotation);
                                return; // 실패
                            }
                        }
                    }
                }

                // 서버 통보 (RPC)
                if (!isOffline && currentNetworkSync.Runner != null)
                {
                    if (targetModel == lootInventoryController.Model)
                    {
                        // Player -> Loot (Put)
                        // Put의 경우 서버가 상자 모델을 관리하므로 originalQuantity를 그대로 보내면 서버가 알아서 병합/나머지 처리를 합니다.
                        if (isSwap) {
                            currentNetworkSync.Rpc_RequestPutItemWithSwap(currentNetworkSync.Runner.LocalPlayer, currentStorageId, item.Data.itemID, sourceIndex, targetIndex, originalQuantity, newRot, swapItemId, swapQuantity);
                        } else {
                            currentNetworkSync.Rpc_RequestPutItem(currentNetworkSync.Runner.LocalPlayer, currentStorageId, item.Data.itemID, sourceIndex, targetIndex, originalQuantity, newRot);
                        }
                    }
                    else if (targetModel == GridInventoryClass.Instance.Controller.Model)
                    {
                        // Loot -> Player (Take)
                        // Take의 경우 플레이어 인벤토리 공간에 따라 실제 가져간 양이 달라질 수 있으므로 이를 계산해서 보냅니다.
                        int actualTakeQuantity = originalQuantity;
                        if (!isSwap && baseTargetItem != null && baseTargetItem.Data == item.Data && item.Data.maxStackSize > 1)
                        {
                            int spaceLeft = item.Data.maxStackSize - baseTargetItem.currentStackCount;
                            actualTakeQuantity = Mathf.Min(originalQuantity, spaceLeft);
                        }

                        if (isSwap) {
                            currentNetworkSync.Rpc_RequestTakeItemWithSwap(currentNetworkSync.Runner.LocalPlayer, currentStorageId, swapItemId, sourceIndex, targetIndex, actualTakeQuantity, swapQuantity);
                        } else {
                            currentNetworkSync.Rpc_RequestTakeItem(currentNetworkSync.Runner.LocalPlayer, currentStorageId, sourceIndex, targetIndex, actualTakeQuantity);
                        }
                    }
                }
            }
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
            if (!currentNetworkSync.HasStateAuthority && !PlayerNetworkSetup.IsOfflineTestMode) return;

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
        public void Init(UnityEngine.UIElements.ScrollView targetScrollView, int cols, UnityEngine.UIElements.VisualElement sharedGhostIcon)
        {
            this.currentColumns = cols;
            this.container = targetScrollView;
            this.itemsContainer = targetScrollView.Q<UnityEngine.UIElements.VisualElement>("lootSlotsContainer");
            
            var existingSlots = itemsContainer.Query<GridSlot>().ToList();
            Slots = existingSlots.ToArray();
            
            GhostIcon = sharedGhostIcon;
        }

        public override System.Collections.IEnumerator InitializeView(int size = 20)
        {
            Slots = new GridSlot[size];
            itemsContainer.Clear();
            
            float containerPaddingTotal = 20f; // 10f left/top + 10f right/bottom padding
            // 패딩과 여백(4f)을 추가하여 정확히 슬롯들이 다음 줄로 밀리지(wrap) 않도록 보장
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