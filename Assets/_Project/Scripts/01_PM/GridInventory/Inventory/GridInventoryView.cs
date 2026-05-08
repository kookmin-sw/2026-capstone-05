using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Systems.GridInventory {
    public class GridInventoryView : GridStorageView {
        [SerializeField] string panelName = "GridInventory";

        public static GridInventoryView Instance { get; private set; }
        public static bool IsAnyInventoryOpen { get; private set; }

        public event System.Action OnSaveClicked;
        public event System.Action OnLoadClicked;

        // Legacy API compatibility for Shop/Backend scripts
        public VisualElement Container => container;
        public StyleSheet GridStyleSheet => styleSheet;

        private PlayerInputHandler localPlayerInputHandler;
        private float playerSearchTimer = 0f;

        private void Awake() {
            if (Instance == null) Instance = this;
            else if (Instance != this) Destroy(gameObject);
        }

        private void OnDestroy() {
            if (Instance == this) Instance = null;
        }

        public override IEnumerator InitializeView(int size = 20) {
            Slots = new GridSlot[size];
            root = document.rootVisualElement;

            container = root.Q<VisualElement>(className: "container");
            if (container == null) {
                // UXML 구조가 비정상일 경우 코드로 기본 뼈대 동적 생성 (Fallback)
                root.Clear();
                root.styleSheets.Add(styleSheet);
                container = root.CreateChild("container");
                var inv = container.CreateChild("inventory-window");
                inv.name = "inventory-window"; // Q<VisualElement>(name: "inventory-window") 대응
                
                var header = inv.CreateChild("inventoryHeader");
                header.name = "inventoryHeader";
                header.Add(new Label(panelName.ToUpper()));
                
                var sc = inv.CreateChild("slotsContainer");
                sc.name = "slotsContainer"; // Q<VisualElement>(name: "slotsContainer") 대응
            }
            
            var inventory = container.Q<VisualElement>(name: "inventory-window");
            
            var headerLabel = inventory.Q<Label>(name: "inventoryHeader");
            if (headerLabel != null) {
                headerLabel.text = panelName.ToUpper();
            }

            var slotsContainer = inventory.Q<VisualElement>(name: "slotsContainer");
            
            // 컨테이너 크기 동적 계산 (슬롯 사이즈 대응)
            float containerPaddingTotal = 20f; // 10f left/top + 10f right/bottom
            slotsContainer.style.width = (currentColumns * SlotTotalSize) + containerPaddingTotal + 4f; // 4f 여유 공간 추가 (Flex-wrap 오차 방지)
            int rowsForUpdate = Mathf.CeilToInt((float)size / currentColumns);
            slotsContainer.style.height = (rowsForUpdate * SlotTotalSize) + containerPaddingTotal + 4f;
            
            var existingSlots = slotsContainer.Query<GridSlot>().ToList();

            // 슬롯 UI 동기화 (부족하면 추가, 많으면 기존 것 재사용)
            for (int i = 0; i < size; i++) {
                if (i < existingSlots.Count) {
                    Slots[i] = existingSlots[i];
                } else {
                    var slot = new GridSlot();
                    slot.name = "slot";
                    slot.AddToClassList("slot");
                    slotsContainer.Add(slot);
                    Slots[i] = slot;
                }
            }

            // 아이템 컨테이너 초기화
            itemsContainer = slotsContainer.Q<VisualElement>("itemsContainer") ?? slotsContainer.CreateChild("itemsContainer");
            itemsContainer.style.position = Position.Absolute;
            itemsContainer.style.top = 0;
            itemsContainer.style.left = 0;
            itemsContainer.style.right = 0;
            itemsContainer.style.bottom = 0;
            itemsContainer.BringToFront(); // 슬롯들 위로 렌더링되도록 수정
            
            // 고스트 아이콘 (드래그 시 표시) 초기화
            ghostIcon = container.Q<VisualElement>(className: "ghostIcon") ?? container.CreateChild("ghostIcon");
            ghostIcon.AddToClassList("ghostIcon");
            ghostIcon.style.position = Position.Absolute;
            ghostIcon.style.visibility = Visibility.Hidden;
            ghostIcon.pickingMode = PickingMode.Ignore;
            
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

            if (localPlayerInputHandler == null)
            {
                playerSearchTimer -= Time.deltaTime;
                if (playerSearchTimer <= 0f)
                {
                    playerSearchTimer = 1f; // 1초 간격으로 플레이어 탐색 (성능 최적화)
                    PlayerController[] controllers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
                    foreach (var controller in controllers)
                    {
                        if (controller.IsLocalPlayer)
                        {
                            localPlayerInputHandler = controller.InputHandler;
                            break;
                        }
                    }
                }
            }

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
                if (localPlayerInputHandler != null) {
                    localPlayerInputHandler.SetInputActive(!isHidden);
                }
            }
        }
    }
}


