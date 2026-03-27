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
    }
}