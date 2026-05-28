using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
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

        public static GridItemView CurrentDraggedItemView { get; protected set; }

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

        public DragSource SourceType { get; set; } = DragSource.Inventory;

        public int ModelColumns { get; set; } = 8;
        protected int currentColumns = 8;
        private readonly List<int> coloredSlotIndexes = new List<int>();
        private readonly HashSet<int> coloredSlotSet = new HashSet<int>();

        public bool IsHighlightTargetActive =>
            IsVisibleForHighlight(container) &&
            IsVisibleForHighlight(itemsContainer) &&
            Slots != null;

        private static bool IsVisibleForHighlight(VisualElement element) {
            return element != null &&
                   element.panel != null &&
                   element.resolvedStyle.display != DisplayStyle.None &&
                   element.resolvedStyle.visibility == Visibility.Visible;
        }

        public event Action<GridItemView, Vector2> OnDragUpdate;
        public event Action OnDragEndEvent;

        public void ResetAllSlotColors() {
            if (Slots == null) return;
            for (int i = 0; i < coloredSlotIndexes.Count; i++) {
                int index = coloredSlotIndexes[i];
                if (index >= 0 && index < Slots.Length && Slots[index] != null) {
                    Slots[index].style.backgroundColor = new StyleColor(StyleKeyword.Null);
                }
            }

            coloredSlotIndexes.Clear();
            coloredSlotSet.Clear();
        }

        public void SetSlotColor(int index, Color color) {
            if (index >= 0 && index < Slots.Length && Slots[index] != null) {
                Slots[index].style.backgroundColor = new StyleColor(color);
                if (coloredSlotSet.Add(index)) {
                    coloredSlotIndexes.Add(index);
                }
            }
        }

        public IEnumerator Initialize(int size, int columns = 8) {
            this.currentColumns = columns;
            this.ModelColumns = columns;
            yield return StartCoroutine(InitializeView(size));
        }

        void OnEnable() {
            ResetDragState();
        }

        void OnDisable() {
            ResetDragState();
            ItemDescriptionPanelController.HideImmediate();
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
                ItemDescriptionPanelController.Hide(itemView);
                itemView.OnStartDrag -= OnPointerDown;
                itemView.SetDragCallbacks(null, null);
                itemsContainer.Remove(itemView);
            }
        }

        public void UpdateItemPosition(GridItemView item, int slotIndex) {
            if (slotIndex >= 0 && slotIndex < Slots.Length) {
                int modelCol = slotIndex % ModelColumns;
                int modelRow = slotIndex / ModelColumns;

                int logicalLeftCol = modelCol + item.MinX;
                int logicalTopRow = modelRow + item.MinY;

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
            CurrentDraggedItemView = item;
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
            OnDragEndEvent?.Invoke();
        }

        protected virtual void Update() {
            if (isDragging && draggedItem != null && ghostIcon != null) {
                if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame) {
                    FinishDragForSplitRequest();
                    return;
                }

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
            float closestDistanceSqr = float.MaxValue;
            float maxDistanceSqr = slotSize * slotSize;

            for (int i = 0; i < Slots.Length; i++) {
                if (Slots[i] == null) continue;

                int modelCol = i % ModelColumns;
                int modelRow = i / ModelColumns;

                float slotCenterX = paddingLeftTop + modelCol * SlotTotalSize + (slotSize / 2f);
                float slotCenterY = paddingLeftTop + modelRow * SlotTotalSize + (slotSize / 2f);

                Vector2 slotCenter = new Vector2(slotCenterX, slotCenterY);
                float distanceSqr = (localPos - slotCenter).sqrMagnitude;

                if (distanceSqr < maxDistanceSqr && distanceSqr < closestDistanceSqr) {
                    closestGridSlot = Slots[i];
                    closestDistanceSqr = distanceSqr;
                }
            }
            return closestGridSlot;
        }


        public event Action<ItemInstance, Vector2> OnRouteDropRequested;

        protected virtual void ProcessDrop(Vector2 position) {
            if (draggedItem != null && draggedItem.ItemInst != null) {
                OnRouteDropRequested?.Invoke(draggedItem.ItemInst, position);
            }
            ResetDragState();
        }

        public void ForceResetDrag()
        {
            if (!isDragging || draggedItem == null) return;

            var item = draggedItem;
            ItemDescriptionPanelController.EndDragLock(item);
            item.CancelDragState();
            ResetDragState();
            item.style.visibility = Visibility.Visible;
        }

        /// <summary>Cancels drag without ProcessDrop; raises OnItemSplitDroppedGlobal for quantity split flow.</summary>
        protected void FinishDragForSplitRequest() {
            if (!isDragging || draggedItem == null) return;

            var item = draggedItem;
            var pos = currentPointerPos;
            ItemDescriptionPanelController.EndDragLock(item);
            item.CancelDragState();
            item.style.visibility = Visibility.Visible;
            if (ghostIcon != null) ghostIcon.style.visibility = Visibility.Hidden;

            ResetDragState();
            OnDragEndEvent?.Invoke();
            GridItemView.OnItemSplitDroppedGlobal?.Invoke(item, pos);
        }

        protected void ResetDragState() {
            if (draggedItem != null) {
                ItemDescriptionPanelController.EndDragLock(draggedItem);
            }
            isDragging = false;
            draggedItem = null;
            CurrentDraggedItemView = null;
            if (ghostIcon != null) {
                ghostIcon.style.visibility = Visibility.Hidden;
            }
        }
    }
}
