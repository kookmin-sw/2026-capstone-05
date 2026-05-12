using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using Systems.GridInventory;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Systems.StorageSystem
{
    public class StorageUI : GridStorageView
    {
        [SerializeField] private string panelName = "Storage";
        private const int DefaultStorageColumns = 9;
        private const int DefaultStorageRows = 18;
        private const float StorageWindowWidth = 980f;

        public static StorageUI ActiveInstance { get; private set; }

        public bool IsOpen => container != null && container.style.display != DisplayStyle.None;

        private StorageNetworkSync networkSync;
        private GridInventoryModel model;
        private string currentStorageId;
        private readonly Dictionary<ItemInstance, GridItemView> itemViews = new Dictionary<ItemInstance, GridItemView>();
        private PlayerInputHandler localPlayerInputHandler;
        private bool eventsBound;
        private bool openedInventoryForStorage;
        private bool inventoryDragEventsBound;
        private bool inventoryDragInProgress;
        private bool pendingRefreshAfterDrag;
        private readonly Dictionary<int, PendingStorageOperation> pendingOperations = new Dictionary<int, PendingStorageOperation>();

        private enum PendingStorageOperationType
        {
            RemoveInventorySource,
            AddToInventory,
            AddToQuickslot,
            RemoveQuickslotSource
        }

        private struct PendingStorageOperation
        {
            public PendingStorageOperationType Type;
            public ItemInstance SourceItem;
            public int TargetIndex;
            public int SourceSlotIndex;
            public bool OptimisticDestinationApplied;
        }

        private void Awake()
        {
            ActiveInstance = this;
        }

        private void OnDestroy()
        {
            if (ActiveInstance == this)
            {
                ActiveInstance = null;
            }

            UnbindModel();
            UnbindInventoryDragEvents();
            UnbindNetworkSyncEvents();
            OnDrop -= HandleStorageDrop;
            OnDropToQuickslot -= HandleDropToQuickslot;
            OnDragUpdate -= HandleStorageDragUpdate;
            OnDragEndEvent -= HandleAnyDragEnd;
            QuickslotUIController.OnItemDroppedGlobal -= HandleQuickslotItemDropped;
            QuickslotUIController.OnItemDragUpdateGlobal -= HandleQuickslotDragUpdate;
            QuickslotUIController.OnItemDragEndGlobal -= HandleAnyDragEnd;
        }

        private void Start()
        {
            int columns = StorageNetworkSync.Instance != null ? StorageNetworkSync.Instance.StorageWidth : DefaultStorageColumns;
            int rows = StorageNetworkSync.Instance != null ? StorageNetworkSync.Instance.StorageHeight : DefaultStorageRows;
            StartCoroutine(Initialize(columns * rows, columns));
        }

        public void Open(string storageId, StorageNetworkSync sync)
        {
            networkSync = sync != null ? sync : StorageNetworkSync.Instance;
            if (networkSync == null)
            {
                Debug.LogWarning("[StorageUI] StorageNetworkSync is missing.");
                return;
            }

            if (container == null)
            {
                StartCoroutine(OpenWhenReady(storageId));
                return;
            }

            BindModel(storageId, networkSync.GetOrCreateModel(storageId));
            BindNetworkSyncEvents();
            OpenPlayerInventoryBesideStorage();
            BindInventoryDragEvents();
            ApplyStorageLayout();
            container.style.display = DisplayStyle.Flex;
            GridInventoryView.IsAnyInventoryOpen = true;
            SetLocalInputActive(false);
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }

        public void Close()
        {
            if (container != null)
            {
                container.style.display = DisplayStyle.None;
            }

            ResetAllSlotColors();
            UnbindInventoryDragEvents();
            ClosePlayerInventoryOpenedByStorage();
            GridInventoryView.IsAnyInventoryOpen = GridInventoryView.Instance != null && GridInventoryView.Instance.IsOpen;
            SetLocalInputActive(true);
            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            UnityEngine.Cursor.visible = false;
        }

        public override IEnumerator InitializeView(int size = 20)
        {
            while (document == null || document.rootVisualElement == null)
            {
                if (document == null)
                {
                    document = GetComponent<UIDocument>();
                }

                yield return null;
            }

            Slots = new GridSlot[size];
            root = document.rootVisualElement;

            if (document != null)
            {
                document.sortingOrder = 4;
            }

            container = root.Q<VisualElement>(className: "container");
            if (container == null)
            {
                root.Clear();
                if (styleSheet != null)
                {
                    root.styleSheets.Add(styleSheet);
                }

                container = root.CreateChild("container");
                VisualElement window = container.CreateChild("inventory-window");
                window.name = "inventory-window";

                Label header = new Label(panelName.ToUpper());
                header.name = "inventoryHeader";
                window.Add(header);

                VisualElement slotsRoot = window.CreateChild("slotsContainer");
                slotsRoot.name = "slotsContainer";
            }

            VisualElement inventory = container.Q<VisualElement>(name: "inventory-window");
            Label headerLabel = inventory.Q<Label>(name: "inventoryHeader");
            if (headerLabel != null)
            {
                headerLabel.text = panelName.ToUpper();
            }

            VisualElement slotsContainer = inventory.Q<VisualElement>(name: "slotsContainer");
            float containerPaddingTotal = 20f;
            float gridWidth = currentColumns * SlotTotalSize + containerPaddingTotal + 4f;
            slotsContainer.style.width = gridWidth;
            slotsContainer.style.minWidth = gridWidth;
            slotsContainer.style.maxWidth = gridWidth;
            int rows = Mathf.CeilToInt((float)size / currentColumns);
            float gridHeight = rows * SlotTotalSize + containerPaddingTotal + 4f;
            slotsContainer.style.height = gridHeight;
            slotsContainer.style.minHeight = gridHeight;

            ConfigureStorageScrollView(inventory, slotsContainer, gridWidth);

            List<GridSlot> existingSlots = slotsContainer.Query<GridSlot>().ToList();
            for (int i = 0; i < size; i++)
            {
                if (i < existingSlots.Count)
                {
                    Slots[i] = existingSlots[i];
                    continue;
                }

                StorageSlot slot = new StorageSlot();
                slot.name = "slot";
                slot.AddToClassList("slot");
                slotsContainer.Add(slot);
                Slots[i] = slot;
            }

            itemsContainer = slotsContainer.Q<VisualElement>("itemsContainer") ?? slotsContainer.CreateChild("itemsContainer");
            itemsContainer.style.position = Position.Absolute;
            itemsContainer.style.top = 0;
            itemsContainer.style.left = 0;
            itemsContainer.style.right = 0;
            itemsContainer.style.bottom = 0;
            itemsContainer.BringToFront();

            ghostIcon = container.Q<VisualElement>(className: "ghostIcon") ?? container.CreateChild("ghostIcon");
            ghostIcon.AddToClassList("ghostIcon");
            ghostIcon.style.position = Position.Absolute;
            ghostIcon.style.visibility = Visibility.Hidden;
            ghostIcon.pickingMode = PickingMode.Ignore;

            Button closeButton = inventory.Q<Button>(name: "btn-close");
            if (closeButton != null)
            {
                closeButton.RemoveFromHierarchy();
            }

            if (!eventsBound)
            {
                OnDrop += HandleStorageDrop;
                OnDropToQuickslot += HandleDropToQuickslot;
                OnDragUpdate += HandleStorageDragUpdate;
                OnDragEndEvent += HandleAnyDragEnd;
                QuickslotUIController.OnItemDroppedGlobal += HandleQuickslotItemDropped;
                QuickslotUIController.OnItemDragUpdateGlobal += HandleQuickslotDragUpdate;
                QuickslotUIController.OnItemDragEndGlobal += HandleAnyDragEnd;
                eventsBound = true;
            }

            container.style.display = DisplayStyle.None;
            ApplyStorageLayout();
            yield return null;
        }

        protected override void Update()
        {
            base.Update();

            if (!IsOpen || Keyboard.current == null)
            {
                return;
            }

            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Close();
            }
        }

        protected override void ProcessDrop(Vector2 position)
        {
            if (TryMoveDraggedStorageItemToQuickslot(position))
            {
                ResetDragState();
                return;
            }

            if (TryMoveDraggedStorageItemToInventory(position))
            {
                ResetDragState();
                return;
            }

            base.ProcessDrop(position);
        }

        public bool TryAcceptExternalDrop(GridItemView externalItemView, Vector2 screenPosition)
        {
            if (!IsOpen || externalItemView?.ItemInst == null || model == null)
            {
                return false;
            }

            GridSlot targetSlot = GetGridSlotAtPosition(screenPosition);
            if (targetSlot == null)
            {
                return false;
            }

            GridInventoryModel inventoryModel = global::Systems.GridInventory.GridInventory.Instance?.Controller?.Model;
            if (inventoryModel == null)
            {
                return false;
            }

            ItemInstance sourceItem = externalItemView.ItemInst;
            (int oldX, int oldY) = inventoryModel.GetItemAnchorPosition(sourceItem);
            if (oldX < 0 || oldY < 0)
            {
                return false;
            }

            (int targetX, int targetY) = model.GetCoordinates(targetSlot.Index);
            if (!CanPlaceOrStackInModel(model, sourceItem, targetX, targetY))
            {
                return false;
            }

            int requestId = SubmitPut(targetSlot.Index, sourceItem);
            if (requestId == 0)
            {
                return false;
            }

            if (!IsAuthoritativeStorage())
            {
                pendingOperations[requestId] = new PendingStorageOperation
                {
                    Type = PendingStorageOperationType.RemoveInventorySource,
                    SourceItem = sourceItem,
                    SourceSlotIndex = inventoryModel.GetIndex(oldX, oldY),
                    TargetIndex = targetSlot.Index
                };
                return true;
            }

            inventoryModel.TryRemove(sourceItem);
            return true;
        }

        private IEnumerator OpenWhenReady(string storageId)
        {
            while (container == null)
            {
                yield return null;
            }

            Open(storageId, networkSync);
        }

        private void BindModel(string storageId, GridInventoryModel nextModel)
        {
            UnbindModel();
            currentStorageId = storageId;
            model = nextModel;
            model.OnModelChanged += HandleModelChanged;
            RefreshView();
        }

        private void UnbindModel()
        {
            if (model != null)
            {
                model.OnModelChanged -= HandleModelChanged;
            }

            model = null;
            ClearItemViews();
        }

        private void ClearItemViews()
        {
            foreach (GridItemView itemView in itemViews.Values.ToList())
            {
                RemoveItem(itemView);
            }

            itemViews.Clear();
        }

        private void HandleModelChanged(IList<ItemInstance> items)
        {
            if (isDragging || inventoryDragInProgress)
            {
                pendingRefreshAfterDrag = true;
                return;
            }

            RefreshView();
        }

        private void RefreshView()
        {
            if (model == null || itemsContainer == null)
            {
                return;
            }

            HashSet<ItemInstance> currentItems = new HashSet<ItemInstance>();
            for (int i = 0; i < model.Items.Length; i++)
            {
                ItemInstance item = model.Get(i);
                if (item != null)
                {
                    currentItems.Add(item);
                }
            }

            List<ItemInstance> toRemove = new List<ItemInstance>();
            foreach (KeyValuePair<ItemInstance, GridItemView> pair in itemViews)
            {
                if (!currentItems.Contains(pair.Key))
                {
                    RemoveItem(pair.Value);
                    toRemove.Add(pair.Key);
                }
            }

            foreach (ItemInstance item in toRemove)
            {
                itemViews.Remove(item);
            }

            HashSet<ItemInstance> processed = new HashSet<ItemInstance>();
            for (int i = 0; i < model.Items.Length; i++)
            {
                ItemInstance item = model.Get(i);
                if (item == null || processed.Contains(item))
                {
                    continue;
                }

                processed.Add(item);
                (int x, int y) = model.GetItemAnchorPosition(item);
                int anchorIndex = model.GetIndex(x, y);
                if (!itemViews.TryGetValue(item, out GridItemView itemView))
                {
                    itemView = new GridItemView(item);
                    itemViews[item] = itemView;
                    BindItem(itemView, anchorIndex);
                    continue;
                }

                itemView.SetQuantity(item.currentStackCount);
                itemView.RefreshVisuals();
                UpdateItemPosition(itemView, anchorIndex);
                itemView.style.visibility = Visibility.Visible;
                itemView.style.opacity = 1f;
            }

            PruneOrphanItemViews();
        }

        private void PruneOrphanItemViews()
        {
            if (itemsContainer == null)
            {
                return;
            }

            HashSet<GridItemView> trackedViews = new HashSet<GridItemView>(itemViews.Values);
            List<GridItemView> orphanViews = itemsContainer.Children()
                .OfType<GridItemView>()
                .Where(view => !trackedViews.Contains(view))
                .ToList();

            foreach (GridItemView orphanView in orphanViews)
            {
                RemoveItem(orphanView);
            }
        }

        private void HandleStorageDrop(GridItemView itemView, GridSlot targetSlot)
        {
            if (itemView?.ItemInst == null || targetSlot == null || model == null)
            {
                return;
            }

            ItemInstance sourceItem = itemView.ItemInst;
            (int oldX, int oldY) = model.GetItemAnchorPosition(sourceItem);
            if (oldX < 0 || oldY < 0)
            {
                return;
            }

            int oldSlotIndex = model.GetIndex(oldX, oldY);
            (int newX, int newY) = model.GetCoordinates(targetSlot.Index);
            if (!CanPlaceOrStackInModel(model, sourceItem, newX, newY))
            {
                itemView.RevertRotation(itemView.OriginalRotation);
                return;
            }

            int requestId = SubmitMove(oldSlotIndex, targetSlot.Index, sourceItem.currentRotation);
            if (requestId == 0)
            {
                itemView.RevertRotation(itemView.OriginalRotation);
                return;
            }

            if (!IsAuthoritativeStorage())
            {
                MoveItemLocally(model, sourceItem, oldX, oldY, newX, newY, itemView.OriginalRotation);
            }
        }

        private void HandleDropToQuickslot(GridItemView itemView, int quickslotIndex)
        {
            if (itemView?.ItemInst == null || model == null || QuickslotUIController.Instance == null)
            {
                return;
            }

            ItemInstance sourceItem = itemView.ItemInst;
            (int oldX, int oldY) = model.GetItemAnchorPosition(sourceItem);
            if (oldX < 0 || oldY < 0)
            {
                return;
            }

            int oldSlotIndex = model.GetIndex(oldX, oldY);
            ItemInstance targetItem = QuickslotUIController.Instance.GetItem(quickslotIndex);

            if (!IsAuthoritativeStorage())
            {
                if (targetItem != null)
                {
                    bool canStackIntoQuickslot = targetItem.Data == sourceItem.Data &&
                        targetItem.Data.maxStackSize > 1 &&
                        targetItem.currentStackCount + sourceItem.currentStackCount <= targetItem.Data.maxStackSize;
                    if (!canStackIntoQuickslot)
                    {
                        return;
                    }
                }

                int requestId = SubmitRemove(oldSlotIndex, ItemRotation.Deg0);
                if (requestId == 0)
                {
                    return;
                }

                pendingOperations[requestId] = new PendingStorageOperation
                {
                    Type = PendingStorageOperationType.AddToQuickslot,
                    SourceItem = sourceItem,
                    TargetIndex = quickslotIndex,
                    SourceSlotIndex = oldSlotIndex
                };
                return;
            }

            ItemRotation sourceRotation = sourceItem.currentRotation;
            sourceItem.currentRotation = ItemRotation.Deg0;

            if (targetItem != null)
            {
                if (SubmitRemove(oldSlotIndex, ItemRotation.Deg0) == 0)
                {
                    sourceItem.currentRotation = sourceRotation;
                    return;
                }

                QuickslotUIController.Instance.RemoveItemFromSlot(quickslotIndex);
                if (SubmitPut(oldSlotIndex, targetItem) == 0)
                {
                    sourceItem.currentRotation = sourceRotation;
                    SubmitPut(oldSlotIndex, sourceItem);
                    QuickslotUIController.Instance.SetItemInSlot(quickslotIndex, targetItem);
                    return;
                }
            }
            else if (SubmitRemove(oldSlotIndex, ItemRotation.Deg0) == 0)
            {
                sourceItem.currentRotation = sourceRotation;
                return;
            }

            QuickslotUIController.Instance.SetItemInSlot(quickslotIndex, sourceItem);
        }

        private void HandleQuickslotItemDropped(ItemInstance item, int sourceQuickslotIndex, Vector2 screenPosition)
        {
            if (!IsOpen || item == null || QuickslotUIController.Instance == null)
            {
                return;
            }

            GridSlot targetSlot = GetGridSlotAtPosition(screenPosition);
            if (targetSlot == null)
            {
                return;
            }

            GridSlot inventorySlot = GridInventoryView.Instance != null
                ? GridInventoryView.Instance.GetGridSlotAtPosition(screenPosition)
                : null;
            if (inventorySlot != null)
            {
                return;
            }

            (int targetX, int targetY) = model.GetCoordinates(targetSlot.Index);
            if (!CanPlaceOrStackInModel(model, item, targetX, targetY))
            {
                QuickslotUIController.Instance.RefreshSlotVisual(sourceQuickslotIndex);
                return;
            }

            if (!IsAuthoritativeStorage())
            {
                int requestId = SubmitPut(targetSlot.Index, item);
                if (requestId == 0)
                {
                    QuickslotUIController.Instance.RefreshSlotVisual(sourceQuickslotIndex);
                    return;
                }

                pendingOperations[requestId] = new PendingStorageOperation
                {
                    Type = PendingStorageOperationType.RemoveQuickslotSource,
                    SourceItem = item,
                    TargetIndex = sourceQuickslotIndex,
                    SourceSlotIndex = targetSlot.Index
                };
                return;
            }

            int putRequestId = SubmitPut(targetSlot.Index, item);
            if (putRequestId == 0)
            {
                QuickslotUIController.Instance.RefreshSlotVisual(sourceQuickslotIndex);
                return;
            }

            QuickslotUIController.Instance.RemoveItemFromSlot(sourceQuickslotIndex);
        }

        private void HandleQuickslotDragUpdate(ItemInstance item, Vector2 screenPosition)
        {
            if (!IsOpen || item == null)
            {
                return;
            }

            GridSlot targetSlot = GetGridSlotAtPosition(screenPosition);
            if (targetSlot == null)
            {
                ResetAllSlotColors();
                return;
            }

            inventoryDragInProgress = true;
            HighlightIncomingDrop(item, screenPosition);
        }

        private void HandleStorageDragUpdate(GridItemView itemView, Vector2 screenPosition)
        {
            if (!IsOpen || itemView?.ItemInst == null)
            {
                return;
            }

            HighlightIncomingDrop(itemView.ItemInst, screenPosition);
        }

        private void HandleInventoryDragUpdate(GridItemView itemView, Vector2 screenPosition)
        {
            if (!IsOpen || itemView?.ItemInst == null)
            {
                return;
            }

            GridSlot targetSlot = GetGridSlotAtPosition(screenPosition);
            if (targetSlot == null)
            {
                ResetAllSlotColors();
                return;
            }

            inventoryDragInProgress = true;
            HighlightIncomingDrop(itemView.ItemInst, screenPosition);
        }

        private void HandleAnyDragEnd()
        {
            inventoryDragInProgress = false;
            ResetAllSlotColors();

            if (!pendingRefreshAfterDrag)
            {
                return;
            }

            pendingRefreshAfterDrag = false;
            RefreshView();
        }

        private bool TryMoveDraggedStorageItemToQuickslot(Vector2 screenPosition)
        {
            if (QuickslotUIController.Instance == null || draggedItem?.ItemInst == null)
            {
                return false;
            }

            int quickslotIndex = QuickslotUIController.Instance.GetSlotIndexAtPosition(screenPosition);
            if (quickslotIndex < 0)
            {
                return false;
            }

            HandleDropToQuickslot(draggedItem, quickslotIndex);
            return true;
        }

        private bool TryMoveDraggedStorageItemToInventory(Vector2 screenPosition)
        {
            if (draggedItem?.ItemInst == null || GridInventoryView.Instance == null || !GridInventoryView.Instance.isActiveAndEnabled)
            {
                return false;
            }

            GridSlot inventorySlot = GridInventoryView.Instance.GetGridSlotAtPosition(screenPosition);
            if (inventorySlot == null)
            {
                return false;
            }

            GridInventoryModel inventoryModel = global::Systems.GridInventory.GridInventory.Instance?.Controller?.Model;
            if (inventoryModel == null)
            {
                return false;
            }

            ItemInstance sourceItem = draggedItem.ItemInst;
            (int oldX, int oldY) = model.GetItemAnchorPosition(sourceItem);
            if (oldX < 0 || oldY < 0)
            {
                return false;
            }

            int oldSlotIndex = model.GetIndex(oldX, oldY);
            (int targetX, int targetY) = inventoryModel.GetCoordinates(inventorySlot.Index);
            if (!CanPlaceOrStackInModel(inventoryModel, sourceItem, targetX, targetY))
            {
                draggedItem.RevertRotation(draggedItem.OriginalRotation);
                return false;
            }

            if (!IsAuthoritativeStorage())
            {
                int requestId = SubmitRemove(oldSlotIndex, sourceItem.currentRotation);
                if (requestId == 0)
                {
                    return false;
                }

                pendingOperations[requestId] = new PendingStorageOperation
                {
                    Type = PendingStorageOperationType.AddToInventory,
                    SourceItem = sourceItem,
                    TargetIndex = inventorySlot.Index,
                    SourceSlotIndex = oldSlotIndex
                };
                return true;
            }

            if (SubmitRemove(oldSlotIndex, sourceItem.currentRotation) == 0)
            {
                draggedItem.RevertRotation(draggedItem.OriginalRotation);
                return false;
            }

            if (!PlaceOrStackInModel(inventoryModel, sourceItem, targetX, targetY))
            {
                SubmitPut(oldSlotIndex, sourceItem);
                return false;
            }

            return true;
        }

        private static bool MoveItemLocally(GridInventoryModel targetModel, ItemInstance item, int oldX, int oldY, int newX, int newY, ItemRotation fallbackRotation)
        {
            if (targetModel == null || item == null)
            {
                return false;
            }

            targetModel.TryRemove(item);
            if (PlaceOrStackInModel(targetModel, item, newX, newY))
            {
                return true;
            }

            item.currentRotation = fallbackRotation;
            targetModel.PlaceItem(item, oldX, oldY);
            return false;
        }

        private bool TryPlaceIncomingItem(ItemInstance sourceItem, int targetSlotIndex)
        {
            if (model == null || sourceItem == null)
            {
                return false;
            }

            (int x, int y) = model.GetCoordinates(targetSlotIndex);
            return PlaceOrStackInModel(model, sourceItem, x, y);
        }

        private static bool PlaceOrStackInModel(GridInventoryModel targetModel, ItemInstance sourceItem, int x, int y)
        {
            ItemInstance targetItem = targetModel.Get(x, y);
            if (targetItem != null && targetItem != sourceItem && targetItem.Data == sourceItem.Data && targetItem.Data.maxStackSize > 1)
            {
                int total = targetItem.currentStackCount + sourceItem.currentStackCount;
                if (total <= targetItem.Data.maxStackSize)
                {
                    targetItem.currentStackCount = total;
                    targetModel.Items.Invoke();
                    return true;
                }

                return false;
            }

            return targetModel.PlaceItem(sourceItem, x, y);
        }

        private void HighlightIncomingDrop(ItemInstance item, Vector2 screenPosition)
        {
            ResetAllSlotColors();
            GridSlot targetSlot = GetGridSlotAtPosition(screenPosition);
            if (targetSlot == null || model == null)
            {
                return;
            }

            (int x, int y) = model.GetCoordinates(targetSlot.Index);
            bool canPlace = CanPlaceOrStackInModel(model, item, x, y);
            Color color = canPlace ? new Color(0f, 1f, 0f, 0.3f) : new Color(1f, 0f, 0f, 0.3f);
            foreach (Vector2Int pos in item.Data.gridShape.GetRotatedPositions(item.currentRotation))
            {
                int checkX = x + pos.x;
                int checkY = y + pos.y;
                if (checkX >= 0 && checkY >= 0 && checkX < model.Width && checkY < model.Height)
                {
                    SetSlotColor(model.GetIndex(checkX, checkY), color);
                }
            }
        }

        private static bool CanPlaceOrStackInModel(GridInventoryModel targetModel, ItemInstance sourceItem, int x, int y)
        {
            ItemInstance targetItem = targetModel.Get(x, y);
            if (targetItem != null && targetItem != sourceItem && targetItem.Data == sourceItem.Data && targetItem.Data.maxStackSize > 1)
            {
                return targetItem.currentStackCount + sourceItem.currentStackCount <= targetItem.Data.maxStackSize;
            }

            return targetModel.CanPlaceItem(sourceItem, x, y);
        }

        private int SubmitMove(int fromSlotIndex, int toSlotIndex, ItemRotation rotation)
        {
            if (networkSync == null || string.IsNullOrWhiteSpace(currentStorageId))
            {
                return 0;
            }

            return networkSync.SubmitMoveItem(currentStorageId, fromSlotIndex, toSlotIndex, (int)rotation);
        }

        private int SubmitPut(int targetSlotIndex, ItemInstance item)
        {
            if (networkSync == null || string.IsNullOrWhiteSpace(currentStorageId))
            {
                return 0;
            }

            return networkSync.SubmitPutItem(currentStorageId, targetSlotIndex, item);
        }

        private int SubmitRemove(int sourceSlotIndex, ItemRotation? rotation = null)
        {
            if (networkSync == null || string.IsNullOrWhiteSpace(currentStorageId))
            {
                return 0;
            }

            int rotationValue = rotation.HasValue ? (int)rotation.Value : -1;
            return networkSync.SubmitRemoveItem(currentStorageId, sourceSlotIndex, rotationValue);
        }

        private bool IsAuthoritativeStorage()
        {
            return networkSync == null || networkSync.IsAuthoritative;
        }

        private void BindNetworkSyncEvents()
        {
            if (networkSync == null)
            {
                return;
            }

            networkSync.OnStorageOperationConfirmed -= HandleStorageOperationConfirmed;
            networkSync.OnStorageOperationConfirmed += HandleStorageOperationConfirmed;
        }

        private void UnbindNetworkSyncEvents()
        {
            if (networkSync == null)
            {
                return;
            }

            networkSync.OnStorageOperationConfirmed -= HandleStorageOperationConfirmed;
        }

        private void HandleStorageOperationConfirmed(int requestId, bool success, string itemId, int count, int rotation)
        {
            if (!pendingOperations.TryGetValue(requestId, out PendingStorageOperation operation))
            {
                return;
            }

            pendingOperations.Remove(requestId);
            if (!success)
            {
                RollbackOptimisticOperation(operation);
                return;
            }

            switch (operation.Type)
            {
                case PendingStorageOperationType.RemoveInventorySource:
                    if (!operation.OptimisticDestinationApplied)
                    {
                        global::Systems.GridInventory.GridInventory.Instance?.Controller?.Model?.TryRemove(operation.SourceItem);
                    }
                    break;
                case PendingStorageOperationType.RemoveQuickslotSource:
                    if (QuickslotUIController.Instance != null)
                    {
                        QuickslotUIController.Instance.RemoveItemInstance(operation.SourceItem);
                    }
                    break;
                case PendingStorageOperationType.AddToInventory:
                    if (operation.OptimisticDestinationApplied)
                    {
                        break;
                    }

                    if (!AddConfirmedItemToInventory(operation.TargetIndex, itemId, count, rotation))
                    {
                        RestoreConfirmedItemToStorage(operation.SourceSlotIndex, itemId, count, rotation);
                    }
                    break;
                case PendingStorageOperationType.AddToQuickslot:
                    if (operation.OptimisticDestinationApplied)
                    {
                        break;
                    }

                    if (!AddConfirmedItemToQuickslot(operation.TargetIndex, itemId, count, rotation))
                    {
                        RestoreConfirmedItemToStorage(operation.SourceSlotIndex, itemId, count, rotation);
                    }
                    break;
            }
        }

        private void RollbackOptimisticOperation(PendingStorageOperation operation)
        {
            if (!operation.OptimisticDestinationApplied || operation.SourceItem == null)
            {
                return;
            }

            switch (operation.Type)
            {
                case PendingStorageOperationType.RemoveInventorySource:
                    model?.TryRemove(operation.SourceItem);
                    global::Systems.GridInventory.GridInventory.Instance?.Controller?.Model?.TryAdd(operation.SourceItem);
                    break;
                case PendingStorageOperationType.AddToInventory:
                    global::Systems.GridInventory.GridInventory.Instance?.Controller?.Model?.TryRemove(operation.SourceItem);
                    model?.TryAdd(operation.SourceItem);
                    break;
                case PendingStorageOperationType.AddToQuickslot:
                    QuickslotUIController.Instance?.RemoveItemInstance(operation.SourceItem);
                    model?.TryAdd(operation.SourceItem);
                    break;
            }
        }

        private bool AddConfirmedItemToInventory(int targetSlotIndex, string itemId, int count, int rotation)
        {
            GridInventoryModel inventoryModel = global::Systems.GridInventory.GridInventory.Instance?.Controller?.Model;
            ItemInstance item = CreateConfirmedItem(itemId, count, rotation);
            if (inventoryModel == null || item == null)
            {
                return false;
            }

            (int x, int y) = inventoryModel.GetCoordinates(targetSlotIndex);
            if (!PlaceOrStackInModel(inventoryModel, item, x, y))
            {
                return inventoryModel.TryAdd(item);
            }

            return true;
        }

        private bool AddConfirmedItemToQuickslot(int quickslotIndex, string itemId, int count, int rotation)
        {
            if (QuickslotUIController.Instance == null)
            {
                return false;
            }

            ItemInstance item = CreateConfirmedItem(itemId, count, rotation);
            if (item == null)
            {
                return false;
            }

            ItemInstance existing = QuickslotUIController.Instance.GetItem(quickslotIndex);
            if (existing != null)
            {
                if (existing.Data == item.Data && existing.Data.maxStackSize > 1 &&
                    existing.currentStackCount + item.currentStackCount <= existing.Data.maxStackSize)
                {
                    existing.currentStackCount += item.currentStackCount;
                    QuickslotUIController.Instance.RefreshSlotVisual(quickslotIndex);
                    return true;
                }

                return false;
            }

            item.currentRotation = ItemRotation.Deg0;
            QuickslotUIController.Instance.SetItemInSlot(quickslotIndex, item);
            return true;
        }

        private void RestoreConfirmedItemToStorage(int sourceSlotIndex, string itemId, int count, int rotation)
        {
            ItemInstance item = CreateConfirmedItem(itemId, count, rotation);
            if (item == null)
            {
                return;
            }

            SubmitPut(sourceSlotIndex, item);
        }

        private static ItemInstance CreateConfirmedItem(string itemId, int count, int rotation)
        {
            ItemData itemData = ItemDataRegistry.Find(itemId);
            if (itemData == null)
            {
                return null;
            }

            ItemInstance item = new ItemInstance(itemData, Mathf.Max(1, count));
            item.currentRotation = (ItemRotation)Mathf.Clamp(rotation, 0, 3);
            return item;
        }

        private void OpenPlayerInventoryBesideStorage()
        {
            if (GridInventoryView.Instance == null)
            {
                openedInventoryForStorage = false;
                return;
            }

            openedInventoryForStorage = !GridInventoryView.Instance.IsOpen;
            GridInventoryView.Instance.OpenForStorage(openedInventoryForStorage);
        }

        private void ClosePlayerInventoryOpenedByStorage()
        {
            if (!openedInventoryForStorage || GridInventoryView.Instance == null)
            {
                openedInventoryForStorage = false;
                return;
            }

            GridInventoryView.Instance.CloseForStorage();
            openedInventoryForStorage = false;
        }

        private void BindInventoryDragEvents()
        {
            if (inventoryDragEventsBound || GridInventoryView.Instance == null)
            {
                return;
            }

            GridInventoryView.Instance.OnDragUpdate += HandleInventoryDragUpdate;
            GridInventoryView.Instance.OnDragEndEvent += HandleAnyDragEnd;
            inventoryDragEventsBound = true;
        }

        private void UnbindInventoryDragEvents()
        {
            if (!inventoryDragEventsBound || GridInventoryView.Instance == null)
            {
                inventoryDragEventsBound = false;
                return;
            }

            GridInventoryView.Instance.OnDragUpdate -= HandleInventoryDragUpdate;
            GridInventoryView.Instance.OnDragEndEvent -= HandleAnyDragEnd;
            inventoryDragEventsBound = false;
        }

        private void ApplyStorageLayout()
        {
            if (container == null)
            {
                return;
            }

            container.pickingMode = PickingMode.Ignore;
            container.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0f));
            container.style.alignItems = Align.FlexEnd;
            container.style.justifyContent = Justify.Center;

            VisualElement inventory = container.Q<VisualElement>(name: "inventory-window");
            if (inventory != null)
            {
                inventory.pickingMode = PickingMode.Position;
                inventory.style.marginLeft = 0;
                inventory.style.marginRight = 24;
                inventory.style.width = StorageWindowWidth;
                inventory.style.maxWidth = StorageWindowWidth;
                inventory.style.minWidth = StorageWindowWidth;
                inventory.style.height = new Length(82f, LengthUnit.Percent);
                inventory.style.paddingLeft = 12;
                inventory.style.paddingRight = 12;

                VisualElement splitContainer = inventory.Q<VisualElement>(className: "split-container");
                if (splitContainer != null)
                {
                    splitContainer.style.justifyContent = Justify.Center;
                    splitContainer.style.overflow = Overflow.Hidden;
                }

                VisualElement leftPanel = inventory.Q<VisualElement>(className: "left-panel");
                if (leftPanel != null)
                {
                    leftPanel.style.borderRightWidth = 0;
                    leftPanel.style.paddingRight = 0;
                    leftPanel.style.marginRight = 0;
                    leftPanel.style.flexGrow = 0;
                    leftPanel.style.flexShrink = 0;
                }

                VisualElement rightPanel = inventory.Q<VisualElement>(className: "right-panel");
                if (rightPanel != null)
                {
                    rightPanel.style.display = DisplayStyle.None;
                }
            }
        }

        private void ConfigureStorageScrollView(VisualElement inventory, VisualElement slotsContainer, float gridWidth)
        {
            ScrollView slotsScrollView = inventory.Q<ScrollView>(className: "slots-scroll-view");
            if (slotsScrollView == null)
            {
                VisualElement parent = slotsContainer.parent;
                while (parent != null)
                {
                    if (parent is ScrollView scrollView)
                    {
                        slotsScrollView = scrollView;
                        break;
                    }

                    parent = parent.parent;
                }
            }

            if (slotsScrollView == null)
            {
                return;
            }

            slotsScrollView.mode = ScrollViewMode.Vertical;
            slotsScrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            slotsScrollView.verticalScrollerVisibility = ScrollerVisibility.Auto;
            slotsScrollView.style.width = gridWidth + 24f;
            slotsScrollView.style.minWidth = gridWidth + 24f;
            slotsScrollView.style.maxWidth = gridWidth + 24f;
            slotsScrollView.style.height = new Length(100f, LengthUnit.Percent);
            slotsScrollView.style.flexGrow = 1;
            slotsScrollView.style.overflow = Overflow.Hidden;

            if (slotsScrollView.horizontalScroller != null)
            {
                slotsScrollView.horizontalScroller.style.display = DisplayStyle.None;
            }

            slotsScrollView.contentContainer.style.width = gridWidth;
            slotsScrollView.contentContainer.style.minWidth = gridWidth;
            slotsScrollView.contentContainer.style.maxWidth = gridWidth;
            slotsScrollView.contentContainer.style.overflow = Overflow.Hidden;
        }

        private void SetLocalInputActive(bool active)
        {
            localPlayerInputHandler = null;

            PlayerController[] controllers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            foreach (PlayerController controller in controllers)
            {
                if (controller == null)
                {
                    continue;
                }

                NetworkObject networkObject = controller.GetComponent<NetworkObject>();
                bool isLocalPlayer = controller.IsLocalPlayer ||
                    (networkObject != null && networkObject.HasInputAuthority);
                if (!isLocalPlayer)
                {
                    continue;
                }

                localPlayerInputHandler = controller.InputHandler;
                break;
            }

            if (localPlayerInputHandler != null)
            {
                localPlayerInputHandler.SetInputActive(active);
            }
        }
    }
}
