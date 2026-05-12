using System;
using System.Collections.Generic;
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
        private StorageSystem.StorageNetworkSync currentNetworkSync;

        private UnityEngine.UIElements.VisualElement originalInventoryParent;
        private GridItemView currentActionItemView;
        private GridInventoryModel currentActionSourceModel;
        private UnityEngine.UIElements.VisualElement originalInventoryGhostIcon;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start() 
        {
            lootView.Initialize();

            lootView.OnTakeAllClicked += HandleTakeAll;
            lootView.OnCloseClicked += CloseLoot;
            lootView.OnQuantitySplitConfirm += RealHandleQuantitySplitConfirm;

            GridInventoryController.OnRequestQuickMove -= HandleQuickMove;
            GridInventoryController.OnRequestQuickMove += HandleQuickMove;
            GridInventoryController.OnRequestSplitMove -= RealHandleSplitMoveRequest;
            GridInventoryController.OnRequestSplitMove += RealHandleSplitMoveRequest;
            GridItemView.OnItemDroppedGlobal -= HandleItemDroppedGlobal;
            GridItemView.OnItemDroppedGlobal += HandleItemDroppedGlobal;
        }

        public void OpenLoot(InteractableLoot source, string storageId, string title, StorageSystem.StorageNetworkSync networkSync)
        {
            if (IsOpen) return;
            IsOpen = true;
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
            dummyLootGridView.Init(lootView.GetLootScrollView(), networkSync.StorageWidth, lootView.GhostIcon);

            // 2. Build the Loot Inventory Controller
            var model = networkSync.GetOrCreateModel(storageId);

            lootInventoryController = new GridInventoryController.Builder(dummyLootGridView)
                .WithDimensions(networkSync.StorageWidth, networkSync.StorageHeight)
                .WithExistingModel(model)
                .Build();

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

            SubmitSnapshot();

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
            if (IsOpen && UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame)
            {
                CloseLoot();
            }
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

        private void HandleTakeAll()
        {
            if (!IsOpen || lootInventoryController == null || GridInventoryClass.Instance == null) return;
            var targetModel = GridInventoryClass.Instance.Controller.Model;
            var sourceModel = lootInventoryController.Model;

            List<ItemInstance> itemsToMove = new List<ItemInstance>();
            HashSet<ItemInstance> processed = new HashSet<ItemInstance>();
            for (int i = 0; i < sourceModel.Items.Length; i++)
            {
                var item = sourceModel.Items[i];
                if (item != null && !processed.Contains(item))
                {
                    processed.Add(item);
                    itemsToMove.Add(item);
                }
            }

            foreach (var item in itemsToMove)
            {
                if (targetModel.TryAdd(item))
                {
                    sourceModel.TryRemove(item);
                }
            }
            UpdateCapacities();
        }

        private void HandleQuickMove(ItemInstance item, GridInventoryModel sourceModel)
        {
            if (!IsOpen) return;
            
            GridInventoryModel targetModel = (sourceModel == lootInventoryController.Model) 
                ? GridInventoryClass.Instance.Controller.Model 
                : lootInventoryController.Model;

            if (targetModel.TryAdd(item))
            {
                sourceModel.TryRemove(item);
                UpdateCapacities();
            }
        }

        // Corrected tracking
        private ItemInstance currentSplitItem;
        
        private void RealHandleSplitMoveRequest(ItemInstance item, GridInventoryModel sourceModel)
        {
            if (!IsOpen || item.currentStackCount <= 1) return;
            currentActionSourceModel = sourceModel;
            currentSplitItem = item;
            lootView.ShowQuantityPopup(item.currentStackCount - 1);
        }

        private void RealHandleQuantitySplitConfirm(int amount)
        {
            if (currentActionSourceModel == null || currentSplitItem == null) return;
            
            GridInventoryModel targetModel = (currentActionSourceModel == lootInventoryController.Model) 
                ? GridInventoryClass.Instance.Controller.Model 
                : lootInventoryController.Model;

            ItemInstance splitPart = new ItemInstance(currentSplitItem.Data, amount);
            if (targetModel.TryAdd(splitPart))
            {
                currentSplitItem.currentStackCount -= amount;
                currentActionSourceModel.Items.Invoke();
                UpdateCapacities();
            }
            currentSplitItem = null;
            currentActionSourceModel = null;
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

            if (lootSlot != null && sourceModel != lootInventoryController.Model)
            {
                targetModel = lootInventoryController.Model;
                var coords = targetModel.GetCoordinates(lootSlot.Index);
                targetX = coords.x; targetY = coords.y;
            }
            else if (playerSlot != null && sourceModel != GridInventoryClass.Instance.Controller.Model)
            {
                targetModel = GridInventoryClass.Instance.Controller.Model;
                var coords = targetModel.GetCoordinates(playerSlot.Index);
                targetX = coords.x; targetY = coords.y;
            }

            if (targetModel != null)
            {
                var baseTargetItem = targetModel.Get(targetX, targetY);

                if (baseTargetItem != null && baseTargetItem.Data == item.Data && item.Data.maxStackSize > 1)
                {
                    int total = item.currentStackCount + baseTargetItem.currentStackCount;
                    if (total <= item.Data.maxStackSize)
                    {
                        baseTargetItem.currentStackCount = total;
                        sourceModel.TryRemove(item);
                        targetModel.Items.Invoke();
                    }
                    else
                    {
                        baseTargetItem.currentStackCount = item.Data.maxStackSize;
                        item.currentStackCount = total - item.Data.maxStackSize;
                        targetModel.Items.Invoke();
                        sourceModel.Items.Invoke();
                    }
                }
                else if (baseTargetItem == null)
                {
                    if (targetModel.CanPlaceItem(item, targetX, targetY))
                    {
                        sourceModel.TryRemove(item);
                        targetModel.PlaceItem(item, targetX, targetY);
                    }
                }
                UpdateCapacities();
            }
        }

        private void UpdateCapacities()
        {
            if (lootInventoryController == null || GridInventoryClass.Instance == null) return;
            // Optional: calculate actual used slots and max slots
            SubmitSnapshot();
        }

        private void SubmitSnapshot()
        {
            if (currentNetworkSync != null && currentStorageId != null && lootInventoryController != null)
            {
                currentNetworkSync.SubmitStorageSnapshot(currentStorageId, 
                    StorageSystem.StorageGridSerializer.ToSaveData(currentStorageId, lootInventoryController.Model));
            }
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