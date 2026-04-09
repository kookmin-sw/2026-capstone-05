using UnityEngine;
using UnityEngine.UIElements;

namespace Systems.GridInventory {
    public class PanelDragManipulator : PointerManipulator {
        bool isDragging;
        Vector2 targetStartPosition;
        Vector2 pointerStartPosition;
        
        public PanelDragManipulator() {
            activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse });
        }
        
        protected override void RegisterCallbacksOnTarget() {
            target.RegisterCallback<PointerDownEvent>(OnPointerDown);
            target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            target.RegisterCallback<PointerUpEvent>(OnPointerUp);
        }

        protected override void UnregisterCallbacksFromTarget() {
            target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
            target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
            target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
        }

        void OnPointerDown(PointerDownEvent evt) {
            if (!CanStartManipulation(evt) || isDragging) return;

            // 기존 레이아웃(flexbox 등)에서 설정한 위치를 기준으로, 
            // 현재 수동으로 적용된 left/top 오프셋 값만 가져옵니다. (처음엔 0)
            float startLeft = target.style.left.keyword == StyleKeyword.Auto ? 0 : target.style.left.value.value;
            float startTop = target.style.top.keyword == StyleKeyword.Auto ? 0 : target.style.top.value.value;

            targetStartPosition = new Vector2(startLeft, startTop);
            pointerStartPosition = evt.position;
            isDragging = true;
            
            target.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        void OnPointerMove(PointerMoveEvent evt) {
            if (!isDragging || !target.HasPointerCapture(evt.pointerId)) return;

            Vector2 pointerDelta = (Vector2)evt.position - pointerStartPosition;
            target.style.left = targetStartPosition.x + pointerDelta.x;
            target.style.top = targetStartPosition.y + pointerDelta.y;
            
            evt.StopPropagation();
        }

        void OnPointerUp(PointerUpEvent evt) {
            if (!CanStopManipulation(evt) || !isDragging) return;
            
            isDragging = false;
            target.ReleasePointer(evt.pointerId);
            evt.StopPropagation();
        }
    }
}

