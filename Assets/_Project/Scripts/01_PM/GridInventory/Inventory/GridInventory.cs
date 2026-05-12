using System.Collections.Generic;
using UnityEngine;

namespace Systems.GridInventory {
    public class GridInventory : MonoBehaviour {
        [SerializeField] GridInventoryView view;
        private int gridWidth = 8;
        private int gridHeight = 8;
        
        // 아이템의 종류와 초기 수량을 지정하여 시작 시 모델에 삽입하기 위함
        [System.Serializable]
        public struct StartingItem {
            public ItemData itemData;
            public int quantity;
        }

        [SerializeField] List<StartingItem> startingItems = new List<StartingItem>();
        [SerializeField] private bool startEmptyOnJoin = true;

        GridInventoryController controller;
        public GridInventoryController Controller => controller;
        public static GridInventory Instance { get; private set; }

        void Awake() {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            controller = new GridInventoryController.Builder(view)
                .WithStartingItems(startEmptyOnJoin ? null : startingItems)
                .WithDimensions(gridWidth, gridHeight)
                .Build();
        }

        // 아이템 추가, 확인, 소모 등은 확장해야 할 Model 기능에 의존하므로
        // 임시로 ItemData 기반으로 수정해 둡니다.
        public bool AddItem(ItemData itemData, int quantity = 1) {
            return controller?.Model?.AddItemQuantity(itemData, quantity) ?? false;
        }

        public bool HasItem(ItemData itemData, int amount = 1) {
            return controller?.Model?.HasItem(itemData, amount) ?? false;
        }

        public bool ConsumeItem(ItemData itemData, int amount = 1) {
            return controller?.Model?.TryConsumeItem(itemData, amount) ?? false;
        }
    }
}

