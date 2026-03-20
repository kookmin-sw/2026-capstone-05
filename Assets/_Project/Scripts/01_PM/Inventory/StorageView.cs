using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Systems.Inventory {
    public abstract class StorageView : MonoBehaviour {
        public Slot[] Slots;

        [SerializeField] protected UIDocument document;
        [SerializeField] protected StyleSheet styleSheet;

        protected static VisualElement ghostIcon;
        protected VisualElement itemsContainer; // Overlay layer to hold ItemViews
        
        static bool isDragging;
        static ItemView draggedItem;
        
        protected VisualElement root;
        protected VisualElement container;

        protected float slotSize = 65f;
        protected float slotSpacing = 4f; // 2px margin * 2 = 4 (from CSS)
        
        protected int currentColumns = 8;

        public event Action<ItemView, Slot> OnDrop;
        
        public IEnumerator Initialize(int size, int columns = 8) {
            this.currentColumns = columns;
            yield return StartCoroutine(InitializeView(size));
            
            if (ghostIcon != null) {
                ghostIcon.RegisterCallback<PointerMoveEvent>(OnPointerMove);
                ghostIcon.RegisterCallback<PointerUpEvent>(OnPointerUp);
            }
        }

        public abstract IEnumerator InitializeView(int size = 20);

        public void BindItem(ItemView itemView, int slotIndex) {
            itemsContainer.Add(itemView);
            itemView.OnStartDrag += OnPointerDown;

            UpdateItemPosition(itemView, slotIndex);
        }

        public void RemoveItem(ItemView itemView) {
            if (itemView != null && itemsContainer.Contains(itemView)) {
                itemView.OnStartDrag -= OnPointerDown;
                itemsContainer.Remove(itemView);
            }
        }

        public void UpdateItemPosition(ItemView item, int slotIndex) {
            if (slotIndex >= 0 && slotIndex < Slots.Length) {
                int col = slotIndex % currentColumns;
                int row = slotIndex / currentColumns;
                
                // 10px container padding + 2px slot margin = 12
                item.style.left = 12f + col * (slotSize + slotSpacing);
                item.style.top = 12f + row * (slotSize + slotSpacing);
                
                item.style.width = (item.Width * slotSize) + ((item.Width - 1) * slotSpacing);
                item.style.height = (item.Height * slotSize) + ((item.Height - 1) * slotSpacing);
            }
        }

        void OnPointerDown(Vector2 position, ItemView item) {
            isDragging = true;
            draggedItem = item;
        
            SetGhostIconPosition(position, item);
            
            ghostIcon.style.backgroundImage = new StyleBackground(item.BaseSprite);
            ghostIcon.style.width = item.style.width;
            ghostIcon.style.height = item.style.height;

            item.style.visibility = Visibility.Hidden;
            ghostIcon.style.visibility = Visibility.Visible;
            ghostIcon.BringToFront();
        }
        
        void OnPointerMove(PointerMoveEvent evt) {
            if (!isDragging) return;
            
            SetGhostIconPosition(evt.position, draggedItem);
        }

        void OnPointerUp(PointerUpEvent evt) {
            if (!isDragging) return;

            // Find slot at drop point (e.g. top-left of the item using ghost bounds or pointer offset)
            Vector2 checkPosition = new Vector2(
                ghostIcon.worldBound.xMin, 
                ghostIcon.worldBound.yMin
            );

            // Slightly adjust inwards to avoid edge snapping issues
            checkPosition.x += slotSize / 2f;
            checkPosition.y += slotSize / 2f;

            Slot closestSlot = Slots.FirstOrDefault(slot => slot.worldBound.Contains(checkPosition));
        
            if (closestSlot != null) {
                OnDrop?.Invoke(draggedItem, closestSlot);
            } else {
                draggedItem.style.visibility = Visibility.Visible; 
            }
            
            isDragging = false;
            draggedItem = null;
            ghostIcon.style.visibility = Visibility.Hidden;
        }
        
        static void SetGhostIconPosition(Vector2 position, ItemView originItem) {
            if(originItem != null) {
                ghostIcon.style.top = position.y - (originItem.layout.height / 2);
                ghostIcon.style.left = position.x - (originItem.layout.width / 2);
            }
        }
    }
}