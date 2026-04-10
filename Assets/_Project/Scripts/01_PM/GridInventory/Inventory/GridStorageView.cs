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

        protected VisualElement ghostIcon;
        protected VisualElement itemsContainer; // Overlay layer to hold ItemViews
        
        protected bool isDragging;
        protected Vector2 currentPointerPos;
        protected GridItemView draggedItem;
        
        protected VisualElement root;
        protected VisualElement container;

        protected float slotSize = 65f;
        protected float slotSpacing = 4f; // 2px margin * 2 = 4 (from CSS)
        
        protected int currentColumns = 8;
        private bool eventsRegistered = false;

        public event Action<GridItemView, GridSlot> OnDrop;
        public event Action<GridItemView, int> OnDropToQuickslot;
        
        public IEnumerator Initialize(int size, int columns = 8) {
            this.currentColumns = columns;
            yield return StartCoroutine(InitializeView(size));
            
            // 占싱븝옙트 占쌩븝옙 占쏙옙占?占쏙옙占쏙옙
            RegisterEventCallbacks();
        }
        
        void OnEnable() {
            // 占썲래占쏙옙 占쏙옙占쏙옙 占쏙옙占쏙옙 (占시뤄옙占쏙옙 占쏙옙占?占쏙옙占쏙옙占?占쏙옙)
            ResetDragState();
        }
        
        void OnDisable() {
            // 占쏙옙占쏙옙占쏙옙트 占쏙옙활占쏙옙화 占쏙옙 占썲래占쏙옙 占쏙옙占쏙옙 占쏙옙占쏙옙
            ResetDragState();
        }
        
        void RegisterEventCallbacks() {
            // 占쏙옙 占싱삼옙 root占쏙옙 占싱븝옙트 占쏙옙占쏙옙占?占십울옙 占쏙옙占쏙옙 (ItemView占쏙옙占쏙옙 占쏙옙占쏙옙 처占쏙옙)
            eventsRegistered = true;
        }
        
        void UnregisterEventCallbacks() {
            // ItemView占쏙옙占쏙옙 占쏙옙占쏙옙 처占쏙옙占싹므뤄옙 특占쏙옙占쏙옙 占쏙옙占쏙옙 占쏙옙占십울옙
            eventsRegistered = false;
        }

        public abstract IEnumerator InitializeView(int size = 20);

        public void BindItem(GridItemView itemView, int slotIndex) {
            if (itemsContainer == null) {
                return;
            }
            
            itemsContainer.Add(itemView);
            
            // 占쏙옙占쏙옙 占싱븝옙트 + 占쏙옙占싸울옙 占썲래占쏙옙 占쌥뱄옙 占쏙옙占?
            itemView.OnStartDrag += OnPointerDown;
            itemView.SetDragCallbacks(OnItemDragMove, OnItemDragEnd);

            UpdateItemPosition(itemView, slotIndex);
        }

        public void RemoveItem(GridItemView itemView) {
            if (itemView != null && itemsContainer.Contains(itemView)) {
                itemView.OnStartDrag -= OnPointerDown;
                itemView.SetDragCallbacks(null, null);  // 占쌥뱄옙 占쏙옙占쏙옙
                itemsContainer.Remove(itemView);
            }
        }

        public void UpdateItemPosition(GridItemView item, int slotIndex) {
            if (slotIndex >= 0 && slotIndex < Slots.Length) {
                int col = slotIndex % currentColumns;
                int row = slotIndex / currentColumns;
                
                // 10px container padding + 2px slot margin = 12
                // 논리적 앵커(0,0)의 위치가 col, row가 되게 하려면,
                // 박스 좌상단 기준점은 앵커(MinX, MinY)만큼 어긋나야 합니다.
                int logicalLeftCol = col + item.MinX;
                int logicalTopRow = row + item.MinY;

                item.style.left = 12f + logicalLeftCol * (slotSize + slotSpacing);
                item.style.top = 12f + logicalTopRow * (slotSize + slotSpacing);
                
                item.style.width = (item.Width * slotSize) + ((item.Width - 1) * slotSpacing);
                item.style.height = (item.Height * slotSize) + ((item.Height - 1) * slotSpacing);
            }
        }

        void OnPointerDown(Vector2 position, GridItemView item) {
            Debug.Log($"OnPointerDown called - item: {item?.name}, ghostIcon: {ghostIcon != null}");
            
            if (ghostIcon == null || item == null) {
                Debug.LogError($"Cannot start drag - ghostIcon: {ghostIcon != null}, item: {item != null}");
                return;
            }
            
            isDragging = true;
            currentPointerPos = position;
            draggedItem = item;
            draggedItem.OriginalRotation = item.ItemInst.currentRotation; // 드래그 시작 시 원본 회전각도 기록
            Debug.Log($"Drag started for item: {item.name}");
            SetGhostIconPosition(position, item);
            
            ghostIcon.style.backgroundImage = new StyleBackground(item.ItemInst.Data.itemIcon);
            ghostIcon.style.transformOrigin = item.Icon.style.transformOrigin;
            ghostIcon.style.rotate = new Rotate(new Angle(item.VisualAngle));
            ghostIcon.style.width = item.Icon.style.width;
            ghostIcon.style.height = item.Icon.style.height;

            item.style.visibility = Visibility.Hidden;
            ghostIcon.style.visibility = Visibility.Visible;
            ghostIcon.BringToFront();
        }
        
        // 占쏙옙占싸울옙 占썲래占쏙옙 占시쏙옙占쏙옙 占쌥뱄옙占?
        void OnItemDragMove(GridItemView item, Vector2 position) {
            if (!isDragging || draggedItem != item || ghostIcon == null) return;
                        currentPointerPos = position;            SetGhostIconPosition(position, item);
        }
        
        void OnItemDragEnd(GridItemView item) {
            if (!isDragging || draggedItem != item) return;
            
            // SetGhostIconPosition을 통해 이제 항상 마우스 커서(currentPointerPos)가
            // ghostIcon의 기준점(0,0 타일의 중앙)에 위치하도록 만들어두었습니다.
            // 따라서 떨어뜨리는 World 좌표 위치는 단순히 현재 마우스 커서의 위치가 됩니다.
            Vector2 dropPosition = currentPointerPos;
                           
            ProcessDrop(dropPosition);
        }
        
        protected virtual void Update() {
            // R키 회전 기믹 구현
            if (isDragging && draggedItem != null && ghostIcon != null) {
                if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.rKey.wasPressedThisFrame) {
                    draggedItem.RotateClockwise();
                    
                    var positions = draggedItem.ItemInst.Data.gridShape.GetRotatedPositions(draggedItem.ItemInst.currentRotation);
                    string posStr = string.Join(", ", positions);
                    Debug.Log($"[Inventory Rotate] Angle: {(int)draggedItem.ItemInst.currentRotation * 90}도 ({draggedItem.ItemInst.currentRotation}), Occupied Cells (Relative): [{posStr}]");

                    ghostIcon.style.rotate = new Rotate(new Angle(draggedItem.VisualAngle));
                    ghostIcon.style.transformOrigin = draggedItem.Icon.style.transformOrigin;
                    
                    // ghostIcon은 Icon과 동일한 크기와 배치를 가져야 올바르게 표시됩니다.
                    ghostIcon.style.width = draggedItem.Icon.style.width;
                    ghostIcon.style.height = draggedItem.Icon.style.height;
                    
                    float newWidth = (draggedItem.Width * slotSize) + ((draggedItem.Width - 1) * slotSpacing);
                    float newHeight = (draggedItem.Height * slotSize) + ((draggedItem.Height - 1) * slotSpacing);
                    
                    draggedItem.style.width = newWidth;
                    draggedItem.style.height = newHeight;
                    
                    // 회전 후 바운드 크기 및 오프셋이 변경되었으므로 현재 마우스 위치 기준으로 UI를 즉시 재정렬합니다.
                    SetGhostIconPosition(currentPointerPos, draggedItem);
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
                
                float slotCenterX = 12f + col * (slotSize + slotSpacing) + (slotSize / 2);
                float slotCenterY = 12f + row * (slotSize + slotSpacing) + (slotSize / 2);
                
                Vector2 slotCenter = new Vector2(slotCenterX, slotCenterY);
                float distance = Vector2.Distance(localPos, slotCenter);
                
                if (distance < slotSize && distance < closestDistance) {
                    closestGridSlot = Slots[i];
                    closestDistance = distance;
                }
            }
            return closestGridSlot;
        }

        void ProcessDrop(Vector2 position) {
            if (QuickslotUIController.Instance != null) {
                int quickslotIndex = QuickslotUIController.Instance.GetSlotIndexAtPosition(position);
                if (quickslotIndex >= 0) {
                    OnDropToQuickslot?.Invoke(draggedItem, quickslotIndex);
                    ResetDragState();
                    return;
                }
            }

            Vector2 localPos = itemsContainer.WorldToLocal(position);
            
            // 가장 가까운 슬롯 찾기
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
        
        void SetGhostIconPosition(Vector2 position, GridItemView originItem) {
            if(ghostIcon != null && originItem != null) {
                GridInventoryDragHelper.UpdateGhostPosition(ghostIcon, position);
            }
        }
        
        protected void ResetDragState() {
            isDragging = false;
            draggedItem = null;
            if (ghostIcon != null) {
                ghostIcon.style.visibility = Visibility.Hidden;
            }
        }
        
        void OnDestroy() {
            UnregisterEventCallbacks();
        }
    }
}


