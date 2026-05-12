using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Systems.GridInventory {
    public abstract class GridStorageView : MonoBehaviour {
        public GridSlot[] Slots;

        [SerializeField] protected UIDocument document;
        [SerializeField] protected StyleSheet styleSheet;

        public VisualElement GhostIcon {
            get => ghostIcon;
            set => ghostIcon = value;
        }

        protected VisualElement ghostIcon;
        protected VisualElement itemsContainer;

        protected bool isDragging;
        protected Vector2 currentPointerPos;
        protected GridItemView draggedItem;

        protected VisualElement root;
        protected VisualElement container;

        protected float slotSize = 100f;
        protected float slotSpacing = 0f;
        protected float paddingLeftTop = 10f;
        protected float itemPadding = 4f; // 아이템의 시각적 여백 (슬롯 마진을 없애고 아이템 크기를 줄임)

        protected float SlotTotalSize => slotSize + slotSpacing;

        protected int currentColumns = 8;

        public event Action<GridItemView, GridSlot> OnDrop;
        public event Action<GridItemView, int> OnDropToQuickslot;
        public event Action<GridItemView, Vector2> OnDragUpdate;
        public event Action OnDragEndEvent;

        public void ResetAllSlotColors() {
            if (Slots == null) return;
            foreach (var slot in Slots) {
                if (slot != null && slot.style != null) {
                    slot.style.backgroundColor = new StyleColor(StyleKeyword.Null);
                }
            }
        }

        public void SetSlotColor(int index, Color color) {
            if (index >= 0 && index < Slots.Length && Slots[index] != null) {
                Slots[index].style.backgroundColor = new StyleColor(color);
            }
        }

        public IEnumerator Initialize(int size, int columns = 8) {
            this.currentColumns = columns;
            yield return StartCoroutine(InitializeView(size));
        }

        void OnEnable() {
            ResetDragState();
        }

        void OnDisable() {
            ResetDragState();
        }

        public StyleSheet GridStyleSheet => styleSheet;

        public VisualElement Container { get { return container; } }

        public abstract IEnumerator InitializeView(int size = 20);

        public void BindItem(GridItemView itemView, int slotIndex) {
            if (itemsContainer == null) return;

            itemsContainer.Add(itemView);

            itemView.OnStartDrag += OnPointerDown;
            itemView.SetDragCallbacks(OnItemDragMove, OnItemDragEnd);

            UpdateItemPosition(itemView, slotIndex);
        }

        public void RemoveItem(GridItemView itemView) {
            if (itemView != null && itemsContainer.Contains(itemView)) {
                itemView.OnStartDrag -= OnPointerDown;
                itemView.SetDragCallbacks(null, null);
                itemsContainer.Remove(itemView);
            }
        }

        public void UpdateItemPosition(GridItemView item, int slotIndex) {
            if (slotIndex >= 0 && slotIndex < Slots.Length) {
                int col = slotIndex % currentColumns;
                int row = slotIndex / currentColumns;

                int logicalLeftCol = col + item.MinX;
                int logicalTopRow = row + item.MinY;

                item.style.left = paddingLeftTop + logicalLeftCol * SlotTotalSize + (itemPadding / 2f);
                item.style.top = paddingLeftTop + logicalTopRow * SlotTotalSize + (itemPadding / 2f);

                item.style.width = (item.Width * SlotTotalSize) - itemPadding;
                item.style.height = (item.Height * SlotTotalSize) - itemPadding;
            }
        }

        void OnPointerDown(Vector2 position, GridItemView item) {
            if (ghostIcon == null || item == null) return;

            isDragging = true;
            currentPointerPos = position;
            draggedItem = item;
            draggedItem.OriginalRotation = item.ItemInst.currentRotation;

            GridInventoryDragHelper.UpdateGhostPosition(ghostIcon, position);

            ghostIcon.style.backgroundImage = new StyleBackground(item.ItemInst.Data.itemIcon);
            ghostIcon.style.transformOrigin = item.Icon.style.transformOrigin;
            ghostIcon.style.rotate = new Rotate(new Angle(item.VisualAngle));
            ghostIcon.style.width = item.Icon.style.width;
            ghostIcon.style.height = item.Icon.style.height;

            item.style.visibility = Visibility.Hidden;
            ghostIcon.style.visibility = Visibility.Visible;
            ghostIcon.BringToFront();
            
            OnDragUpdate?.Invoke(item, position);
        }

        void OnItemDragMove(GridItemView item, Vector2 position) {
            if (!isDragging || draggedItem != item || ghostIcon == null) return;
            currentPointerPos = position;
            GridInventoryDragHelper.UpdateGhostPosition(ghostIcon, position);
            OnDragUpdate?.Invoke(item, position);
            GridItemView.OnItemDragUpdateGlobal?.Invoke(item, position);
        }

        void OnItemDragEnd(GridItemView item) {
            if (!isDragging || draggedItem != item) return;
            ProcessDrop(currentPointerPos);
            GridItemView.OnItemDroppedGlobal?.Invoke(item, currentPointerPos);
            OnDragEndEvent?.Invoke();
        }

        protected virtual void Update() {
            if (isDragging && draggedItem != null && ghostIcon != null) {
                if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.rKey.wasPressedThisFrame) {
                    draggedItem.RotateClockwise();

                    ghostIcon.style.rotate = new Rotate(new Angle(draggedItem.VisualAngle));
                    ghostIcon.style.transformOrigin = draggedItem.Icon.style.transformOrigin;
                    ghostIcon.style.width = draggedItem.Icon.style.width;
                    ghostIcon.style.height = draggedItem.Icon.style.height;

                    float newWidth = (draggedItem.Width * SlotTotalSize) - itemPadding;
                    float newHeight = (draggedItem.Height * SlotTotalSize) - itemPadding;

                    draggedItem.style.width = newWidth;
                    draggedItem.style.height = newHeight;

                    GridInventoryDragHelper.UpdateGhostPosition(ghostIcon, currentPointerPos);
                    OnDragUpdate?.Invoke(draggedItem, currentPointerPos);
                }
            }
        }

        public GridSlot GetGridSlotAtPosition(Vector2 position) {
            if (itemsContainer == null || Slots == null) return null;
            Vector2 localPos = itemsContainer.WorldToLocal(position);

            GridSlot closestGridSlot = null;
            float closestDistance = float.MaxValue;

            for (int i = 0; i < Slots.Length; i++) {
                if (Slots[i] == null) continue;

                int col = i % currentColumns;
                int row = i / currentColumns;

                float slotCenterX = paddingLeftTop + col * SlotTotalSize + (slotSize / 2f);
                float slotCenterY = paddingLeftTop + row * SlotTotalSize + (slotSize / 2f);

                Vector2 slotCenter = new Vector2(slotCenterX, slotCenterY);
                float distance = Vector2.Distance(localPos, slotCenter);

                if (distance < slotSize && distance < closestDistance) {
                    closestGridSlot = Slots[i];
                    closestDistance = distance;
                }
            }
            return closestGridSlot;
        }

        protected virtual void ProcessDrop(Vector2 position) {
            if (QuickslotUIController.Instance != null) {
                int quickslotIndex = QuickslotUIController.Instance.GetSlotIndexAtPosition(position);
                if (quickslotIndex >= 0) {
                    OnDropToQuickslot?.Invoke(draggedItem, quickslotIndex);
                    ResetDragState();
                    return;
                }
            }

            if (Systems.StorageSystem.StorageUI.ActiveInstance != null &&
                Systems.StorageSystem.StorageUI.ActiveInstance != this &&
                Systems.StorageSystem.StorageUI.ActiveInstance.IsOpen &&
                Systems.StorageSystem.StorageUI.ActiveInstance.TryAcceptExternalDrop(draggedItem, position)) {
                ResetDragState();
                return;
            }

            GridSlot closestGridSlot = GetGridSlotAtPosition(position);

            if (closestGridSlot != null) {
                OnDrop?.Invoke(draggedItem, closestGridSlot);
            } else {
                if (draggedItem != null) {
                    draggedItem.style.visibility = Visibility.Visible;
                }
            }

            ResetDragState();
        }

        protected void ResetDragState() {
            isDragging = false;
            draggedItem = null;
            if (ghostIcon != null) {
                ghostIcon.style.visibility = Visibility.Hidden;
            }
        }
    }
}
