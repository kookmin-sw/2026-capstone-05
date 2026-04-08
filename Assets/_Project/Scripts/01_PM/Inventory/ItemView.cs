using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Systems.Inventory {
    public class ItemView : VisualElement {
        public Image Icon;
        public Label StackLabel;
        public SerializableGuid ItemId { get; private set; }
        public Sprite BaseSprite;

        public int Width { get; private set; }
        public int Height { get; private set; }

        public event Action<Vector2, ItemView> OnStartDrag = delegate { };

        public ItemView(SerializableGuid id, Sprite icon, int width, int height, int qty = 1) {
            ItemId = id;
            BaseSprite = icon;
            Width = width;
            Height = height;

            // Setup styling for absolute positioning
            style.position = Position.Absolute;
            
            // Background / Icon
            Icon = new Image {
                sprite = BaseSprite,
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
            SetQuantity(qty);
            Add(StackLabel);

            // 드래그 이벤트 등록
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            
            // 드래그 가능하게 설정
            pickingMode = PickingMode.Position;
        }
        
        private bool isDraggingThis = false;
        private Vector2 dragStartPos;
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

        public void SetGridPosition(int col, int row, float slotSize, float spacing) {
            style.left = col * (slotSize + spacing);
            style.top = row * (slotSize + spacing);
            style.width = (Width * slotSize) + ((Width - 1) * spacing);
            style.height = (Height * slotSize) + ((Height - 1) * spacing);
        }

        void OnPointerDown(PointerDownEvent evt) {
            if (evt.button != 0) return;
            
            isDraggingThis = true;
            dragStartPos = evt.position;
            this.CapturePointer(evt.pointerId);
            
            // 기존 OnStartDrag 이벤트도 호출 (호환성을 위해)
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