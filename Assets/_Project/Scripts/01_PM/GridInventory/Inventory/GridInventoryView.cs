using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Systems.GridInventory {
    public class GridInventoryView : GridStorageView {
        [SerializeField] string panelName = "GridInventory";

        public static GridInventoryView Instance { get; private set; }
        public static bool IsAnyInventoryOpen { get; set; }

        public event System.Action OnSaveClicked;
        public event System.Action OnLoadClicked;

        private PlayerInputHandler localPlayerInputHandler;
        private float playerSearchTimer = 0f;
        private bool openedByStorage;

        public bool IsOpen => container != null && container.style.display != DisplayStyle.None;

        private void HandleSaveClicked() {
            OnSaveClicked?.Invoke();
        }

        private void HandleLoadClicked() {
            OnLoadClicked?.Invoke();
        }

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
            
            if (document != null) {
                document.sortingOrder = 3; // 인벤토리
            }

            QuantityPopupView.EnsureExists();

            container = root.Q<VisualElement>(className: "container");
            if (container == null) {
                // UXML 구조가 비정상일 경우 코드로 기본 뼈대 동적 생성 (Fallback)
                root.Clear();
                FixedAspectRatioManager.RequestRefresh();
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
            
            // 테스트용 라운드 강제 종료 버튼 복구 (Load 버튼은 숨김 유지)
            var btnSave = inventory.Q<Button>(name: "btn-save");
            // var btnLoad = inventory.Q<Button>(name: "btn-load");

            UpdateSaveLoadButtonsVisibility();

            if (btnSave != null) {
                btnSave.text = "Round End (Test)"; // 버튼 텍스트 변경
                btnSave.clicked -= HandleSaveClicked;
                btnSave.clicked += HandleSaveClicked;
            }

            /*
            if (btnLoad != null) {
                btnLoad.style.display = DisplayStyle.None; // Load 버튼은 강제로 숨김
                // btnLoad.clicked -= HandleLoadClicked;
                // btnLoad.clicked += HandleLoadClicked;
            }
            */

            // 인벤토리 창 게임 시작 시 안 보이도록 숨기기
            container.style.display = DisplayStyle.None;
            IsAnyInventoryOpen = false;
            ResetStorageLayout();
            FixedAspectRatioManager.RequestRefresh();
            
            yield return null; 
        }


        void OnDisable() {
            IsAnyInventoryOpen = false;
        }

        public void OpenForStorage(bool closeWithStorage = true)
        {
            if (container == null)
            {
                return;
            }

            openedByStorage = closeWithStorage;
            container.style.display = DisplayStyle.Flex;
            IsAnyInventoryOpen = true;
            ApplyStorageLayout();
            FixedAspectRatioManager.RequestRefresh();
            
            UpdateSaveLoadButtonsVisibility(); // 테스트용 버튼 가시성 업데이트 복구

            PauseMenuManager.UpdateCursorAndInputState();
        }

        public void CloseForStorage()
        {
            if (!openedByStorage || container == null)
            {
                return;
            }

            openedByStorage = false;
            container.style.display = DisplayStyle.None;
            IsAnyInventoryOpen = false;
            ResetStorageLayout();
            FixedAspectRatioManager.RequestRefresh();
            
            PauseMenuManager.UpdateCursorAndInputState();
        }

        public void CloseInventory()
        {
            if (container != null && IsOpen)
            {
                container.style.display = DisplayStyle.None;
                IsAnyInventoryOpen = false;
                FixedAspectRatioManager.RequestRefresh();
                PauseMenuManager.UpdateCursorAndInputState();
            }
        }


        protected override void Update() {
            base.Update();

            if (localPlayerInputHandler == null)
            {
                playerSearchTimer -= Time.deltaTime;
                if (playerSearchTimer <= 0f)
                {
                    playerSearchTimer = 1f; // 1초 간격으로 플레이어 탐색 (성능 최적화)
                    if (LocalPlayerReferenceResolver.TryGetLocalPlayer(out PlayerController controller))
                    {
                        localPlayerInputHandler = controller.InputHandler;
                    }
                }
            }

            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame && IsOpen) {
                if (PauseMenuManager.isPaused) return; // 일시정지 중일 때 입력 무시

                PauseMenuManager.CloseOpenInGameUI();
                return;
            }

            // Tab 키를 누르면 인벤토리 토글 (표시/숨김)
            if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame && container != null) {
                if (PauseMenuManager.isPaused) return; // 일시정지 중일 때 입력 무시

                if (PauseMenuManager.CloseOpenInGameUI()) return;

                if (Systems.Loot.LootController.Instance != null) {
                    Systems.Loot.LootController.Instance.OpenInventoryOnly();
                    return;
                }

                bool isHidden = container.style.display == DisplayStyle.None;
                container.style.display = isHidden ? DisplayStyle.Flex : DisplayStyle.None;

                IsAnyInventoryOpen = isHidden;
                FixedAspectRatioManager.RequestRefresh();

                // 인벤토리가 열려있을 때(isHidden == true가 방금 열린 것)
                if (isHidden) {
                    UpdateSaveLoadButtonsVisibility();
                }

                PauseMenuManager.UpdateCursorAndInputState();
            }
        }

        private void ApplyStorageLayout()
        {
            if (container == null)
            {
                return;
            }

            container.pickingMode = PickingMode.Ignore;
            container.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0f));
            container.style.alignItems = Align.FlexStart;
            container.style.justifyContent = Justify.Center;

            VisualElement inventory = container.Q<VisualElement>(name: "inventory-window");
            if (inventory != null)
            {
                inventory.pickingMode = PickingMode.Position;
                inventory.style.marginLeft = 24;
                inventory.style.marginRight = 0;
                inventory.style.maxWidth = 900;
            }
        }

        private void ResetStorageLayout()
        {
            if (container == null)
            {
                return;
            }

            container.pickingMode = PickingMode.Position;
            container.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.75f));
            container.style.alignItems = Align.Center;
            container.style.justifyContent = Justify.Center;

            VisualElement inventory = container.Q<VisualElement>(name: "inventory-window");
            if (inventory != null)
            {
                inventory.pickingMode = PickingMode.Position;
                inventory.style.marginLeft = StyleKeyword.Null;
                inventory.style.marginRight = StyleKeyword.Null;
                inventory.style.maxWidth = StyleKeyword.Null;
            }
        }
        
        private void UpdateSaveLoadButtonsVisibility()
        {
            if (container == null) return;
            var inventory = container.Q<VisualElement>(name: "inventory-window");
            if (inventory == null) return;
            
            var btnSave = inventory.Q<Button>(name: "btn-save");
            // var btnLoad = inventory.Q<Button>(name: "btn-load");
            
            // 로드 버튼은 사용하지 않으므로 항상 숨김
            // if (btnLoad != null) btnLoad.style.display = DisplayStyle.None;

            if (AuthSession.IsOffline)
            {
                if (btnSave != null) btnSave.style.display = DisplayStyle.Flex;
            }
            else
            {
                bool isHost = Systems.Loot.LootNetworkSync.Instance != null && Systems.Loot.LootNetworkSync.Instance.HasStateAuthority;
                if (isHost)
                {
                    if (btnSave != null) btnSave.style.display = DisplayStyle.Flex;
                }
                else
                {
                    if (btnSave != null) btnSave.style.display = DisplayStyle.None;
                }
            }
        }
    }
}


