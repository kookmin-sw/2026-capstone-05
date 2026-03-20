using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Systems.Inventory {
    public class InventoryController {
        readonly InventoryView view;
        readonly InventoryModel model;
        readonly int width;
        readonly int height;
        int Capacity => width * height;

        InventoryController(InventoryView view, InventoryModel model, int width, int height) {
            Debug.Assert(view != null, "View is null");
            Debug.Assert(model != null, "Model is null");
            Debug.Assert(width > 0 && height > 0, "Dimensions are less than 1");
            this.view = view;
            this.model = model;
            this.width = width;
            this.height = height;

            view.StartCoroutine(Initialize());
        }
        
        IEnumerator Initialize() {
            yield return view.Initialize(Capacity, width);

            view.OnDrop += HandleDrop;
            model.OnModelChanged += HandleModelChanged;

            RefreshView();
        }

        readonly Dictionary<Item, ItemView> itemViews = new Dictionary<Item, ItemView>();

        void HandleDrop(ItemView originalItemView, Slot closestSlot) {
            Item sourceItem = null;
            foreach (var kvp in itemViews) {
                if (kvp.Value == originalItemView) {
                    sourceItem = kvp.Key;
                    break;
                }
            }

            if (sourceItem == null) return;

            int sourceIndex = -1;
            for (int i = 0; i < Capacity; i++) {
                if (model.Get(i) == sourceItem) {
                    sourceIndex = i;
                    break;
                }
            }

            if (sourceIndex == -1) return;

            var targetItem = model.Get(closestSlot.Index);

            // Moving to Empty Slot
            if (targetItem == null) {
                // Ensure model properly removes/re-places considering items size
                model.Swap(sourceIndex, closestSlot.Index);
                return;
            }

            if (sourceItem.details.Id.Equals(targetItem.details.Id) && targetItem.details.maxStack > 1) {
                model.Combine(sourceIndex, closestSlot.Index);
            } else {
                model.Swap(sourceIndex, closestSlot.Index);
            }
        }

        void HandleModelChanged(IList<Item> items) => RefreshView();

        void RefreshView() {
            var currentItemsInModel = new HashSet<Item>();

            for (int i = 0; i < Capacity; i++) {
                var item = model.Get(i);
                if (item != null) {
                    currentItemsInModel.Add(item);
                }
            }

            var toRemove = new List<Item>();
            foreach (var kvp in itemViews) {
                if (!currentItemsInModel.Contains(kvp.Key)) {
                    view.RemoveItem(kvp.Value);
                    toRemove.Add(kvp.Key);
                }
            }
            foreach (var r in toRemove) {
                itemViews.Remove(r);
            }

            var processedItems = new HashSet<Item>();
            for (int i = 0; i < Capacity; i++) {
                var item = model.Get(i);
                if (item == null) continue;

                if (!processedItems.Contains(item)) {
                    processedItems.Add(item);

                    if (!itemViews.TryGetValue(item, out var itemView)) {
                        itemView = new ItemView(item.Id, item.details.Icon, item.details.Width, item.details.Height, item.quantity);
                        itemViews[item] = itemView;
                        view.BindItem(itemView, i);
                    } else {
                        itemView.SetQuantity(item.quantity);
                        view.UpdateItemPosition(itemView, i);
                        itemView.style.visibility = UnityEngine.UIElements.Visibility.Visible;
                    }
                }
            }
        }

        #region Builder

        public class Builder {
            InventoryView view;
            IEnumerable<ItemDetails> itemDetails;
            int width = 10;
            int height = 5;

            public Builder(InventoryView view) {
                this.view = view;
            }

            public Builder WithStartingItems(IEnumerable<ItemDetails> itemDetails) {
                this.itemDetails = itemDetails;
                return this;
            }

            public Builder WithDimensions(int width, int height) {
                this.width = width;
                this.height = height;
                return this;
            }

            public InventoryController Build() {
                InventoryModel model = itemDetails != null
                    ? new InventoryModel(itemDetails, width, height)
                    : new InventoryModel(Array.Empty<ItemDetails>(), width, height);

                return new InventoryController(view, model, width, height);
            }
        }

        #endregion Builder
    }
}
