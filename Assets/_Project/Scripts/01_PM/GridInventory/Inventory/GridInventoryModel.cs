using System;
using System.Collections.Generic;
using UnityEngine;

namespace Systems.GridInventory {
    public class GridInventoryModel {
        public ObservableArray<ItemInstance> Items { get; set; }
        private Dictionary<ItemInstance, Vector2Int> itemAnchors = new Dictionary<ItemInstance, Vector2Int>();
        
        public int Width { get; private set; }
        public int Height { get; private set; }
        
        public int Gold { get; private set; }

        public event Action<int> OnGoldChanged;

        public event Action<ItemInstance[]> OnModelChanged {
            add => Items.AnyValueChanged += value;
            remove => Items.AnyValueChanged -= value;
        }
        
        public GridInventoryModel(int width, int height) {
            Width = width;
            Height = height;
            Gold = 500;
            int capacity = width * height;
            Items = new ObservableArray<ItemInstance>(capacity);
        }
        
        public int GetIndex(int x, int y) => y * Width + x;
        public (int x, int y) GetCoordinates(int index) => (index % Width, index / Width);

        public ItemInstance Get(int x, int y) {
            if (IsOutOfBounds(x, y)) return null;
            return Items[GetIndex(x, y)];
        }
        
        public ItemInstance Get(int index) {
            if (index < 0 || index >= Items.Length) return null;
            return Items[index];
        }

        public void Clear() {
            for (int i = 0; i < Items.Length; i++)
            {
                Items.SetSilent(i, null);
            }
            itemAnchors.Clear();
            Items.Invoke();
        }
        
        public void SetGold(int amount) {
            Gold = amount;
            OnGoldChanged?.Invoke(Gold);
        }

        public void AddGold(int amount) {
            Gold += amount;
            OnGoldChanged?.Invoke(Gold);
        }

        public bool TrySpendGold(int amount) {
            if (Gold >= amount) {
                Gold -= amount;
                OnGoldChanged?.Invoke(Gold);
                return true;
            }
            return false;
        }
        
        public bool TryAdd(ItemInstance item) {
            for (int y = 0; y < Height; y++) {
                for (int x = 0; x < Width; x++) {
                    if (CanPlaceItem(item, x, y)) {
                        PlaceItem(item, x, y);
                        return true;
                    }
                }
            }
            return false;
        }

        public bool TryRemove(ItemInstance item) {
            bool removed = false;
            for (int i = 0; i < Width * Height; i++) {
                if (Items[i] == item) {
                    Items.SetSilent(i, null);
                    removed = true;
                }
            }
            if (removed) {
                itemAnchors.Remove(item);
                Items.Invoke();
            }
            return removed;
        }

        public void EnsureDimensions(int width, int height) {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            if (Width == width && Height == height) return;

            if (Items.Length != width * height) {
                Debug.LogWarning($"[Inventory] Cannot resize grid from {Width}x{Height} to {width}x{height}: capacity differs.");
                return;
            }

            var oldItems = new List<ItemInstance>();
            var processedItems = new HashSet<ItemInstance>();
            for (int i = 0; i < Items.Length; i++) {
                var item = Items[i];
                if (item != null && processedItems.Add(item)) {
                    oldItems.Add(item);
                }
                Items.SetSilent(i, null);
            }

            var oldAnchors = new Dictionary<ItemInstance, Vector2Int>(itemAnchors);
            itemAnchors.Clear();
            Width = width;
            Height = height;

            foreach (var item in oldItems) {
                if (item == null) continue;

                if (oldAnchors.TryGetValue(item, out var oldAnchor) && CanPlaceItem(item, oldAnchor.x, oldAnchor.y)) {
                    PlaceItem(item, oldAnchor.x, oldAnchor.y);
                }
                else if (!TryAdd(item)) {
                    Debug.LogWarning($"[Inventory] Failed to relocate item while resizing grid: {item.Data?.itemID}");
                }
            }

            Items.Invoke();
        }

        public void Transpose(bool clockwise) {
            var oldItems = new List<ItemInstance>();
            var processedItems = new HashSet<ItemInstance>();
            for (int i = 0; i < Items.Length; i++) {
                var item = Items[i];
                if (item != null && !processedItems.Contains(item)) {
                    oldItems.Add(item);
                    processedItems.Add(item);
                }
                Items.SetSilent(i, null);
            }
            
            int oldWidth = Width;
            int oldHeight = Height;
            
            Width = oldHeight;
            Height = oldWidth;
            
            var oldAnchors = new Dictionary<ItemInstance, Vector2Int>(itemAnchors);
            itemAnchors.Clear();
            
            foreach (var item in oldItems) {
                if (oldAnchors.TryGetValue(item, out var oldAnchor)) {
                    int newX = clockwise ? oldHeight - 1 - oldAnchor.y : oldAnchor.y;
                    int newY = clockwise ? oldAnchor.x : oldWidth - 1 - oldAnchor.x;
                    
                    item.Rotate(clockwise);
                    
                    var positions = item.Data.gridShape.GetRotatedPositions(item.currentRotation);
                    foreach (var pos in positions) {
                        int px = newX + pos.x;
                        int py = newY + pos.y;
                        Items.SetSilent(GetIndex(px, py), item);
                    }
                    itemAnchors[item] = new Vector2Int(newX, newY);
                }
            }
            
            Items.Invoke();
        }

        public bool CanPlaceItem(ItemInstance item, int startX, int startY, ItemInstance ignoreItem = null, ItemInstance ignoreItem2 = null) {
            if (item == null || item.Data == null || item.Data.gridShape == null) return false;

            var positions = item.Data.gridShape.GetRotatedPositions(item.currentRotation);

            foreach (var pos in positions) {
                int px = startX + pos.x;
                int py = startY + pos.y;

                if (IsOutOfBounds(px, py)) {
                    return false;
                }

                var existingItem = Get(px, py);
                if (existingItem != null && existingItem != item) {
                    if (ignoreItem != null && existingItem == ignoreItem) continue;
                    if (ignoreItem2 != null && existingItem == ignoreItem2) continue;
                    return false;
                }
            }
            return true;
        }

        public bool PlaceItem(ItemInstance item, int startX, int startY) {
            if (!CanPlaceItem(item, startX, startY)) return false;

            var positions = item.Data.gridShape.GetRotatedPositions(item.currentRotation);

            foreach (var pos in positions) {
                int px = startX + pos.x;
                int py = startY + pos.y;
                Items.SetSilent(GetIndex(px, py), item);
            }
            itemAnchors[item] = new Vector2Int(startX, startY);
            Items.Invoke();
            return true;
        }

        public (int x, int y) GetItemAnchorPosition(ItemInstance item) {
            if (item != null && itemAnchors.TryGetValue(item, out var pos)) {
                return (pos.x, pos.y);
            }
            return (-1, -1);
        }

        public (int x, int y) GetItemTrueAnchorPosition(ItemInstance item, int foundX, int foundY, Vector2Int foundLocalPos) {
            return (foundX - foundLocalPos.x, foundY - foundLocalPos.y);
        }

        public bool AddItemQuantity(ItemData itemData, int amount = 1) {
            // Find existing items to stack if possible
            for (int i = 0; i < Items.Length; i++) {
                var item = Items[i];
                if (item != null && item.Data == itemData && item.currentStackCount < itemData.maxStackSize) {
                    int spaceLeft = itemData.maxStackSize - item.currentStackCount;
                    if (spaceLeft >= amount) {
                        item.currentStackCount += amount;
                        Items.Invoke();
                        return true;
                    } else {
                        item.currentStackCount += spaceLeft;
                        amount -= spaceLeft;
                        Items.Invoke();
                    }
                }
            }

            // Create new item instances for the remaining amount
            while (amount > 0) {
                int addAmount = Math.Min(amount, itemData.maxStackSize);
                var newItem = new ItemInstance(itemData, addAmount);
                if (!TryAdd(newItem)) {
                    return false;
                }
                amount -= addAmount;
            }
            return true;
        }

        public bool HasItem(ItemData itemData, int amount = 1) {
            int count = 0;
            var processedItems = new HashSet<ItemInstance>();
            for (int i = 0; i < Items.Length; i++) {
                var item = Items[i];
                if (item != null && item.Data == itemData && !processedItems.Contains(item)) {
                    processedItems.Add(item);
                    count += item.currentStackCount;
                    if (count >= amount) return true;
                }
            }
            return false;
        }

        public bool TryConsumeItem(ItemData itemData, int amount = 1) {
            if (!HasItem(itemData, amount)) return false;

            int remaining = amount;
            var processedItems = new HashSet<ItemInstance>();
            var itemsToConsume = new List<ItemInstance>();
            
            for (int i = 0; i < Items.Length; i++) {
                var item = Items[i];
                if (item != null && item.Data == itemData && !processedItems.Contains(item)) {
                    processedItems.Add(item);
                    itemsToConsume.Add(item);
                }
            }

            foreach (var item in itemsToConsume) {
                if (item.currentStackCount > remaining) {
                    item.currentStackCount -= remaining;
                    remaining = 0;
                    Items.Invoke();
                    break;
                } else {
                    remaining -= item.currentStackCount;
                    TryRemove(item);
                    if (remaining <= 0) break;
                }
            }
            return true;
        }

        private bool IsOutOfBounds(int x, int y) {
            return x < 0 || y < 0 || x >= Width || y >= Height;
        }
    }
}

