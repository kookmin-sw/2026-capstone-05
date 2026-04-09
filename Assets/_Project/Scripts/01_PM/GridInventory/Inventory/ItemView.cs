using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Systems.GridInventory {
    public class ItemView : VisualElement {
        public Image Icon;
        public Label StackLabel;
        
        public ItemInstance ItemInst { get; private set; }
        
        public int Width { get; private set; }
        public int Height { get; private set; }

        public event Action<Vector2, ItemView> OnStartDrag = delegate { };

        public ItemView(ItemInstance itemInst) {
            ItemInst = itemInst;
            
            // Setup styling for absolute positioning
            style.position = Position.Absolute;
            
            // Background / Icon
            Icon = new Image {
                sprite = itemInst.Data.itemIcon,
                scaleMode = ScaleMode.ScaleToFit
            };
            Icon.style.flexGrow = 1;
            Icon.style.paddingLeft = Icon.style.paddingRight = Icon.style.paddingTop = Icon.style.paddingBottom = 2;
            
            Add(Icon);

            // Stack count
            StackLabel = new Label();
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
            
            RefreshVisuals();
        }

        public void RefreshVisuals() {
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

            // Apply visual rotation to the Icon
            float rotationDegrees = (int)ItemInst.currentRotation * 90f;
            Icon.style.rotate = new Rotate(new Angle(rotationDegrees));
            
            SetQuantity(ItemInst.currentStackCount);
        }
        
        private bool isDraggingThis = false;
        private Action<ItemView, Vector2> onDragMove;
        private Action<ItemView> onDragEnd;
        
        public void SetDragCallbacks(Action<ItemView, Vector2> onMove, Action<ItemView> onEnd) {
            onDragMove = onMove;
            onDragEnd = onEnd;
        }

        public void SetQuantity(int qty) {
            StackLabel.text = qty > 1 ? qty.ToString() : string.Empty;
            StackLabel.visible = qty > 1;
        }

        void OnPointerDown(PointerDownEvent evt) {
            if (evt.button != 0) return;
            
            isDraggingThis = true;
            this.CapturePointer(evt.pointerId);
            
            OnStartDrag?.Invoke(evt.position, this);
            
            evt.StopPropagation();
        }
        
        void OnPointerMove(PointerMoveEvent evt) {
            if (!isDraggingThis) return;
            
            onDragMove?.Invoke(this, evt.position);
            evt.StopPropagation();
        }
        
        void OnPointerUp(PointerUpEvent evt) {
            if (!isDraggingThis) return;
            
            isDraggingThis = false;
            this.ReleasePointer(evt.pointerId);
            
            onDragEnd?.Invoke(this);
            evt.StopPropagation();
        }
    }
}
