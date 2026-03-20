using System.Collections.Generic;
using UnityEngine;

namespace Systems.Inventory {
    public class Inventory : MonoBehaviour {
        [SerializeField] InventoryView view;
        [SerializeField, ReadOnly] int gridWidth = 8;
        [SerializeField, ReadOnly] int gridHeight = 10;
        [SerializeField] List<ItemDetails> startingItems = new List<ItemDetails>();

        InventoryController controller;

        void Awake() {
            // 인스펙터에 남은 과거 데이터 캐싱을 무시하고 강제로 8x10 적용
            gridWidth = 8;
            gridHeight = 10;

            controller = new InventoryController.Builder(view)
                .WithStartingItems(startingItems)
                .WithDimensions(gridWidth, gridHeight)
                .Build();
        }
    }
}