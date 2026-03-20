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

            RegisterCallback<PointerDownEvent>(OnPointerDown);
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
            
            OnStartDrag.Invoke(evt.position, this);
            evt.StopPropagation();
        }
    }
}