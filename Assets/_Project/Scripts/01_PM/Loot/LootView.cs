using System;
using UnityEngine;
using UnityEngine.UIElements;
using Systems.GridInventory;

namespace Systems.Loot
{
    public class LootView : MonoBehaviour
    {
        [SerializeField] private UIDocument document;

        public event Action OnCloseClicked;

        private VisualElement root;
        private VisualElement splitContainer;
        private VisualElement lootLeftPanel;
        private VisualElement lootCenterPanel;
        private VisualElement lootRightPanel;
        private Label headerLabel;
        private ScrollView lootScrollView;
        private VisualElement playerInventoryContainer;
        private VisualElement itemDetailContainer;
        private Label detailName;
        private Label detailType;
        private Label detailDesc;
        private Button btnClose;
        private bool initialized;

        public VisualElement GhostIcon { get; private set; }

        private void Awake()
        {
            if (document == null) document = GetComponent<UIDocument>();
        }

        public void Initialize()
        {
            if (initialized) return;

            QuantityPopupView.EnsureExists();

            if (document == null)
            {
                Debug.LogError("LootView: UIDocument is not assigned.");
                return;
            }

            root = document.rootVisualElement;
            root.style.display = DisplayStyle.None;
            document.sortingOrder = 7;

            splitContainer = root.Q<VisualElement>(className: "split-container");
            lootLeftPanel = root.Q<VisualElement>(className: "loot-left-panel");
            lootCenterPanel = root.Q<VisualElement>(className: "loot-center-panel");
            lootRightPanel = root.Q<VisualElement>(className: "loot-right-panel");
            headerLabel = root.Q<Label>("loot-header");
            lootScrollView = root.Q<ScrollView>("loot-scroll-view");
            playerInventoryContainer = root.Q<VisualElement>("player-inventory-container");
            itemDetailContainer = root.Q<VisualElement>("item-detail-container");
            detailName = root.Q<Label>("detail-name");
            detailType = root.Q<Label>("detail-type");
            detailDesc = root.Q<Label>("detail-desc");
            btnClose = root.Q<Button>("btn-close");
            GhostIcon = root.Q<VisualElement>("ghostIcon");

            if (btnClose != null)
            {
                btnClose.clicked += () => OnCloseClicked?.Invoke();
            }

            initialized = true;
        }

        public void Show()
        {
            if (root == null) Initialize();
            if (root == null) return;

            root.style.display = DisplayStyle.Flex;
            HideItemDetail();
        }

        public void Hide()
        {
            if (root != null)
            {
                root.style.display = DisplayStyle.None;
            }
        }

        public void SetLootMode()
        {
            SetInventoryOnlyMode(false);
        }

        public void SetInventoryOnlyMode(bool inventoryOnly)
        {
            if (root == null) Initialize();

            if (lootLeftPanel != null)
            {
                lootLeftPanel.style.display = inventoryOnly ? DisplayStyle.None : DisplayStyle.Flex;
            }

            if (lootCenterPanel != null)
            {
                lootCenterPanel.style.display = inventoryOnly ? DisplayStyle.None : DisplayStyle.Flex;
            }

            if (lootRightPanel != null)
            {
                lootRightPanel.style.display = DisplayStyle.Flex;
            }

            if (splitContainer != null)
            {
                splitContainer.style.justifyContent = inventoryOnly ? Justify.FlexEnd : Justify.SpaceBetween;
            }
        }

        public void SetLootHeader(string title)
        {
            if (headerLabel != null) headerLabel.text = title;
        }

        public void UpdateCapacities(int lootUsed, int lootMax, int invUsed, int invMax)
        {
        }

        public void ShowItemDetail(string name, string type, string desc)
        {
            if (detailName != null) detailName.text = name;
            if (detailType != null) detailType.text = type;
            if (detailDesc != null) detailDesc.text = desc;
            if (itemDetailContainer != null) itemDetailContainer.style.display = DisplayStyle.Flex;
        }

        public void HideItemDetail()
        {
            if (itemDetailContainer != null)
            {
                itemDetailContainer.style.display = DisplayStyle.None;
            }
        }

        public VisualElement GetPlayerInventoryContainer() => playerInventoryContainer;
        public ScrollView GetLootScrollView() => lootScrollView;
        public VisualElement GetRootVisualElement() => root;
    }
}
