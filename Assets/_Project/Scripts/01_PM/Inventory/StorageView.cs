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

        protected VisualElement ghostIcon;
        protected VisualElement itemsContainer; // Overlay layer to hold ItemViews
        
        protected bool isDragging;
        protected ItemView draggedItem;
        
        protected VisualElement root;
        protected VisualElement container;

        protected float slotSize = 65f;
        protected float slotSpacing = 4f; // 2px margin * 2 = 4 (from CSS)
        
        protected int currentColumns = 8;
        private bool eventsRegistered = false;

        public event Action<ItemView, Slot> OnDrop;
        
        public IEnumerator Initialize(int size, int columns = 8) {
            this.currentColumns = columns;
            yield return StartCoroutine(InitializeView(size));
            
            // 이벤트 중복 등록 방지
            RegisterEventCallbacks();
        }
        
        void OnEnable() {
            // 드래그 상태 리셋 (플레이 모드 재시작 시)
            ResetDragState();
        }
        
        void OnDisable() {
            // 컴포넌트 비활성화 시 드래그 상태 정리
            ResetDragState();
        }
        
        void RegisterEventCallbacks() {
            // 더 이상 root에 이벤트 등록할 필요 없음 (ItemView에서 직접 처리)
            eventsRegistered = true;
        }
        
        void UnregisterEventCallbacks() {
            // ItemView에서 직접 처리하므로 특별한 정리 불필요
            eventsRegistered = false;
        }

        public abstract IEnumerator InitializeView(int size = 20);

        public void BindItem(ItemView itemView, int slotIndex) {
            if (itemsContainer == null) {
                return;
            }
            
            itemsContainer.Add(itemView);
            
            // 기존 이벤트 + 새로운 드래그 콜백 등록
            itemView.OnStartDrag += OnPointerDown;
            itemView.SetDragCallbacks(OnItemDragMove, OnItemDragEnd);

            UpdateItemPosition(itemView, slotIndex);
        }

        public void RemoveItem(ItemView itemView) {
            if (itemView != null && itemsContainer.Contains(itemView)) {
                itemView.OnStartDrag -= OnPointerDown;
                itemView.SetDragCallbacks(null, null);  // 콜백 제거
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
            Debug.Log($"OnPointerDown called - item: {item?.name}, ghostIcon: {ghostIcon != null}");
            
            if (ghostIcon == null || item == null) {
                Debug.LogError($"Cannot start drag - ghostIcon: {ghostIcon != null}, item: {item != null}");
                return;
            }
            
            isDragging = true;
            draggedItem = item;
            Debug.Log($"Drag started for item: {item.name}");
            SetGhostIconPosition(position, item);
            
            ghostIcon.style.backgroundImage = new StyleBackground(item.BaseSprite);
            ghostIcon.style.width = item.style.width;
            ghostIcon.style.height = item.style.height;

            item.style.visibility = Visibility.Hidden;
            ghostIcon.style.visibility = Visibility.Visible;
            ghostIcon.BringToFront();
        }
        
        // 새로운 드래그 시스템 콜백들
        void OnItemDragMove(ItemView item, Vector2 position) {
            if (!isDragging || draggedItem != item || ghostIcon == null) return;
            
            SetGhostIconPosition(position, item);
        }
        
        void OnItemDragEnd(ItemView item) {
            if (!isDragging || draggedItem != item) return;
            
            // 마지막 마우스 위치를 기준으로 드롭 시도
            Vector2 dropPosition = ghostIcon != null ? 
                new Vector2(ghostIcon.style.left.value.value + ghostIcon.layout.width/2, 
                           ghostIcon.style.top.value.value + ghostIcon.layout.height/2) : 
                Vector2.zero;
                           
            ProcessDrop(dropPosition);
        }
        
        void ProcessDrop(Vector2 position) {
            // itemsContainer 기준 좌표로 변환
            Vector2 localPos = itemsContainer.WorldToLocal(position);
            
            // 가장 가까운 슬롯 찾기
            Slot closestSlot = null;
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
                    closestSlot = Slots[i];
                    closestDistance = distance;
                }
            }
            
            if (closestSlot != null) {
                OnDrop?.Invoke(draggedItem, closestSlot);
            } else {
                if (draggedItem != null) {
                    draggedItem.style.visibility = Visibility.Visible;
                }
            }
            
            ResetDragState();
        }
        
        void SetGhostIconPosition(Vector2 position, ItemView originItem) {
            if(ghostIcon != null && originItem != null) {
                ghostIcon.style.top = position.y - (originItem.layout.height / 2);
                ghostIcon.style.left = position.x - (originItem.layout.width / 2);
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