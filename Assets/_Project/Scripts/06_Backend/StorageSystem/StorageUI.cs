using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
            OnDrop -= HandleStorageDrop;
            OnDropToQuickslot -= HandleDropToQuickslot;
            QuickslotUIController.OnItemDroppedGlobal -= HandleQuickslotItemDropped;
            QuickslotUIController.OnItemDragUpdateGlobal -= HandleQuickslotDragUpdate;
            QuickslotUIController.OnItemDragEndGlobal -= ResetAllSlotColors;
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
            OpenPlayerInventoryBesideStorage();
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
                QuickslotUIController.OnItemDroppedGlobal += HandleQuickslotItemDropped;
                QuickslotUIController.OnItemDragUpdateGlobal += HandleQuickslotDragUpdate;
                QuickslotUIController.OnItemDragEndGlobal += ResetAllSlotColors;
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
            if (!TryPlaceIncomingItem(sourceItem, targetSlot.Index))
            {
                return false;
            }

            inventoryModel.TryRemove(sourceItem);
            SubmitSnapshot();
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
            itemViews.Clear();
        }

        private void HandleModelChanged(IList<ItemInstance> items)
        {
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

            (int newX, int newY) = model.GetCoordinates(targetSlot.Index);
            model.TryRemove(sourceItem);

            if (!PlaceOrStackInModel(model, sourceItem, newX, newY))
            {
                itemView.RevertRotation(itemView.OriginalRotation);
                model.PlaceItem(sourceItem, oldX, oldY);
                return;
            }

            SubmitSnapshot();
        }

        private void HandleDropToQuickslot(GridItemView itemView, int quickslotIndex)
        {
            if (itemView?.ItemInst == null || model == null || QuickslotUIController.Instance == null)
            {
                return;
            }

            ItemInstance sourceItem = itemView.ItemInst;
            (int oldX, int oldY) = model.GetItemAnchorPosition(sourceItem);
            ItemInstance targetItem = QuickslotUIController.Instance.GetItem(quickslotIndex);

            model.TryRemove(sourceItem);
            sourceItem.currentRotation = ItemRotation.Deg0;

            if (targetItem != null)
            {
                QuickslotUIController.Instance.RemoveItemFromSlot(quickslotIndex);
                if (!model.PlaceItem(targetItem, oldX, oldY))
                {
                    model.PlaceItem(sourceItem, oldX, oldY);
                    QuickslotUIController.Instance.SetItemInSlot(quickslotIndex, targetItem);
                    return;
                }
            }

            QuickslotUIController.Instance.SetItemInSlot(quickslotIndex, sourceItem);
            SubmitSnapshot();
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

            if (!TryPlaceIncomingItem(item, targetSlot.Index))
            {
                QuickslotUIController.Instance.RefreshSlotVisual(sourceQuickslotIndex);
                return;
            }

            QuickslotUIController.Instance.RemoveItemFromSlot(sourceQuickslotIndex);
            SubmitSnapshot();
        }

        private void HandleQuickslotDragUpdate(ItemInstance item, Vector2 screenPosition)
        {
            if (!IsOpen || item == null)
            {
                return;
            }

            HighlightIncomingDrop(item, screenPosition);
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
            (int targetX, int targetY) = inventoryModel.GetCoordinates(inventorySlot.Index);

            model.TryRemove(sourceItem);
            if (!PlaceOrStackInModel(inventoryModel, sourceItem, targetX, targetY))
            {
                model.PlaceItem(sourceItem, oldX, oldY);
                return false;
            }

            SubmitSnapshot();
            return true;
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
            if (targetItem != null && targetItem.Data == sourceItem.Data && targetItem.Data.maxStackSize > 1)
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
            bool canPlace = model.CanPlaceItem(item, x, y);
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

        private void SubmitSnapshot()
        {
            if (networkSync == null || model == null || string.IsNullOrWhiteSpace(currentStorageId))
            {
                return;
            }

            networkSync.SubmitStorageSnapshot(currentStorageId, StorageGridSerializer.ToSaveData(currentStorageId, model));
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
            if (localPlayerInputHandler == null)
            {
                PlayerController[] controllers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
                foreach (PlayerController controller in controllers)
                {
                    if (controller != null && controller.IsLocalPlayer)
                    {
                        localPlayerInputHandler = controller.InputHandler;
                        break;
                    }
                }
            }

            if (localPlayerInputHandler != null)
            {
                localPlayerInputHandler.SetInputActive(active);
            }
        }
    }
}
