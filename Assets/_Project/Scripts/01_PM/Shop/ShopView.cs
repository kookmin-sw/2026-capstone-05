using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Systems.GridInventory;

namespace Systems.Shop
{
    public class ShopView : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        [SerializeField] private UIDocument backgroundDocument;
        [SerializeField] private Canvas mascotCanvas; // 마스코트 캔버스도 상점을 열고 닫을 때 켜고 끄기 위한 필드
        
        // Expose events for Controller
        public event Action<ShopItemEntry, int> OnBuyItemClicked;
        public event Action<ShopItemEntry, int> OnSellItemClicked;
        public event Action OnBuyTabClicked;
        public event Action OnSellTabClicked;
        public event Action<int, int> OnGridCellClicked; // x, y
        
        private VisualElement root;
        private Label shopTitle;
        private Label goldLabel;
        private VisualElement leftPanel;
        private float catalogLeftPanelWidth = -1f;
        private float catalogLeftPanelHeight = -1f;
        private VisualElement catalogContainer;
        private ScrollView catalogScroll;
        private VisualElement placementGridContainer;
        private VisualElement placementGrid;
        
        // Popup
        private VisualElement popupOverlay;
        private Label popupItemName;
        private Label popupItemPrice;
        private Label popupItemMaxQuantity;
        private Label popupItemDesc;
        private Button btnPopupConfirm;
        private Button btnPopupCancel;
        private VisualElement popupShapeGrid;
        
        // Tabs
        private Button btnTabBuy;
        private Button btnTabSell;
        
        // Placement Actions
        private VisualElement mainActionButtons;
        private VisualElement placementActionButtons;
        private Button btnPlace;
        private Button btnCancelPlace;
        
        private ShopItemEntry currentSelectedItem;
        private bool isCurrentlySelling = false;
        private Button btnQuantityMinus;
        private Button btnQuantityPlus;
        private Label labelQuantity;
        private int currentQuantity = 1;
        private int maxQuantity = 1;
        private int currentGold;

        private void Awake()
        {
            if (document == null) document = GetComponent<UIDocument>();
        }

        public void Initialize()
        {
            if (document == null)
            {
                Debug.LogError("ShopView: document가 할당되지 않았습니다!");
                return;
            }

            root = document.rootVisualElement;
            root.style.display = DisplayStyle.None; // Hide initially
            FixedAspectRatioManager.RequestRefresh();

            document.sortingOrder = 6;

            if (backgroundDocument != null)
            {
                backgroundDocument.sortingOrder = 2; // 상점 배경
                backgroundDocument.gameObject.SetActive(false);
            }
            else
            {
                Debug.LogWarning("ShopView: backgroundDocument(갈색 배경)가 할당되지 않았습니다! Inspector를 확인해주세요.");
            }

            if (mascotCanvas != null)
            {
                mascotCanvas.gameObject.SetActive(false); // 처음에 마스코트 숨김
            }
            else
            {
                Debug.LogWarning("ShopView: mascotCanvas가 할당되지 않았습니다! Inspector를 확인해주세요.");
            }

            shopTitle = root.Q<Label>("shop-title");
            goldLabel = root.Q<Label>("gold-label");
            leftPanel = root.Q<VisualElement>("left-panel");
            catalogContainer = root.Q<VisualElement>("catalog-container");
            catalogScroll = root.Q<ScrollView>("catalog-scroll");
            placementGridContainer = root.Q<VisualElement>("placement-grid-container");
            placementGrid = root.Q<VisualElement>("placement-grid");

            // Popup
            popupOverlay = root.Q<VisualElement>("item-detail-popup");
            popupItemName = root.Q<Label>("popup-item-name");
            popupItemPrice = root.Q<Label>("popup-item-price");
            popupItemMaxQuantity = root.Q<Label>("popup-item-max-quantity");
            popupItemDesc = root.Q<Label>("popup-item-desc");
            btnPopupConfirm = root.Q<Button>("btn-popup-confirm");
            btnPopupCancel = root.Q<Button>("btn-popup-cancel");
            popupShapeGrid = root.Q<VisualElement>("popup-shape-grid");

            btnQuantityMinus = root.Q<Button>("btn-quantity-minus");
            btnQuantityPlus = root.Q<Button>("btn-quantity-plus");
            labelQuantity = root.Q<Label>("label-quantity");

            if (btnQuantityMinus != null)
            {
                btnQuantityMinus.clicked += () => 
                {
                    if (currentQuantity > 1)
                    {
                        currentQuantity--;
                        UpdateQuantityUI();
                    }
                };
            }
            if (btnQuantityPlus != null)
            {
                btnQuantityPlus.clicked += () => 
                {
                    if (currentQuantity < maxQuantity)
                    {
                        currentQuantity++;
                        UpdateQuantityUI();
                    }
                };
            }

            btnPopupConfirm.clicked += () => 
            {
                popupOverlay.style.display = DisplayStyle.None;
                if (currentSelectedItem != null)
                {
                    if (isCurrentlySelling)
                    {
                        OnSellItemClicked?.Invoke(currentSelectedItem, currentQuantity);
                    }
                    else
                    {
                        OnBuyItemClicked?.Invoke(currentSelectedItem, currentQuantity);
                    }
                }
            };
            btnPopupCancel.clicked += () => popupOverlay.style.display = DisplayStyle.None;

            // Tabs
            btnTabBuy = root.Q<Button>("btn-tab-buy");
            btnTabSell = root.Q<Button>("btn-tab-sell");

            btnTabBuy.pickingMode = PickingMode.Position;
            btnTabSell.pickingMode = PickingMode.Position;

            btnTabBuy.RegisterCallback<PointerDownEvent>(HandleBuyTabPointerDown);
            btnTabSell.RegisterCallback<PointerDownEvent>(HandleSellTabPointerDown);

            // Placement Actions
            mainActionButtons = root.Q<VisualElement>("main-action-buttons");
            placementActionButtons = root.Q<VisualElement>("placement-action-buttons");
            btnPlace = root.Q<Button>("btn-place");
            btnCancelPlace = root.Q<Button>("btn-cancel-place");

            mainActionButtons.pickingMode = PickingMode.Position;
            mainActionButtons.RegisterCallback<PointerDownEvent>(HandleMainActionButtonsPointerDown);
            mainActionButtons.style.display = DisplayStyle.None;
            placementActionButtons.style.display = DisplayStyle.None;
            
            // To be wired by Controller
        }

        public void ShowShop()
        {
            root.style.display = DisplayStyle.Flex;
            SetShopModeVisuals(false);
            FixedAspectRatioManager.RequestRefresh();
            
            if (backgroundDocument != null)
            {
                backgroundDocument.gameObject.SetActive(true);
            }

            if (mascotCanvas != null)
            {
                mascotCanvas.gameObject.SetActive(true); // 상점 열릴 때 마스코트 표시
            }

            SetDialogue("* \"Welcome to Pokopia, where every item has a tale!\"");
            SwitchToCatalogMode();

            // 상점이 열릴 때 퀵슬롯과 기존 인벤토리를 숨깁니다.
            if (QuickslotUIController.Instance != null && QuickslotUIController.Instance.GetComponent<UIDocument>() != null)
            {
                var quickslotRoot = QuickslotUIController.Instance.GetComponent<UIDocument>().rootVisualElement;
                if (quickslotRoot != null)
                {
                    quickslotRoot.style.display = DisplayStyle.None;
                    FixedAspectRatioManager.RequestRefresh();
                }
            }

            if (GridInventoryView.Instance != null && GridInventoryView.Instance.Container != null)
            {
                GridInventoryView.Instance.Container.style.display = DisplayStyle.None;
                FixedAspectRatioManager.RequestRefresh();
            }
        }

        public void HideShop()
        {
            root.style.display = DisplayStyle.None;
            FixedAspectRatioManager.RequestRefresh();

            if (backgroundDocument != null)
            {
                backgroundDocument.gameObject.SetActive(false);
            }

            if (mascotCanvas != null)
            {
                mascotCanvas.gameObject.SetActive(false); // 상점 닫힐 때 마스코트 숨김
            }

            // 상점이 닫힐 때 퀵슬롯을 다시 표시합니다.
            if (QuickslotUIController.Instance != null && QuickslotUIController.Instance.GetComponent<UIDocument>() != null)
            {
                var quickslotRoot = QuickslotUIController.Instance.GetComponent<UIDocument>().rootVisualElement;
                if (quickslotRoot != null)
                {
                    quickslotRoot.style.display = DisplayStyle.Flex;
                    FixedAspectRatioManager.RequestRefresh();
                }
            }
        }

        public void UpdateGold(int gold)
        {
            currentGold = gold;

            if (goldLabel != null)
                goldLabel.text = $"Gold: {gold}G";

            if (popupOverlay != null &&
                popupOverlay.style.display == DisplayStyle.Flex &&
                !isCurrentlySelling &&
                currentSelectedItem != null)
            {
                RefreshBuyQuantityLimit();
            }
        }

        public void SetDialogue(string text)
        {
        }

        public void RenderCatalog(List<ShopItemEntry> items, bool isSellMode = false)
        {
            catalogContainer.Clear();
            var template = root.Q<VisualElement>("item-card-template");

            foreach (var item in items)
            {
                // Clone the template manually or instantiate if it's a real template
                VisualElement card = new VisualElement();
                card.AddToClassList("item-card");

                VisualElement icon = new VisualElement();
                icon.AddToClassList("card-icon");
                if (item.ItemData.itemIcon != null)
                    icon.style.backgroundImage = new StyleBackground(item.ItemData.itemIcon);
                
                string displayName = item.ItemData.ItemNameString;
                if (isSellMode && item.StockCount > 1)
                {
                    displayName += $" x{item.StockCount}";
                }
                Label nameLabel = new Label(displayName);
                nameLabel.AddToClassList("card-name");
                
                Label priceLabel = new Label($"{item.BuyPrice}G");
                priceLabel.AddToClassList("card-price");

                card.Add(icon);
                card.Add(nameLabel);
                card.Add(priceLabel);

                card.RegisterCallback<ClickEvent>(evt => 
                {
                    if (isSellMode)
                        ShowSellPopup(item);
                    else
                        ShowBuyPopup(item);
                });

                catalogContainer.Add(card);
            }
        }

        private void ShowBuyPopup(ShopItemEntry item)
        {
            isCurrentlySelling = false;
            currentSelectedItem = item;
            maxQuantity = CalculateMaxBuyQuantity(item);
            currentQuantity = 1;
            
            popupItemName.text = item.ItemData.ItemNameString;
            popupItemPrice.text = $"{item.BuyPrice} Gold";
            popupItemDesc.text = item.ItemData.DescriptionString;
            UpdateMaxQuantityLabel();
            
            UpdateQuantityUI();
            
            // Render shape in popupShapeGrid if needed
            
            popupOverlay.style.display = DisplayStyle.Flex;
            FixedAspectRatioManager.RequestRefresh();
            SetDialogue("* \"Ah, standard equipment! A solid choice. Let me know if you want it.\"");
        }

        private void ShowSellPopup(ShopItemEntry item)
        {
            isCurrentlySelling = true;
            currentSelectedItem = item;
            currentQuantity = 1;
            maxQuantity = item.StockCount;
            
            popupItemName.text = item.ItemData.ItemNameString;
            popupItemPrice.text = $"{item.BuyPrice} Gold"; // Adjust for sell price logic
            popupItemDesc.text = item.ItemData.DescriptionString;
            if (popupItemMaxQuantity != null)
            {
                popupItemMaxQuantity.style.display = DisplayStyle.None;
            }
            
            UpdateQuantityUI();
            
            popupOverlay.style.display = DisplayStyle.Flex;
            FixedAspectRatioManager.RequestRefresh();
            SetDialogue("* \"What are you selling today? Let's see...\"");
        }
        
        private void UpdateQuantityUI()
        {
            if (labelQuantity != null)
            {
                labelQuantity.text = currentQuantity.ToString();
            }

            btnQuantityMinus?.SetEnabled(currentQuantity > 1);
            btnQuantityPlus?.SetEnabled(currentQuantity < maxQuantity);
            
            if (currentSelectedItem != null)
            {
                int totalPrice = currentSelectedItem.BuyPrice * currentQuantity;
                string actionText = isCurrentlySelling ? "SELL" : "BUY";
                if (btnPopupConfirm != null)
                {
                    btnPopupConfirm.text = $"[{actionText}]\nConfirm {totalPrice}G";
                    btnPopupConfirm.SetEnabled(currentQuantity > 0);
                }
            }
        }

        private void HandleBuyTabPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0)
                return;

            SelectBuyTab();
            evt.StopImmediatePropagation();
        }

        private void HandleSellTabPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0)
                return;

            SelectSellTab();
            evt.StopImmediatePropagation();
        }

        private void HandleMainActionButtonsPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || mainActionButtons == null)
                return;

            Vector2 pointerPosition = new Vector2(evt.position.x, evt.position.y);
            Vector2 localPosition = mainActionButtons.WorldToLocal(pointerPosition);
            float width = mainActionButtons.resolvedStyle.width;
            if (width <= 0f)
                width = mainActionButtons.layout.width;

            if (localPosition.x < width * 0.5f)
                SelectBuyTab();
            else
                SelectSellTab();

            evt.StopImmediatePropagation();
        }

        private void SelectBuyTab()
        {
            SetShopModeVisuals(false);
            OnBuyTabClicked?.Invoke();
        }

        private void SelectSellTab()
        {
            SetShopModeVisuals(true);
            OnSellTabClicked?.Invoke();
        }

        private void SetShopModeVisuals(bool isSellMode)
        {
            if (shopTitle != null)
            {
                shopTitle.text = isSellMode ? "SDI SHOP - SELL" : "SDI SHOP - BUY";
            }

            if (btnTabBuy != null && btnTabSell != null)
            {
                if (isSellMode)
                {
                    btnTabSell.AddToClassList("active");
                    btnTabBuy.RemoveFromClassList("active");
                }
                else
                {
                    btnTabBuy.AddToClassList("active");
                    btnTabSell.RemoveFromClassList("active");
                }
            }
        }

        private int CalculateMaxBuyQuantity(ShopItemEntry item)
        {
            if (item == null || item.ItemData == null)
                return 0;

            return Mathf.Max(1, item.ItemData.maxStackSize);
        }

        private void RefreshBuyQuantityLimit()
        {
            maxQuantity = CalculateMaxBuyQuantity(currentSelectedItem);
            currentQuantity = Mathf.Clamp(currentQuantity, 1, maxQuantity);
            UpdateMaxQuantityLabel();
            UpdateQuantityUI();
        }

        private void UpdateMaxQuantityLabel()
        {
            if (popupItemMaxQuantity == null)
                return;

            popupItemMaxQuantity.style.display = DisplayStyle.Flex;
            popupItemMaxQuantity.text = $"1회 최대 구매 : {maxQuantity}";
        }
        
        public void SwitchToPlacementMode()
        {
            CacheCatalogLeftPanelSize();

            catalogScroll.style.display = DisplayStyle.None;
            placementGridContainer.style.display = DisplayStyle.Flex;
            
            // Reuse the visible buy catalog panel size for placement mode.
            if (leftPanel != null)
            {
                leftPanel.style.flexGrow = 0;
                if (catalogLeftPanelWidth > 0f)
                {
                    leftPanel.style.width = catalogLeftPanelWidth;
                }

                if (catalogLeftPanelHeight > 0f)
                {
                    leftPanel.style.height = catalogLeftPanelHeight;
                }
            }

            placementGridContainer.style.flexGrow = 1;
            placementGridContainer.style.width = new StyleLength(new Length(100f, LengthUnit.Percent));
            placementGridContainer.style.height = new StyleLength(new Length(100f, LengthUnit.Percent));
            
            mainActionButtons.style.display = DisplayStyle.None;
            placementActionButtons.style.display = DisplayStyle.None;
            FixedAspectRatioManager.RequestRefresh();
            
            SetDialogue("* \"Find a good spot in your bag! Just click where you want to put it.\"");
        }

        public void SwitchToCatalogMode()
        {
            catalogScroll.style.display = DisplayStyle.Flex;
            placementGridContainer.style.display = DisplayStyle.None;

            if (leftPanel != null)
            {
                leftPanel.style.flexGrow = 1;
                leftPanel.style.width = new StyleLength(new Length(50f, LengthUnit.Percent));
                leftPanel.style.height = new StyleLength(StyleKeyword.Auto);
            }
            
            mainActionButtons.style.display = DisplayStyle.None;
            placementActionButtons.style.display = DisplayStyle.None;
            FixedAspectRatioManager.RequestRefresh();
        }

        private void CacheCatalogLeftPanelSize()
        {
            if (leftPanel == null)
            {
                return;
            }

            float width = leftPanel.resolvedStyle.width;
            if (float.IsNaN(width) || width <= 0f)
            {
                width = leftPanel.layout.width;
            }

            float height = leftPanel.resolvedStyle.height;
            if (float.IsNaN(height) || height <= 0f)
            {
                height = leftPanel.layout.height;
            }

            if (width > 0f)
            {
                catalogLeftPanelWidth = width;
            }

            if (height > 0f)
            {
                catalogLeftPanelHeight = height;
            }
        }
        
        public Button GetCancelPlaceButton() => btnCancelPlace;
        
        public VisualElement GetPlacementGridContainer() => placementGridContainer;
        
        public VisualElement GetRootVisualElement()
        {
            FixedAspectRatioManager.RequestRefresh();
            return root;
        }
    }
}
