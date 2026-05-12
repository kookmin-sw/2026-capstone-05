using UnityEngine;
using UnityEngine.UIElements;

namespace Systems.GridInventory {
    public static class GridInventoryDragHelper {
        public static float SlotSize { get; set; } = 100f;
        public static float SlotSpacing { get; set; } = 0f;
        public static float ItemPadding { get; set; } = 4f;

        public static void GetGhostSizeAndPivot(ItemInstance item, out float dragWidth, out float dragHeight, out float anchorXRatio, out float anchorYRatio) {
            var basePositions = item.Data.gridShape.GetRotatedPositions(ItemRotation.Deg0);
            int baseMinX = 0, baseMinY = 0, baseMaxX = 0, baseMaxY = 0;
            foreach(var pos in basePositions) {
                if(pos.x < baseMinX) baseMinX = pos.x;
                if(pos.y < baseMinY) baseMinY = pos.y;
                if(pos.x > baseMaxX) baseMaxX = pos.x;
                if(pos.y > baseMaxY) baseMaxY = pos.y;
            }
            int baseWidth = (baseMaxX - baseMinX) + 1;
            int baseHeight = (baseMaxY - baseMinY) + 1;
            
            float slotTotalSize = SlotSize + SlotSpacing;
            dragWidth = (baseWidth * slotTotalSize) - ItemPadding;
            dragHeight = (baseHeight * slotTotalSize) - ItemPadding;

            // 앵커(Pivot)는 원본(Deg0) 기준 0,0 셀의 정중앙이어야 합니다.
            // 아이템의 왼쪽 끝은 (ItemPadding / 2) 만큼 패딩이 들어가 있습니다.
            // 0,0 셀의 중앙은 로컬 좌표계에서 (-baseMinX * slotTotalSize) + (SlotSize / 2f) - (ItemPadding / 2f) 입니다.
            float anchorXPixel = (-baseMinX * slotTotalSize) + (SlotSize / 2f) - (ItemPadding / 2f);
            float anchorYPixel = (-baseMinY * slotTotalSize) + (SlotSize / 2f) - (ItemPadding / 2f);

            anchorXRatio = dragWidth > 0 ? anchorXPixel / dragWidth : 0f;
            anchorYRatio = dragHeight > 0 ? anchorYPixel / dragHeight : 0f;
        }

        public static void UpdateGhostPosition(VisualElement ghost, Vector2 screenPosition) {
            if (ghost == null) return;
            float baseW = ghost.style.width.value.value;
            float baseH = ghost.style.height.value.value;
            
            float pivotXRatio = ghost.style.transformOrigin.value.x.value / 100f;
            float pivotYRatio = ghost.style.transformOrigin.value.y.value / 100f;
            
            float pivotX = pivotXRatio * baseW;
            float pivotY = pivotYRatio * baseH;

            Vector2 localPos = ghost.parent.WorldToLocal(screenPosition);
            
            ghost.style.left = localPos.x - pivotX;
            ghost.style.top = localPos.y - pivotY;
        }

        public static void UpdateGhostPosition(VisualElement ghost, Vector2 screenPosition, Vector2 pointerOffset) {
            if (ghost == null || ghost.parent == null) return;

            Vector2 localPos = ghost.parent.WorldToLocal(screenPosition);
            ghost.style.left = localPos.x - pointerOffset.x;
            ghost.style.top = localPos.y - pointerOffset.y;
        }
    }
}
