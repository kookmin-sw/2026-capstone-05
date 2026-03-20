using System;
using System.Collections.Generic;

namespace Systems.Inventory {
    public class InventoryModel {
        public ObservableArray<Item> Items { get; set; }
        public int Width { get; private set; }
        public int Height { get; private set; }

        public event Action<Item[]> OnModelChanged {
            add => Items.AnyValueChanged += value;
            remove => Items.AnyValueChanged -= value;
        }
        
        public InventoryModel(IEnumerable<ItemDetails> itemDetails, int width, int height) {
            Width = width;
            Height = height;
            int capacity = width * height;
            Items = new ObservableArray<Item>(capacity);
            foreach (var itemDetail in itemDetails) {
                TryAdd(itemDetail.Create(1));
            }
        }
        
        public int GetIndex(int x, int y) => y * Width + x;
        public (int x, int y) GetCoordinates(int index) => (index % Width, index / Width);

        public Item Get(int x, int y) {
            if (IsOutOfBounds(x, y)) return null;
            return Items[GetIndex(x, y)];
        }
        
        public Item Get(int index) => Items[index];
        public void Clear() => Items.Clear();
        
        public bool TryAdd(Item item) {
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

        public bool TryRemove(Item item) {
            bool removed = false;
            // Items.Length (capacity)만큼 순회하도록 수정
            for (int i = 0; i < Width * Height; i++) {
                if (Items[i] == item) {
                    Items.SetSilent(i, null);
                    removed = true;
                }
            }
            if (removed) Items.Invoke();
            return removed;
        }

        public bool CanPlaceItem(Item item, int startX, int startY, Item ignoreItem = null) {
            int itemWidth = item.details.Width;
            int itemHeight = item.details.Height;

            if (startX < 0 || startY < 0 || startX + itemWidth > Width || startY + itemHeight > Height) {
                return false;
            }

            for (int x = startX; x < startX + itemWidth; x++) {
                for (int y = startY; y < startY + itemHeight; y++) {
                    var existingItem = Get(x, y);
                    if (existingItem != null && existingItem != ignoreItem) {
                        return false;
                    }
                }
            }
            return true;
        }

        public bool PlaceItem(Item item, int startX, int startY) {
            if (!CanPlaceItem(item, startX, startY)) return false;

            int itemWidth = item.details.Width;
            int itemHeight = item.details.Height;

            for (int x = startX; x < startX + itemWidth; x++) {
                for (int y = startY; y < startY + itemHeight; y++) {
                    Items.SetSilent(GetIndex(x, y), item);
                }
            }
            Items.Invoke();
            return true;
        }

        public void Swap(int sourceIndex, int targetIndex) {
            var sourceItem = Items[sourceIndex];
            var targetItem = Items[targetIndex];

            // 원래의 왼쪽 상단 좌표를 찾아둠
            int sourceOriginalIndex = sourceIndex;
            if (sourceItem != null) {
                for (int i = 0; i < Items.Length; i++) {
                    if (Items[i] == sourceItem) {
                        sourceOriginalIndex = i;
                        break;
                    }
                }
            }

            int targetOriginalIndex = targetIndex;
            if (targetItem != null) {
                for (int i = 0; i < Items.Length; i++) {
                    if (Items[i] == targetItem) {
                        targetOriginalIndex = i;
                        break;
                    }
                }
            }

            if (sourceItem == targetItem && sourceItem != null) {
                // 동일한 아이템 내에서의 스왑 시도는 무시 (예: 큰 아이템을 자기 자신의 다른 슬롯에 드롭)
                var pos = GetCoordinates(sourceOriginalIndex);
                TryRemove(sourceItem);
                
                // 새로운 targetIndex(드롭된 위치)를 기준으로 배치 가능한지 확인
                var newTargetPos = GetCoordinates(targetIndex);
                if (CanPlaceItem(sourceItem, newTargetPos.x, newTargetPos.y)) {
                    PlaceItem(sourceItem, newTargetPos.x, newTargetPos.y);
                } else {
                    PlaceItem(sourceItem, pos.x, pos.y);
                }
                return;
            }

            // 양쪽 위치 기록
            var sourcePos = GetCoordinates(sourceOriginalIndex);
            var targetPos = GetCoordinates(targetIndex);

            // 타겟 원본 위치
            var targetOriginalPos = GetCoordinates(targetOriginalIndex);

            if (sourceItem != null) TryRemove(sourceItem);
            if (targetItem != null) TryRemove(targetItem);

            bool canPlaceSourceAtTarget = sourceItem == null || CanPlaceItem(sourceItem, targetPos.x, targetPos.y);
            bool canPlaceTargetAtSource = targetItem == null || CanPlaceItem(targetItem, sourcePos.x, sourcePos.y);

            if (canPlaceSourceAtTarget && canPlaceTargetAtSource) {
                if (sourceItem != null) PlaceItem(sourceItem, targetPos.x, targetPos.y);
                if (targetItem != null) PlaceItem(targetItem, sourcePos.x, sourcePos.y);
            } else {
                // 원상복구
                if (sourceItem != null) PlaceItem(sourceItem, sourcePos.x, sourcePos.y);
                if (targetItem != null) PlaceItem(targetItem, targetOriginalPos.x, targetOriginalPos.y);
            }
        }
        
        public int Combine(int source, int target) {
            var total = Items[source].quantity + Items[target].quantity;
            Items[target].quantity = total;
            TryRemove(Items[source]);
            Items.Invoke();
            return total;
        }

        private bool IsOutOfBounds(int x, int y) {
            return x < 0 || y < 0 || x >= Width || y >= Height;
        }
    }
}