using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using Systems.GridInventory;

namespace Systems.Shop
{
    public class ShopController : MonoBehaviour
    {
        // 전역으로 접근 가능한 싱글톤 인스턴스 (UIManager 등의 하위에 두어도 됩니다)
        public static ShopController Instance { get; private set; }

        public bool IsOpen { get; private set; }

        [SerializeField] private ShopView shopView;
        
        private ShopModel model;
        private UnityEngine.UIElements.VisualElement originalInventoryParent;
        private UnityEngine.UIElements.VisualElement originalGhostParent;
        
        private ShopItemEntry currentPlacingItem;
        private ItemInstance ghostItemInstance;
        private bool isPlacing;
        private float openTime;
        
        private bool isWaitingForPurchaseApproval;
        private (int x, int y) pendingPlacementCoords;
        private ItemInstance pendingStackTargetItem;
        
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            
            if (shopView != null)
            {
                if (PlayerNetworkSetup.IsOfflineTestMode)
                {
                    if (global::Systems.GridInventory.GridInventory.Instance != null && global::Systems.GridInventory.GridInventory.Instance.Controller != null)
                    {
                        var invModel = global::Systems.GridInventory.GridInventory.Instance.Controller.Model;
                        if (invModel != null)
                        {
                            invModel.OnGoldChanged -= shopView.UpdateGold;
                        }
                    }
                }
                else
                {
                    BackendRoundManager.SharedGoldUpdated -= shopView.UpdateGold;
                }
            }
        }

        private void Start()
        {
            // 초기화 시 빈 아이템 리스트로 모델 생성 (실제 아이템은 OpenShop 할 때 주입)
            int initialGold = 0;
            
            if (PlayerNetworkSetup.IsOfflineTestMode)
            {
                if (global::Systems.GridInventory.GridInventory.Instance != null && global::Systems.GridInventory.GridInventory.Instance.Controller != null)
                {
                    var invModel = global::Systems.GridInventory.GridInventory.Instance.Controller.Model;
                    initialGold = invModel.Gold;
                    invModel.OnGoldChanged += shopView.UpdateGold;
                }
            }
            else
            {
                if (BackendRoundManager.Instance != null)
                {
                    initialGold = BackendRoundManager.Instance.SharedGold;
                }
                BackendRoundManager.SharedGoldUpdated += shopView.UpdateGold;
            }

            model = new ShopModel(new List<ShopItemEntry>());
            
            shopView.Initialize();
            shopView.UpdateGold(initialGold);
            
            // 이벤트 연결
            shopView.OnBuyItemClicked += HandleBuyItemClicked;
            shopView.OnSellItemClicked += HandleSellItemClicked;
            shopView.OnExitClicked += HandleExitShop;
            shopView.OnBuyTabClicked += HandleBuyTabClicked;
            shopView.OnSellTabClicked += HandleSellTabClicked;
            
            shopView.GetCancelPlaceButton().clicked += HandleCancelPlacement;
        }

        /// <summary>
        /// 특정 NPC나 자판기에서 전달받은 아이템 목록으로 상점을 엽니다.
        /// </summary>
        public void OpenShop(List<ShopItemEntry> shopItems)
        {
            IsOpen = true;
            openTime = Time.time;
            model.SetShopItems(shopItems);
            
            shopView.ShowShop();
            shopView.RenderCatalog(model.ShopItems, isSellMode: false);
            
            // 마우스 커서 표시 및 플레이어 조작 비활성화
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
            PlayerInputHandler playerInput = FindAnyObjectByType<PlayerInputHandler>();
            if (playerInput != null)
            {
                playerInput.SetInputActive(false);
            }
            
            // 인벤토리가 열려 있다면 닫거나, 상점 전용 배치 모드를 준비하는 로직
            if (GridInventoryView.Instance != null && GridInventoryView.Instance.isActiveAndEnabled)
            {
                // 필요하다면 인벤토리를 강제로 숨기는 로직 추가
            }

            // 👉 [추가] 상점 입장 인사
            MascotEventManager.TriggerGreeting();
        }

        public void CloseShop()
        {
            IsOpen = false;
            // 상점 종료 시점에 혹시나 배치 모드 중이었다면 인벤토리를 본래 UI로 돌려놓습니다.
            if (originalInventoryParent != null)
            {
                HandleCancelPlacement();
            }

            shopView.HideShop();
            
            // 마우스 커서 숨김 및 플레이어 조작 활성화
            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            UnityEngine.Cursor.visible = false;
            PlayerInputHandler playerInput = FindAnyObjectByType<PlayerInputHandler>();
            if (playerInput != null)
            {
                playerInput.SetInputActive(true);
            }
        }

        private void Update()
        {
            if (IsOpen && Time.time - openTime > 0.1f)
            {
                if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame)
                {
                    CloseShop();
                    return;
                }
            }

            if (isPlacing && ghostItemInstance != null && GridInventoryView.Instance != null)
            {
                var ghostIcon = shopView.GetRootVisualElement().Q<UnityEngine.UIElements.VisualElement>(name: "ghostIcon");
                if (ghostIcon != null && ghostIcon.style.visibility == Visibility.Visible)
                {
                    // R 키로 회전
                    if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.rKey.wasPressedThisFrame)
                    {
                        int nextRot = ((int)ghostItemInstance.currentRotation + 1) % 4;
                        ghostItemInstance.currentRotation = (ItemRotation)nextRot;
                        ghostIcon.style.rotate = new Rotate(new Angle(nextRot * 90f));
                    }

                    Vector2 mousePos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
                    mousePos.y = Screen.height - mousePos.y; 
                    
                    var panel = shopView.GetRootVisualElement().panel;
                    Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(panel, mousePos);
                    
                    // 앵커와 피벗 업데이트를 매 프레임마다 혹은 회전 시에 해줘야 드래깅이 더 자연스럽습니다.
                    float dragWidth, dragHeight, anchorXRatio, anchorYRatio;
                    GridInventoryDragHelper.GetGhostSizeAndPivot(ghostItemInstance, out dragWidth, out dragHeight, out anchorXRatio, out anchorYRatio);
                    ghostIcon.style.width = dragWidth;
                    ghostIcon.style.height = dragHeight;
                    ghostIcon.style.transformOrigin = new TransformOrigin(new Length(anchorXRatio * 100f, LengthUnit.Percent), new Length(anchorYRatio * 100f, LengthUnit.Percent));
                    
                    GridInventoryDragHelper.UpdateGhostPosition(ghostIcon, panelPos);
                    
                    GridSlot closestSlot = GridInventoryView.Instance.GetGridSlotAtPosition(panelPos);
                    GridInventoryView.Instance.ResetAllSlotColors();
                    
                    bool canPlace = false;
                    ItemInstance stackTargetItem = null;
                    if (closestSlot != null && global::Systems.GridInventory.GridInventory.Instance != null && global::Systems.GridInventory.GridInventory.Instance.Controller != null)
                    {
                        var invModel = global::Systems.GridInventory.GridInventory.Instance.Controller.Model;
                        var targetCoords = invModel.GetCoordinates(closestSlot.Index);
                        
                        var positions = ghostItemInstance.Data.gridShape.GetRotatedPositions(ghostItemInstance.currentRotation);
                        
                        HashSet<ItemInstance> overlappingItems = new HashSet<ItemInstance>();
                        bool outOfBounds = false;
                        
                        foreach (var pos in positions) {
                            int checkX = targetCoords.x + pos.x;
                            int checkY = targetCoords.y + pos.y;
                            
                            if (checkX < 0 || checkY < 0 || checkX >= invModel.Width || checkY >= invModel.Height) {
                                outOfBounds = true;
                                break;
                            }
                            var foundItem = invModel.Get(checkX, checkY);
                            if (foundItem != null) {
                                overlappingItems.Add(foundItem);
                            }
                        }

                        if (!outOfBounds) {
                            if (overlappingItems.Count == 0) {
                                canPlace = true;
                            } else if (overlappingItems.Count == 1) {
                                var targetItem = overlappingItems.First();
                                if (targetItem.Data == ghostItemInstance.Data && targetItem.Data.maxStackSize >= targetItem.currentStackCount + ghostItemInstance.currentStackCount) {
                                    canPlace = true;
                                    stackTargetItem = targetItem;
                                }
                            }
                        }
                        
                        Color highlightColor = canPlace ? new Color(0f, 1f, 0f, 0.3f) : new Color(1f, 0f, 0f, 0.3f);
                        
                        foreach (var pos in positions) {
                            int checkX = targetCoords.x + pos.x;
                            int checkY = targetCoords.y + pos.y;

                            if (checkX >= 0 && checkY >= 0 && checkX < invModel.Width && checkY < invModel.Height) {
                                int slotIndex = invModel.GetIndex(checkX, checkY);
                                GridInventoryView.Instance.SetSlotColor(slotIndex, highlightColor);
                            }
                        }
                    }

                    // 좌클릭 시 배치 시도
                    if (UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
                    {
                        if (closestSlot != null && canPlace && !isWaitingForPurchaseApproval)
                        {
                            var invModel = global::Systems.GridInventory.GridInventory.Instance.Controller.Model;
                            var targetCoords = invModel.GetCoordinates(closestSlot.Index);
                            
                            int totalCost = currentPlacingItem.BuyPrice * ghostItemInstance.currentStackCount;
                            
                            isWaitingForPurchaseApproval = true;
                            pendingPlacementCoords = targetCoords;
                            pendingStackTargetItem = stackTargetItem;
                            
                            if (PlayerNetworkSetup.IsOfflineTestMode)
                            {
                                // Offline fallback
                                if (invModel.TrySpendGold(totalCost))
                                {
                                    OnSpendGoldResult(true);
                                }
                                else
                                {
                                    OnSpendGoldResult(false);
                                }
                                
                                shopView.SetDialogue("* \"탁월한 선택이야!\"");
                                HandleCancelPlacement(); // 배치 모드 종료 (돌아가기)
                                // 👉 [추가] 판매 감사 애니메이션
                                MascotEventManager.TriggerThankYou();
                            }
                            else
                            {
                                if (BackendPlayerNetworkSync.LocalInstance != null)
                                {
                                    BackendPlayerNetworkSync.LocalInstance.RpcRequestSpendGold(totalCost);
                                }
                            }
                        }
                    }
                }
            }
        }

        public void OnSpendGoldResult(bool success)
        {
            if (!isWaitingForPurchaseApproval) return;
            isWaitingForPurchaseApproval = false;

            if (success)
            {
                if (global::Systems.GridInventory.GridInventory.Instance != null && global::Systems.GridInventory.GridInventory.Instance.Controller != null)
                {
                    var invModel = global::Systems.GridInventory.GridInventory.Instance.Controller.Model;
                    if (pendingStackTargetItem != null) {
                        pendingStackTargetItem.currentStackCount += ghostItemInstance.currentStackCount;
                        invModel.Items.Invoke(); // UI 갱신
                    } else {
                        invModel.PlaceItem(ghostItemInstance, pendingPlacementCoords.x, pendingPlacementCoords.y);
                    }
                }
                
                shopView.SetDialogue("* \"탁월한 선택이야!\"");
                HandleCancelPlacement(); // 배치 모드 종료 (돌아가기)
            }
            else
            {
                shopView.SetDialogue("* \"앗! 골드가 부족하잖아!\"");
            }
            
            pendingStackTargetItem = null;
        }

        private void HandleBuyTabClicked()
        {
            if (isPlacing) HandleCancelPlacement();
            
            shopView.RenderCatalog(model.ShopItems, isSellMode: false);
            shopView.SetDialogue("* \"Welcome to Pokopia, where every item has a tale!\"");
        }

        private void HandleSellTabClicked()
        {
            if (isPlacing) HandleCancelPlacement();
            
            RefreshSellCatalog();
            shopView.SetDialogue("* \"What do you have for me today?\"");
        }

        private void RefreshSellCatalog()
        {
            List<ShopItemEntry> inventoryItems = new List<ShopItemEntry>();
            if (global::Systems.GridInventory.GridInventory.Instance != null && global::Systems.GridInventory.GridInventory.Instance.Controller != null)
            {
                var invModel = global::Systems.GridInventory.GridInventory.Instance.Controller.Model;
                HashSet<ItemInstance> processed = new HashSet<ItemInstance>();
                Dictionary<ItemData, int> itemCounts = new Dictionary<ItemData, int>();
                
                for (int i = 0; i < invModel.Width * invModel.Height; i++)
                {
                    var item = invModel.Get(i);
                    if (item != null && !processed.Contains(item))
                    {
                        processed.Add(item);
                        if (!itemCounts.ContainsKey(item.Data)) {
                            itemCounts[item.Data] = 0;
                        }
                        itemCounts[item.Data] += item.currentStackCount;
                    }
                }
                
                foreach (var kvp in itemCounts)
                {
                    var itemData = kvp.Key;
                    int totalCount = kvp.Value;
                    
                    // 임시로 구매가의 절반을 판매가로 산정하거나, 기본 10골드로 설정
                    int sellPrice = 10;
                    var shopItem = model.ShopItems.Find(x => x.ItemData == itemData);
                    if (shopItem != null)
                    {
                        sellPrice = Mathf.Max(1, shopItem.BuyPrice / 2);
                    }
                    
                    inventoryItems.Add(new ShopItemEntry {
                        ItemData = itemData,
                        BuyPrice = sellPrice,
                        StockCount = totalCount
                    });
                }
            }
            
            shopView.RenderCatalog(inventoryItems, isSellMode: true);
        }

        private void HandleExitShop()
        {
            CloseShop();
        }

        private void HandleBuyItemClicked(ShopItemEntry itemToBuy, int quantity)
        {
            int currentGold = 0;
            if (PlayerNetworkSetup.IsOfflineTestMode)
            {
                if (global::Systems.GridInventory.GridInventory.Instance != null && global::Systems.GridInventory.GridInventory.Instance.Controller != null)
                {
                    currentGold = global::Systems.GridInventory.GridInventory.Instance.Controller.Model.Gold;
                }
            }
            else
            {
                if (BackendRoundManager.Instance != null)
                {
                    currentGold = BackendRoundManager.Instance.SharedGold;
                }
            }

            if (currentGold < itemToBuy.BuyPrice * quantity)
            {
                shopView.SetDialogue("* \"장난해? 돈이 모자라잖아.\"");
                // 👉 [추가] 잔액 부족 거절 애니메이션
                MascotEventManager.TriggerReject();
                return;
            }

            // 배치 모드로 전환
            shopView.SwitchToPlacementMode();
            shopView.SetDialogue("* \"빈 공간을 찾아 클릭해서 물건을 놓아봐!\"");
            
            // 인벤토리 자체를 활성화 (Flex)
            // 배치 모드 전환 전에 확실히 인벤토리 뷰를 켭니다.
            if (GridInventoryView.Instance != null && GridInventoryView.Instance.Container != null)
            {
                var invContainer = GridInventoryView.Instance.Container;
                
                // 인벤토리 루트 컨테이너 활성화
                invContainer.style.display = UnityEngine.UIElements.DisplayStyle.Flex;
                invContainer.style.visibility = UnityEngine.UIElements.Visibility.Visible;
                
                // 인벤토리 윈도우(main-window)가 화면을 덮지 않도록 조치
                var inventoryWindow = invContainer.Q<UnityEngine.UIElements.VisualElement>("inventory-window");
                if (inventoryWindow != null)
                {
                    inventoryWindow.style.display = UnityEngine.UIElements.DisplayStyle.None;
                }
                
                // 스타일 시트 복사: scrollView가 ShopView로 넘어갈 때 스타일을 잃지 않도록
                var gridStyleSheet = GridInventoryView.Instance.GridStyleSheet;
                if (gridStyleSheet != null && !shopView.GetRootVisualElement().styleSheets.Contains(gridStyleSheet))
                {
                    shopView.GetRootVisualElement().styleSheets.Add(gridStyleSheet);
                }
                
                // inventory-window 전체를 가져오면 오른쪽 패널(장비창 등)도 같이 오므로,
                // 스크롤뷰(실제 그리드)만 뽑아서 붙입니다.
                var scrollView = invContainer.Q<UnityEngine.UIElements.ScrollView>(className: "slots-scroll-view");
                if (scrollView != null)
                {
                    originalInventoryParent = scrollView.parent;
                    shopView.GetPlacementGridContainer().Add(scrollView);
                    
                    // 기존 GridInventoryView에서 슬롯들의 시각적 부모인 itemsContainer가
                    // slotsContainer 내에 Position.Absolute로 존재합니다.
                    // 스크롤뷰를 이동할 때 이들이 제대로 표시되도록 해야 합니다.

                    // 스크롤뷰가 부모(상점 좌측 검은 패널) 공간을 꽉 채우도록 설정
                    scrollView.style.flexGrow = 1;
                    scrollView.style.width = new UnityEngine.UIElements.StyleLength(UnityEngine.UIElements.StyleKeyword.Auto);
                    scrollView.style.height = new UnityEngine.UIElements.StyleLength(UnityEngine.UIElements.StyleKeyword.Auto);
                    
                    // 중요: 스크롤 뷰가 화면에 보이려면 display가 Flex여야 합니다.
                    scrollView.style.display = UnityEngine.UIElements.DisplayStyle.Flex;
                    // 추가: opacity와 visibility 보장
                    scrollView.style.visibility = UnityEngine.UIElements.Visibility.Visible;
                    scrollView.style.opacity = 1f;

                    // 스크롤뷰 안의 컨텐츠가 올바르게 중앙에 오도록 처리
                    var innerSlotsContainer = scrollView.Q<UnityEngine.UIElements.VisualElement>(name: "slotsContainer");
                    if (innerSlotsContainer != null)
                    {
                        innerSlotsContainer.style.alignSelf = UnityEngine.UIElements.Align.Center;
                        innerSlotsContainer.style.marginTop = 12f; // 상단 여백 추가
                        innerSlotsContainer.style.marginBottom = 12f;

                    // UI 요소들이 보일 수 있게 강제로 Visibility 지정
                    innerSlotsContainer.style.visibility = UnityEngine.UIElements.Visibility.Visible;
                    innerSlotsContainer.style.display = UnityEngine.UIElements.DisplayStyle.Flex;
                    
                    // 아이템 컨테이너도 보이게
                    var itemsContainer = innerSlotsContainer.Q<UnityEngine.UIElements.VisualElement>(name: "itemsContainer");
                    if (itemsContainer != null)
                    {
                        itemsContainer.style.visibility = UnityEngine.UIElements.Visibility.Visible;
                        itemsContainer.style.display = UnityEngine.UIElements.DisplayStyle.Flex;
                    }
                    }
                    scrollView.style.alignSelf = UnityEngine.UIElements.Align.Center;
                    
                    // 백그라운드가 묻히지 않게 스크롤뷰 안의 컨텐츠가 보일 수 있도록 처리
                    var contentViewport = scrollView.Q<UnityEngine.UIElements.VisualElement>(className: "unity-scroll-view__content-viewport");
                    if (contentViewport != null)
                    {
                        contentViewport.style.overflow = UnityEngine.UIElements.Overflow.Visible;
                        contentViewport.style.visibility = UnityEngine.UIElements.Visibility.Visible;
                        contentViewport.style.display = UnityEngine.UIElements.DisplayStyle.Flex;
                    }
                    
                    var contentViewOuter = scrollView.Q<UnityEngine.UIElements.VisualElement>(className: "unity-scroll-view__content-container");
                    if (contentViewOuter != null)
                    {
                        contentViewOuter.style.alignSelf = UnityEngine.UIElements.Align.Center;
                        contentViewOuter.style.visibility = UnityEngine.UIElements.Visibility.Visible;
                        contentViewOuter.style.display = UnityEngine.UIElements.DisplayStyle.Flex;
                    }
                }

                var ghostIcon = invContainer.Q<UnityEngine.UIElements.VisualElement>(name: "ghostIcon");
                if (ghostIcon != null)
                {
                    originalGhostParent = ghostIcon.parent;
                    shopView.GetRootVisualElement().Add(ghostIcon);
                    ghostIcon.BringToFront(); // 고스트 아이콘을 최상단으로 올려서 그리드 위로 렌더링되게 함
                    
                    // 드래그 시각화 설정
                    currentPlacingItem = itemToBuy;
                    ghostItemInstance = new ItemInstance(itemToBuy.ItemData, quantity);
                    isPlacing = true;
                    
                    ghostIcon.style.backgroundImage = new StyleBackground(itemToBuy.ItemData.itemIcon);
                    ghostIcon.style.visibility = Visibility.Visible;
                    
                    float dragWidth, dragHeight, anchorXRatio, anchorYRatio;
                    GridInventoryDragHelper.GetGhostSizeAndPivot(ghostItemInstance, out dragWidth, out dragHeight, out anchorXRatio, out anchorYRatio);
                    
                    ghostIcon.style.width = dragWidth;
                    ghostIcon.style.height = dragHeight;
                    ghostIcon.style.transformOrigin = new TransformOrigin(new Length(anchorXRatio * 100f, LengthUnit.Percent), new Length(anchorYRatio * 100f, LengthUnit.Percent));
                    ghostIcon.style.rotate = new Rotate(new Angle(0));
                    ghostIcon.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(BackgroundSizeType.Contain));
                }
            }
        }
        
        private void HandleCancelPlacement()
        {
            isWaitingForPurchaseApproval = false;
            pendingStackTargetItem = null;
            
            shopView.SwitchToCatalogMode();
            shopView.SetDialogue("* \"마음이 바뀌었어? 천천히 골라.\"");
            
            isPlacing = false;
            currentPlacingItem = null;
            ghostItemInstance = null;

            if (GridInventoryView.Instance != null) {
                GridInventoryView.Instance.ResetAllSlotColors();
            }

            // 인벤토리(그리드)를 원래 위치로 복구합니다.
            if (GridInventoryView.Instance != null && GridInventoryView.Instance.Container != null)
            {
                var invContainer = GridInventoryView.Instance.Container;
                // 다시 안 보이게 숨김
                invContainer.style.display = UnityEngine.UIElements.DisplayStyle.None;
                
                // inventory-window 다시 보이게 복구
                var inventoryWindow = invContainer.Q<UnityEngine.UIElements.VisualElement>("inventory-window");
                if (inventoryWindow != null)
                {
                    inventoryWindow.style.display = UnityEngine.UIElements.DisplayStyle.Flex;
                }
                
                invContainer.style.visibility = UnityEngine.UIElements.StyleKeyword.Null;
                
                var scrollView = shopView.GetPlacementGridContainer().Q<UnityEngine.UIElements.ScrollView>(className: "slots-scroll-view");
                if (scrollView != null && originalInventoryParent != null)
                {
                    // 설정했던 flexGrow 속성 등 초기화
                    scrollView.style.flexGrow = UnityEngine.UIElements.StyleKeyword.Null;
                    scrollView.style.width = UnityEngine.UIElements.StyleKeyword.Null;
                    scrollView.style.height = UnityEngine.UIElements.StyleKeyword.Null;
                    scrollView.style.alignSelf = UnityEngine.UIElements.StyleKeyword.Null;
                    scrollView.style.visibility = UnityEngine.UIElements.StyleKeyword.Null;
                    scrollView.style.opacity = UnityEngine.UIElements.StyleKeyword.Null;
                    
                    var contentViewport = scrollView.Q<UnityEngine.UIElements.VisualElement>(className: "unity-scroll-view__content-viewport");
                    if (contentViewport != null)
                    {
                        contentViewport.style.overflow = UnityEngine.UIElements.StyleKeyword.Null;
                        contentViewport.style.visibility = UnityEngine.UIElements.StyleKeyword.Null;
                        contentViewport.style.display = UnityEngine.UIElements.StyleKeyword.Null;
                    }
                    
                    var contentViewOuter = scrollView.Q<UnityEngine.UIElements.VisualElement>(className: "unity-scroll-view__content-container");
                    if (contentViewOuter != null)
                    {
                        contentViewOuter.style.alignSelf = UnityEngine.UIElements.StyleKeyword.Null;
                        contentViewOuter.style.visibility = UnityEngine.UIElements.StyleKeyword.Null;
                        contentViewOuter.style.display = UnityEngine.UIElements.StyleKeyword.Null;
                    }
                    
                    var innerSlotsContainer = scrollView.Q<UnityEngine.UIElements.VisualElement>(name: "slotsContainer");
                    if (innerSlotsContainer != null)
                    {
                        innerSlotsContainer.style.alignSelf = UnityEngine.UIElements.StyleKeyword.Null;
                        innerSlotsContainer.style.marginTop = UnityEngine.UIElements.StyleKeyword.Null;
                        innerSlotsContainer.style.marginBottom = UnityEngine.UIElements.StyleKeyword.Null;
                        innerSlotsContainer.style.visibility = UnityEngine.UIElements.StyleKeyword.Null;
                        innerSlotsContainer.style.display = UnityEngine.UIElements.StyleKeyword.Null;

                        var itemsContainer = innerSlotsContainer.Q<UnityEngine.UIElements.VisualElement>(name: "itemsContainer");
                        if (itemsContainer != null)
                        {
                            itemsContainer.style.visibility = UnityEngine.UIElements.StyleKeyword.Null;
                            itemsContainer.style.display = UnityEngine.UIElements.StyleKeyword.Null;
                        }
                    }

                    // content container 초기화
                    var contentViewInner = scrollView.Q<UnityEngine.UIElements.VisualElement>(className: "unity-scroll-view__content-container");
                    if (contentViewInner != null)
                    {
                        contentViewInner.style.alignSelf = UnityEngine.UIElements.StyleKeyword.Null;
                    }

                    // itemsContainer 복구 (필요한 경우, 하지만 보통 slotsContainer 안에 그대로 있습니다)

                    originalInventoryParent.Add(scrollView);
                }

                var gridStyleSheet = GridInventoryView.Instance.GridStyleSheet;
                if (gridStyleSheet != null && shopView.GetRootVisualElement().styleSheets.Contains(gridStyleSheet))
                {
                    shopView.GetRootVisualElement().styleSheets.Remove(gridStyleSheet);
                }

                var ghostIcon = shopView.GetRootVisualElement().Q<UnityEngine.UIElements.VisualElement>(name: "ghostIcon");
                if (ghostIcon != null && originalGhostParent != null)
                {
                    ghostIcon.style.visibility = Visibility.Hidden;
                    originalGhostParent.Add(ghostIcon);
                }
                
                originalInventoryParent = null;
                originalGhostParent = null;
            }
        }

        private void HandleSellItemClicked(ShopItemEntry itemToSell, int quantity)
        {
            if (global::Systems.GridInventory.GridInventory.Instance != null && global::Systems.GridInventory.GridInventory.Instance.Controller != null)
            {
                bool success = global::Systems.GridInventory.GridInventory.Instance.ConsumeItem(itemToSell.ItemData, quantity);
                if (success)
                {
                    if (PlayerNetworkSetup.IsOfflineTestMode)
                    {
                        // Offline fallback
                        var invModel = global::Systems.GridInventory.GridInventory.Instance.Controller.Model;
                        invModel.AddGold(itemToSell.BuyPrice * quantity); 
                    }
                    else
                    {
                        if (BackendPlayerNetworkSync.LocalInstance != null)
                        {
                            BackendPlayerNetworkSync.LocalInstance.RpcRequestAddGold(itemToSell.BuyPrice * quantity);
                        }
                    }
                    
                    shopView.SetDialogue("* \"탁월한 거래였어!\"");
                    RefreshSellCatalog(); // 판매 후 카탈로그 갱신
                    // 👉 [추가] 판매 감사 애니메이션
                    MascotEventManager.TriggerThankYou();
                }
                else
                {
                    shopView.SetDialogue("* \"어라, 그 물건이 어디 갔지?\"");
                    // 👉 [추가] 아이템 없음 깜짝 놀람 애니메이션
                    MascotEventManager.TriggerSurprise();
                }
            }
        }
        
        // 아이템 배치 성공 시 호출하는 시뮬레이션 메서드
        private void SimulatePlacement(ShopItemEntry itemToBuy)
        {
            if (PlayerNetworkSetup.IsOfflineTestMode)
            {
                if (global::Systems.GridInventory.GridInventory.Instance != null && global::Systems.GridInventory.GridInventory.Instance.Controller != null)
                {
                    var invModel = global::Systems.GridInventory.GridInventory.Instance.Controller.Model;
                    if (invModel.TrySpendGold(itemToBuy.BuyPrice))
                    {
                        shopView.SwitchToCatalogMode();
                        shopView.SetDialogue("* \"구매 고마워!\"");
                        // 👉 [추가] 구매 감사 애니메이션
                        MascotEventManager.TriggerThankYou();
                    }
                    else
                    {
                        shopView.SetDialogue("* \"돈이 부족한걸!\"");
                        // 👉 [추가] 잔액 부족 거절 애니메이션
                        MascotEventManager.TriggerReject();
                    }
                }
            }
            else
            {
                if (BackendRoundManager.Instance != null)
                {
                    if (BackendRoundManager.Instance.SharedGold >= itemToBuy.BuyPrice)
                    {
                        shopView.SwitchToCatalogMode();
                        shopView.SetDialogue("* \"구매 고마워!\"");
                        // 👉 [추가] 구매 감사 애니메이션
                        MascotEventManager.TriggerThankYou();
                    }
                    else
                    {
                        shopView.SetDialogue("* \"돈이 부족한걸!\"");
                        // 👉 [추가] 잔액 부족 거절 애니메이션
                        MascotEventManager.TriggerReject();
                    }
                }
            }
        }
    }
}
