using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Systems.GridInventory {
    public class GridInventoryView : GridStorageView {
        [SerializeField] string panelName = "GridInventory";

        public static bool IsAnyInventoryOpen { get; private set; }

        public event System.Action OnSaveClicked;
        public event System.Action OnLoadClicked;

        public override IEnumerator InitializeView(int size = 20) {
            Slots = new GridSlot[size];
            root = document.rootVisualElement;

            container = root.Q<VisualElement>(className: "container");
            if (container == null) {
                // 대비책 (UXML이 비어있을 경우)
                root.Clear();
                root.styleSheets.Add(styleSheet);
                container = root.CreateChild("container");
                var inv = container.CreateChild("inventory-window");
                inv.CreateChild("inventoryHeader").Add(new Label(panelName.ToUpper()));
                var sContainer = inv.CreateChild("slotsContainer");
                sContainer.style.width = (currentColumns * 69f) + 22f;
                int rows = Mathf.CeilToInt((float)size / currentColumns);
                sContainer.style.height = (rows * 69f) + 22f;
                for (int i = 0; i < size; i++) {
                    var slot = new GridSlot();
                    slot.name = "slot";
                    slot.AddToClassList("slot");
                    sContainer.Add(slot);
                    Slots[i] = slot;
                }

                // Append the specialized overlay itemsContainer inside the slotsContainer
                itemsContainer = sContainer.CreateChild("itemsContainer");
                itemsContainer.style.position = Position.Absolute;
                itemsContainer.style.top = 0;
                itemsContainer.style.left = 0;
                itemsContainer.style.right = 0;
                itemsContainer.style.bottom = 0;

                ghostIcon = container.CreateChild("ghostIcon");
                ghostIcon.AddToClassList("ghostIcon");
                ghostIcon.style.position = Position.Absolute;
                ghostIcon.style.visibility = Visibility.Hidden;
                ghostIcon.pickingMode = PickingMode.Ignore;  // 마우스 이벤트 방지
                yield break;
            }
            
            var inventory = container.Q<VisualElement>(name: "inventory-window");
            
            var headerLabel = inventory.Q<Label>(name: "inventoryHeader");
            if (headerLabel != null) {
                headerLabel.text = panelName.ToUpper();
            }

            var slotsContainer = inventory.Q<VisualElement>(name: "slotsContainer");
            
            // 동적으로 가로 폭 계산 (슬롯크기 65 + 양옆마진 4 = 69) * 컬럼수 + 패딩 20 + 테두리 2 = 딱 맞는 사이즈
            slotsContainer.style.width = (currentColumns * 69f) + 22f;
            
            // 세로 폭 동적 계산 (빈 공간 최소화)
            int rowsForUpdate = Mathf.CeilToInt((float)size / currentColumns);
            slotsContainer.style.height = (rowsForUpdate * 69f) + 22f;
            
            var existingSlots = slotsContainer.Query<GridSlot>().ToList();

            if (existingSlots.Count >= size) {
                for (int i = 0; i < size; i++) {
                    Slots[i] = existingSlots[i];
                }
            } else {
                for (int i = 0; i < existingSlots.Count; i++) {
                    Slots[i] = existingSlots[i];
                }
                for (int i = existingSlots.Count; i < size; i++) {
                    var slot = new GridSlot();
                    slot.name = "slot";
                    slot.AddToClassList("slot");
                    slotsContainer.Add(slot);
                    Slots[i] = slot;
                }
            }

            // Also attach/re-attach the itemsContainer
            itemsContainer = slotsContainer.Q<VisualElement>("itemsContainer") ?? slotsContainer.CreateChild("itemsContainer");
            itemsContainer.style.position = Position.Absolute;
            itemsContainer.style.top = 0;
            itemsContainer.style.left = 0;
            
            ghostIcon = container.Q<VisualElement>(className: "ghostIcon");
            if (ghostIcon == null) {
                ghostIcon = container.CreateChild("ghostIcon");
                ghostIcon.AddToClassList("ghostIcon");
                ghostIcon.style.position = Position.Absolute;
                ghostIcon.style.visibility = Visibility.Hidden;
                ghostIcon.pickingMode = PickingMode.Ignore;
            }
            
            var btnSave = inventory.Q<Button>(name: "btn-save");
            if (btnSave != null) {
                btnSave.clicked += () => OnSaveClicked?.Invoke();
            }

            var btnLoad = inventory.Q<Button>(name: "btn-load");
            if (btnLoad != null) {
                btnLoad.clicked += () => OnLoadClicked?.Invoke();
            }

            // 인벤토리 창 게임 시작 시 안 보이도록 숨기기
            container.style.display = DisplayStyle.None;
            IsAnyInventoryOpen = false;
            
            yield return null; 
        }


        void OnDisable() {
            IsAnyInventoryOpen = false;
        }

        protected override void Update() {
            base.Update();
            
            // Tab 키를 누르면 인벤토리 토글 (표시/숨김)
            if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame && container != null) {
                bool isHidden = container.style.display == DisplayStyle.None;
                container.style.display = isHidden ? DisplayStyle.Flex : DisplayStyle.None;

                IsAnyInventoryOpen = isHidden;

                // 인벤토리가 열려있을 때(isHidden == true가 방금 열린 것)
                if (isHidden) {
                    UnityEngine.Cursor.lockState = CursorLockMode.None;
                    UnityEngine.Cursor.visible = true;
                } else {
                    UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                    UnityEngine.Cursor.visible = false;
                }

                // 플레이어 조작 활성/비활성화 (열렸을 때 조작 끄기)
                PlayerInputHandler playerInput = UnityEngine.Object.FindAnyObjectByType<PlayerInputHandler>();
                if (playerInput != null) {
                    playerInput.SetInputActive(!isHidden);
                }
            }
        }
    }
}


