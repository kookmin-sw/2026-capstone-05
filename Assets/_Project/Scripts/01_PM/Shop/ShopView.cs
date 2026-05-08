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
        public event Action OnExitClicked;
        public event Action OnBuyTabClicked;
        public event Action OnSellTabClicked;
        public event Action<int, int> OnGridCellClicked; // x, y
        
        private VisualElement root;
        private Label goldLabel;
        private Label dialogueText;
        private VisualElement catalogContainer;
        private ScrollView catalogScroll;
        private VisualElement placementGridContainer;
        private VisualElement placementGrid;
        
        // Popup
        private VisualElement popupOverlay;
        private Label popupItemName;
        private Label popupItemPrice;
        private Label popupItemDesc;
        private Button btnPopupConfirm;
        private Button btnPopupCancel;
        private VisualElement popupShapeGrid;
        
        // Tabs
        private Button btnTabBuy;
        private Button btnTabSell;
        private Button btnTabTalk;
        private Button btnTabExit;
        
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

            goldLabel = root.Q<Label>("gold-label");
            dialogueText = root.Q<Label>("dialogue-text");
            catalogContainer = root.Q<VisualElement>("catalog-container");
            catalogScroll = root.Q<ScrollView>("catalog-scroll");
            placementGridContainer = root.Q<VisualElement>("placement-grid-container");
            placementGrid = root.Q<VisualElement>("placement-grid");

            // Popup
            popupOverlay = root.Q<VisualElement>("item-detail-popup");
            popupItemName = root.Q<Label>("popup-item-name");
            popupItemPrice = root.Q<Label>("popup-item-price");
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
            btnTabTalk = root.Q<Button>("btn-tab-talk");
            btnTabExit = root.Q<Button>("btn-tab-exit");

            btnTabExit.clicked += () => OnExitClicked?.Invoke();

            btnTabBuy.clicked += () => 
            {
                btnTabBuy.AddToClassList("active");
                btnTabSell.RemoveFromClassList("active");
                OnBuyTabClicked?.Invoke();
            };

            btnTabSell.clicked += () => 
            {
                btnTabSell.AddToClassList("active");
                btnTabBuy.RemoveFromClassList("active");
                OnSellTabClicked?.Invoke();
            };

            // Placement Actions
            mainActionButtons = root.Q<VisualElement>("main-action-buttons");
            placementActionButtons = root.Q<VisualElement>("placement-action-buttons");
            btnPlace = root.Q<Button>("btn-place");
            btnCancelPlace = root.Q<Button>("btn-cancel-place");
            
            // To be wired by Controller
        }

        public void ShowShop()
        {
            root.style.display = DisplayStyle.Flex;
            
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
                }
            }

            if (GridInventoryView.Instance != null && GridInventoryView.Instance.Container != null)
            {
                GridInventoryView.Instance.Container.style.display = DisplayStyle.None;
            }
        }

        public void HideShop()
        {
            root.style.display = DisplayStyle.None;

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
                }
            }
        }

        public void UpdateGold(int gold)
        {
            if (goldLabel != null)
                goldLabel.text = $"Gold: {gold}g";
        }

        public void SetDialogue(string text)
        {
            if (dialogueText != null)
                dialogueText.text = text;
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
                
                string displayName = item.ItemData.itemName;
                if (isSellMode && item.StockCount > 1)
                {
                    displayName += $" x{item.StockCount}";
                }
                Label nameLabel = new Label(displayName);
                nameLabel.AddToClassList("card-name");
                
                Label priceLabel = new Label($"{item.BuyPrice}g");
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
            currentQuantity = 1;
            maxQuantity = item.ItemData.maxStackSize;
            
            popupItemName.text = item.ItemData.itemName;
            popupItemPrice.text = $"{item.BuyPrice} Gold";
            popupItemDesc.text = item.ItemData.description;
            
            UpdateQuantityUI();
            
            // Render shape in popupShapeGrid if needed
            
            popupOverlay.style.display = DisplayStyle.Flex;
            SetDialogue("* \"Ah, standard equipment! A solid choice. Let me know if you want it.\"");
        }

        private void ShowSellPopup(ShopItemEntry item)
        {
            isCurrentlySelling = true;
            currentSelectedItem = item;
            currentQuantity = 1;
            maxQuantity = item.StockCount;
            
            popupItemName.text = item.ItemData.itemName;
            popupItemPrice.text = $"{item.BuyPrice} Gold"; // Adjust for sell price logic
            popupItemDesc.text = item.ItemData.description;
            
            UpdateQuantityUI();
            
            popupOverlay.style.display = DisplayStyle.Flex;
            SetDialogue("* \"What are you selling today? Let's see...\"");
        }
        
        private void UpdateQuantityUI()
        {
            if (labelQuantity != null)
            {
                labelQuantity.text = currentQuantity.ToString();
            }
            
            if (currentSelectedItem != null)
            {
                int totalPrice = currentSelectedItem.BuyPrice * currentQuantity;
                string actionText = isCurrentlySelling ? "SELL" : "BUY";
                if (btnPopupConfirm != null)
                {
                    btnPopupConfirm.text = $"[{actionText}]\nConfirm {totalPrice}g";
                }
            }
        }
        
        public void SwitchToPlacementMode()
        {
            catalogScroll.style.display = DisplayStyle.None;
            placementGridContainer.style.display = DisplayStyle.Flex;
            
            // Flex-grow를 유지해서 placement-grid-container가 빈 공간을 다 채우게 설정
            placementGridContainer.style.flexGrow = 1;
            
            mainActionButtons.style.display = DisplayStyle.None;
            placementActionButtons.style.display = DisplayStyle.Flex;
            
            SetDialogue("* \"Find a good spot in your bag! Just click where you want to put it.\"");
        }

        public void SwitchToCatalogMode()
        {
            catalogScroll.style.display = DisplayStyle.Flex;
            placementGridContainer.style.display = DisplayStyle.None;
            
            mainActionButtons.style.display = DisplayStyle.Flex;
            placementActionButtons.style.display = DisplayStyle.None;
        }
        
        public Button GetCancelPlaceButton() => btnCancelPlace;
        
        public VisualElement GetPlacementGridContainer() => placementGridContainer;
        
        public VisualElement GetRootVisualElement() => root;
    }
}
