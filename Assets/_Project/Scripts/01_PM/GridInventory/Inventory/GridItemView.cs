using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Systems.GridInventory {
    public class GridItemView : VisualElement {
        public Image Icon;
        public Label StackLabel;
        
        public ItemInstance ItemInst { get; private set; }
        
        public int Width { get; private set; }
        public int Height { get; private set; }
        
        public int MinX { get; private set; }
        public int MinY { get; private set; }
        
        public ItemRotation OriginalRotation { get; set; }
        public float VisualAngle { get; set; }

        public event Action<Vector2, GridItemView> OnStartDrag = delegate { };
        
        public static Action<GridItemView, Vector2> OnItemDragUpdateGlobal;
        public static Action<GridItemView, Vector2> OnItemDroppedGlobal;
        /// <summary>Emitted after drag is cancelled visually (no normal drop): right-click split flow.</summary>
        public static Action<GridItemView, Vector2> OnItemSplitDroppedGlobal;
        private int displayedQuantity = -1;

        public GridItemView(ItemInstance itemInst) {
            ItemInst = itemInst;
            
            // Setup styling for absolute positioning
            style.position = Position.Absolute;
            
            // Background / Icon
            Icon = new Image {
                sprite = itemInst.Data.itemIcon,
                scaleMode = ScaleMode.ScaleToFit
            };
            Icon.pickingMode = PickingMode.Ignore;
            Icon.style.flexGrow = 1;
            Icon.style.paddingLeft = Icon.style.paddingRight = Icon.style.paddingTop = Icon.style.paddingBottom = 2;
            
            Add(Icon);

            // Stack count
            StackLabel = new Label();
            StackLabel.pickingMode = PickingMode.Ignore;
            StackLabel.style.position = Position.Absolute;
            StackLabel.style.bottom = 2;
            StackLabel.style.right = 4;
            StackLabel.style.color = Color.white;
            StackLabel.style.fontSize = 14;
            StackLabel.style.textShadow = new TextShadow {
                blurRadius = 0,
                color = Color.black,
                offset = new Vector2(1, 1)
            };
            SetQuantity(itemInst.currentStackCount);
            Add(StackLabel);

            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            
            pickingMode = PickingMode.Position;
            
            VisualAngle = (int)ItemInst.currentRotation * 90f;
            RefreshVisuals();
        }

        public void RotateClockwise() {
            VisualAngle += 90f;
            ItemInst.Rotate(true);
            RefreshBoundsAndIcon();
        }

        public void RevertRotation(ItemRotation rotation) {
            ItemInst.currentRotation = rotation;
            VisualAngle = (int)rotation * 90f;
            RefreshBoundsAndIcon();
        }

        public void RefreshVisuals() {
            VisualAngle = (int)ItemInst.currentRotation * 90f;
            RefreshBoundsAndIcon();
        }

        private void RefreshBoundsAndIcon() {
            var rotatedPositions = ItemInst.Data.gridShape.GetRotatedPositions(ItemInst.currentRotation);
            
            int minX = 0, minY = 0, maxX = 0, maxY = 0;
            foreach(var pos in rotatedPositions) {
                if(pos.x < minX) minX = pos.x;
                if(pos.y < minY) minY = pos.y;
                if(pos.x > maxX) maxX = pos.x;
                if(pos.y > maxY) maxY = pos.y;
            }

            Width = (maxX - minX) + 1;
            Height = (maxY - minY) + 1;
            MinX = minX;
            MinY = minY;

            // Icon의 transformOrigin은 항상 "회전되지 않은 원본(Deg0) 기준"의 논리적 원점이어야 합니다.
            // 이미지는 항상 원본 상태로 그려진 후 VisualAngle만큼 회전되기 때문입니다.
            GridInventoryDragHelper.GetGhostSizeAndPivot(ItemInst, out float baseW, out float baseH, out float baseAnchorXRatio, out float baseAnchorYRatio);
            
            Icon.style.position = Position.Absolute;
            Icon.style.width = baseW;
            Icon.style.height = baseH;

            Icon.style.transformOrigin = new TransformOrigin(
                new Length(baseAnchorXRatio * 100f, LengthUnit.Percent), 
                new Length(baseAnchorYRatio * 100f, LengthUnit.Percent));

            // 논리적 앵커(0,0)가 부모(회전된 바운딩 박스)의 앵커(0,0) 위치와 일치하도록 오프셋을 계산합니다.
            // 부모 박스 내에서 앵커(0,0)의 위치:
            float slotSize = 100f;
            float slotSpacing = 0f; // 동기화됨: 슬롯 간격 0
            float rotatedAnchorX = (-minX * (slotSize + slotSpacing));
            float rotatedAnchorY = (-minY * (slotSize + slotSpacing));

        // Icon(자식)의 앵커(0,0)를 부모의 앵커 위치에 일치시키기 위한 left, top:
        // 아이템 패딩(4px)으로 인해 아이템 크기가 줄어들었으므로, 내부 아이콘 배치 시 이를 보정합니다.
        float itemPadding = 4f;
        float iconLeft = rotatedAnchorX - (baseAnchorXRatio * baseW - ((slotSize - itemPadding)/2f));
        float iconTop = rotatedAnchorY - (baseAnchorYRatio * baseH - ((slotSize - itemPadding)/2f));

            Icon.style.left = iconLeft;
            Icon.style.top = iconTop;

            // 누적 각도를 사용하여 항상 시계방향으로 회전 애니메이션 되도록 처리
            Icon.style.rotate = new Rotate(new Angle(VisualAngle));
            
            SetQuantity(ItemInst.currentStackCount);
        }
        
        private bool isDraggingThis = false;
        public bool IsDraggingThis => isDraggingThis;
        private int activePointerId = -1;
        private Action<GridItemView, Vector2> onDragMove;
        private Action<GridItemView> onDragEnd;
        
        public void SetDragCallbacks(Action<GridItemView, Vector2> onMove, Action<GridItemView> onEnd) {
            onDragMove = onMove;
            onDragEnd = onEnd;
        }

        public void SetQuantity(int qty) {
            if (displayedQuantity == qty) {
                return;
            }

            displayedQuantity = qty;
            StackLabel.text = qty > 1 ? qty.ToString() : string.Empty;
            StackLabel.visible = qty > 1;
        }

        public static bool IsAnyItemDragging { get; private set; }
        
        void OnPointerDown(PointerDownEvent evt) {
            if (evt.button != 0) return;
            
            isDraggingThis = true;
            IsAnyItemDragging = true;
            activePointerId = evt.pointerId;
            this.CapturePointer(evt.pointerId);
            
            OnStartDrag?.Invoke(evt.position, this);
            
            evt.StopPropagation();
        }

        /// <summary>Clears drag capture without invoking normal drop (used for right-click split).</summary>
        public void CancelDragState() {
            if (!isDraggingThis) return;
            isDraggingThis = false;
            IsAnyItemDragging = false;
            if (activePointerId >= 0) {
                this.ReleasePointer(activePointerId);
                activePointerId = -1;
            }
        }
        
        void OnPointerMove(PointerMoveEvent evt) {
            if (!isDraggingThis) return;
            
            onDragMove?.Invoke(this, evt.position);
            evt.StopPropagation();
        }
        
        void OnPointerUp(PointerUpEvent evt) {
            if (!isDraggingThis) return;
            
            isDraggingThis = false;
            IsAnyItemDragging = false;
            activePointerId = -1;
            this.ReleasePointer(evt.pointerId);
            
            onDragEnd?.Invoke(this);
            evt.StopPropagation();
        }

        public override bool ContainsPoint(Vector2 localPoint) {
            if (!base.ContainsPoint(localPoint)) return false;

            float slotSize = 100f;
            float slotSpacing = 0f;
            float slotTotalSize = slotSize + slotSpacing;

            int lx = Mathf.FloorToInt(localPoint.x / slotTotalSize);
            int ly = Mathf.FloorToInt(localPoint.y / slotTotalSize);
            
            if (lx < 0 || lx >= Width || ly < 0 || ly >= Height) return false;

            int itemX = lx + MinX;
            int itemY = ly + MinY;

            var rotatedPositions = ItemInst.Data.gridShape.GetRotatedPositions(ItemInst.currentRotation);
            foreach (var pos in rotatedPositions) {
                if (pos.x == itemX && pos.y == itemY) {
                    return true;
                }
            }

            return false;
        }
    }
}

