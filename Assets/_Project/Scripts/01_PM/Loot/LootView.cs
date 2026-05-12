using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Systems.Loot
{
    public class LootView : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        
        public event Action OnTakeAllClicked;
        public event Action OnCloseClicked;
        public event Action<int> OnQuantitySplitConfirm;
        
        private VisualElement root;
        private Label headerLabel;
        private Label capacityLabel;
        private Label invCapacityLabel;
        
        private ScrollView lootScrollView;
        private VisualElement lootSlotsContainer;
        private VisualElement playerInventoryContainer;
        
        // Item Detail
        private VisualElement itemDetailContainer;
        private Label detailName;
        private Label detailType;
        private Label detailDesc;
        
        public VisualElement GhostIcon { get; private set; }
        
        private Button btnTakeAll;
        private Button btnClose;
        
        // Quantity Split Popup
        private VisualElement quantityPopup;
        private SliderInt sliderQty;
        private Label lblQtyValue;
        private Button btnQtyMinus;
        private Button btnQtyPlus;
        private Button btnQtyConfirm;
        private Button btnQtyCancel;
        
        private void Awake()
        {
            if (document == null) document = GetComponent<UIDocument>();
        }
        
        public void Initialize()
        {
            if (document == null)
            {
                Debug.LogError("LootView: document가 할당되지 않았습니다!");
                return;
            }

            root = document.rootVisualElement;
            root.style.display = DisplayStyle.None;
            document.sortingOrder = 7;

            headerLabel = root.Q<Label>("loot-header");
            capacityLabel = root.Q<Label>("loot-capacity");
            invCapacityLabel = root.Q<Label>("inventory-capacity");
            
            lootScrollView = root.Q<ScrollView>("loot-scroll-view");
            lootSlotsContainer = root.Q<VisualElement>("lootSlotsContainer");
            playerInventoryContainer = root.Q<VisualElement>("player-inventory-container");
            
            itemDetailContainer = root.Q<VisualElement>("item-detail-container");
            detailName = root.Q<Label>("detail-name");
            detailType = root.Q<Label>("detail-type");
            detailDesc = root.Q<Label>("detail-desc");
            
            btnTakeAll = root.Q<Button>("btn-take-all");
            btnClose = root.Q<Button>("btn-close");
            
            quantityPopup = root.Q<VisualElement>("quantity-popup");
            sliderQty = root.Q<SliderInt>("slider-qty");
            lblQtyValue = root.Q<Label>("lbl-qty-value");
            btnQtyMinus = root.Q<Button>("btn-qty-minus");
            btnQtyPlus = root.Q<Button>("btn-qty-plus");
            btnQtyConfirm = root.Q<Button>("btn-qty-confirm");
            btnQtyCancel = root.Q<Button>("btn-qty-cancel");

            GhostIcon = root.Q<VisualElement>("ghostIcon");

            btnTakeAll.clicked += () => OnTakeAllClicked?.Invoke();
            btnClose.clicked += () => OnCloseClicked?.Invoke();

            btnQtyMinus.clicked += () => {
                if (sliderQty.value > sliderQty.lowValue) sliderQty.value--;
            };
            btnQtyPlus.clicked += () => {
                if (sliderQty.value < sliderQty.highValue) sliderQty.value++;
            };
            sliderQty.RegisterValueChangedCallback(evt => {
                lblQtyValue.text = evt.newValue.ToString();
            });
            btnQtyCancel.clicked += () => quantityPopup.style.display = DisplayStyle.None;
            btnQtyConfirm.clicked += () => {
                quantityPopup.style.display = DisplayStyle.None;
                OnQuantitySplitConfirm?.Invoke(sliderQty.value);
            };
        }

        public void Show()
        {
            root.style.display = DisplayStyle.Flex;
            HideItemDetail();
        }

        public void Hide()
        {
            root.style.display = DisplayStyle.None;
            quantityPopup.style.display = DisplayStyle.None;
        }
        
        public void SetLootHeader(string title)
        {
            if (headerLabel != null) headerLabel.text = title;
        }

        public void UpdateCapacities(int lootUsed, int lootMax, int invUsed, int invMax)
        {
            if (capacityLabel != null) capacityLabel.text = $"{lootUsed} / {lootMax}";
            if (invCapacityLabel != null) invCapacityLabel.text = $"{invUsed} / {invMax}";
        }

        public void ShowItemDetail(string name, string type, string desc)
        {
            detailName.text = name;
            detailType.text = type;
            detailDesc.text = desc;
            itemDetailContainer.style.display = DisplayStyle.Flex;
        }

        public void HideItemDetail()
        {
            itemDetailContainer.style.display = DisplayStyle.None;
        }
        
        public void ShowQuantityPopup(int maxQuantity)
        {
            sliderQty.lowValue = 1;
            sliderQty.highValue = maxQuantity;
            sliderQty.value = 1;
            lblQtyValue.text = "1";
            quantityPopup.style.display = DisplayStyle.Flex;
        }

        public VisualElement GetPlayerInventoryContainer() => playerInventoryContainer;
        public ScrollView GetLootScrollView() => lootScrollView;
        public VisualElement GetRootVisualElement() => root;
    }
}
