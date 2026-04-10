using UnityEngine;
using UnityEngine.UIElements;

namespace Systems.GridInventory {
    public static class GridInventoryDragHelper {
        public static float SlotSize { get; set; } = 65f;
        public static float SlotSpacing { get; set; } = 4f;

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
            
            anchorXRatio = (-baseMinX + 0.5f) / baseWidth;
            anchorYRatio = (-baseMinY + 0.5f) / baseHeight;

            dragWidth = (baseWidth * SlotSize) + ((baseWidth - 1) * SlotSpacing);
            dragHeight = (baseHeight * SlotSize) + ((baseHeight - 1) * SlotSpacing);
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
    }
}