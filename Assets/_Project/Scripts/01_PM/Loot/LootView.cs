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

        private Label headerLabel;

        private Label capacityLabel;

        private Label invCapacityLabel;

        

        private ScrollView lootScrollView;

        private VisualElement lootSlotsContainer;

        private VisualElement playerInventoryContainer;

        

        private VisualElement itemDetailContainer;

        private Label detailName;

        private Label detailType;

        private Label detailDesc;

        

        public VisualElement GhostIcon { get; private set; }

        

        private Button btnClose;

        

        private void Awake()

        {

            if (document == null) document = GetComponent<UIDocument>();

        }

        

        public void Initialize()

        {

            QuantityPopupView.EnsureExists();

            

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

            

            btnClose = root.Q<Button>("btn-close");



            GhostIcon = root.Q<VisualElement>("ghostIcon");



            btnClose.clicked += () => OnCloseClicked?.Invoke();

        }



        public void Show()

        {

            root.style.display = DisplayStyle.Flex;

            HideItemDetail();

        }



        public void Hide()

        {

            root.style.display = DisplayStyle.None;

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



        public VisualElement GetPlayerInventoryContainer() => playerInventoryContainer;

        public ScrollView GetLootScrollView() => lootScrollView;

        public VisualElement GetRootVisualElement() => root;

    }

}

