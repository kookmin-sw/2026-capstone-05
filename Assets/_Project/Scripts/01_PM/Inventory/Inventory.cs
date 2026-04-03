using System.Collections.Generic;
using UnityEngine;

namespace Systems.Inventory {
    public class Inventory : MonoBehaviour {
        [SerializeField] InventoryView view;
        private int gridWidth = 8;
        private int gridHeight = 10;
        [SerializeField] List<ItemDetails> startingItems = new List<ItemDetails>();

        InventoryController controller;

        void Awake() {

            controller = new InventoryController.Builder(view)
                .WithStartingItems(startingItems)
                .WithDimensions(gridWidth, gridHeight)
                .Build();
        }

        public bool AddItem(ItemDetails itemDetails, int quantity = 1) {
            return controller?.Model?.AddItemQuantity(itemDetails, quantity) ?? false;
        }

        public bool HasItem(ItemDetails itemDetails, int amount = 1) {
            return controller?.Model?.HasItem(itemDetails, amount) ?? false;
        }

        public bool ConsumeItem(ItemDetails itemDetails, int amount = 1) {
            return controller?.Model?.TryConsumeItem(itemDetails, amount) ?? false;
        }
    }
}