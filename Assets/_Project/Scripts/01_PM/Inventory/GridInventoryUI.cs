using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Systems.Inventory
{
    /// <summary>
    /// ItemInstance 기반 그리드 인벤토리 UI
    /// UI Toolkit 기반으로 구현
    /// </summary>
    public class GridInventoryUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private StyleSheet styleSheet;
        [SerializeField] private string panelName = "Inventory";

        private GridInventorySystem inventorySystem;
        private VisualElement root;
        private VisualElement container;
        private VisualElement slotsContainer;
        private VisualElement itemsContainer;
        private VisualElement ghostIcon;

        private ItemInstanceView[,] slotViews;
        private int gridWidth, gridHeight;
        private bool isDragging = false;
        private ItemInstanceView draggedItem;

        private float slotSize = 65f;
        private float slotSpacing = 4f;

        public void Initialize(GridInventorySystem system, int width, int height)
        {
            inventorySystem = system;
            gridWidth = width;
            gridHeight = height;

            if (system != null)
            {
                system.OnInventoryChanged += RefreshUI;
            }

            StartCoroutine(InitializeUI());
        }

        private System.Collections.IEnumerator InitializeUI()
        {
            if (uiDocument == null)
            {
                Debug.LogError("UIDocument가 설정되지 않았습니다.");
                yield break;
            }

            root = uiDocument.rootVisualElement;
            
            if (styleSheet != null)
            {
                root.styleSheets.Add(styleSheet);
            }

            // 기존 UXML 구조 사용
            container = root.Q<VisualElement>(className: "container");
            if (container == null)
            {
                Debug.LogError("UXML에서 'container' 클래스를 찾을 수 없습니다.");
                yield break;
            }

            // 헤더 업데이트
            var headerLabel = root.Q<Label>("inventoryHeader");
            if (headerLabel != null)
            {
                headerLabel.text = panelName.ToUpper();
            }

            // 세이브/로드 버튼 생성
            CreateSaveLoadButtons();

            // 슬롯 컨테이너 찾기
            slotsContainer = root.Q<VisualElement>("slotsContainer");
            if (slotsContainer == null)
            {
                Debug.LogError("UXML에서 'slotsContainer'를 찾을 수 없습니다.");
                yield break;
            }

            // 기존 슬롯들 제거하고 새로 생성
            slotsContainer.Clear();

            // 새 그리드 슬롯들 생성
            slotViews = new ItemInstanceView[gridWidth, gridHeight];
            for (int y = 0; y < gridHeight; y++)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    var slotElement = new VisualElement();
                    slotElement.AddToClassList("slot");
                    slotsContainer.Add(slotElement);
                }
            }

            // 아이템 컨테이너 생성 (오버레이)
            itemsContainer = new VisualElement();
            itemsContainer.name = "items-overlay";
            itemsContainer.style.position = Position.Absolute;
            itemsContainer.style.top = 0;
            itemsContainer.style.left = 0;
            itemsContainer.style.right = 0;
            itemsContainer.style.bottom = 0;
            slotsContainer.Add(itemsContainer);

            // 고스트 아이콘
            ghostIcon = root.Q<VisualElement>("ghostIcon");
            if (ghostIcon == null)
            {
                // 원본처럼 container에 ghostIcon 생성
                ghostIcon = new VisualElement();
                ghostIcon.AddToClassList("ghostIcon");
                ghostIcon.name = "ghostIcon";
                ghostIcon.style.visibility = Visibility.Hidden;
                ghostIcon.style.position = Position.Absolute;
                container.Add(ghostIcon); // root가 아니라 container에 추가
            }

            RegisterUIEvents();
            RefreshInventoryDisplay();
        }

        private void RegisterUIEvents()
        {
            // Tab키로 인벤토리 토글 - Update()에서 처리
            
            // 드래그 앤 드롭 이벤트 등록
            if (root != null)
            {
                root.RegisterCallback<PointerMoveEvent>(OnPointerMove);
                root.RegisterCallback<PointerUpEvent>(OnPointerUp);
            }

            // 초기에는 숨김
            if (container != null)
            {
                container.style.display = DisplayStyle.None;
            }
        }
        
        /// <summary>
        /// GridInventorySystem의 OnInventoryChanged 이벤트용 메서드
        /// </summary>
        private void RefreshUI(List<GridInventorySlot> slots)
        {
            if (itemsContainer == null) return;

            // 기존 아이템 뷰들 제거
            itemsContainer.Clear();

            // 새 아이템 뷰들 생성
            foreach (var slot in slots)
            {
                if (slot.itemInstance?.Data != null)
                {
                    CreateItemView(slot);
                }
            }
        }
        
        /// <summary>
        /// 세이브/로드 버튼 생성
        /// </summary>
        private void CreateSaveLoadButtons()
        {
            // 버튼 컨테이너 찾기 또는 생성
            var buttonContainer = root.Q<VisualElement>("button-container");
            if (buttonContainer == null)
            {
                buttonContainer = new VisualElement();
                buttonContainer.name = "button-container";
                buttonContainer.style.flexDirection = FlexDirection.Row;
                buttonContainer.style.justifyContent = Justify.Center;
                buttonContainer.style.marginTop = 10;
                buttonContainer.style.marginBottom = 10;
                
                // 헤더 다음에 버튼 컨테이너 추가
                var header = root.Q<Label>("inventoryHeader");
                if (header?.parent != null)
                {
                    header.parent.Insert(header.parent.IndexOf(header) + 1, buttonContainer);
                }
            }
            
            // 세이브 버튼
            var saveButton = new Button(() => {
                if (inventorySystem != null)
                {
                    inventorySystem.SaveInventory();
                }
            });
            saveButton.text = "저장";
            saveButton.AddToClassList("save-button");
            saveButton.style.marginRight = 10;
            buttonContainer.Add(saveButton);
            
            // 로드 버튼
            var loadButton = new Button(() => {
                if (inventorySystem != null)
                {
                    inventorySystem.LoadInventory();
                }
            });
            loadButton.text = "로드";
            loadButton.AddToClassList("load-button");
            buttonContainer.Add(loadButton);
        }
        
        private void RefreshInventoryDisplay()
        {
            if (inventorySystem == null) return;
            
            // 아이템 컨테이너 초기화
            itemsContainer?.Clear();
            
            // 현재 아이템들 표시
            var allItems = inventorySystem.GetAllItems();
            foreach (var slot in allItems)
            {
                if (slot.itemInstance?.Data != null)
                {
                    CreateItemView(slot);
                }
            }
        }
        
        private void CreateItemView(GridInventorySlot slot)
        {
            // 아이템 뷰 생성 로직 
            var itemView = CreateItemDisplayView(slot);
            if (itemView != null)
            {
                itemsContainer.Add(itemView);
            }
        }

        private void Update()
        {
            // Tab 키로 인벤토리 토글
            if (Input.GetKeyDown(KeyCode.Tab) && container != null)
            {
                bool isHidden = container.style.display == DisplayStyle.None;
                container.style.display = isHidden ? DisplayStyle.Flex : DisplayStyle.None;

                if (isHidden)
                {
                    UnityEngine.Cursor.lockState = CursorLockMode.None;
                    UnityEngine.Cursor.visible = true;
                }
                else
                {
                    UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                    UnityEngine.Cursor.visible = false;
                }

                // 플레이어 입력 제어
                var playerInput = FindAnyObjectByType<PlayerInputHandler>();
                if (playerInput != null)
                {
                    playerInput.SetInputActive(!isHidden);
                }
            }
        }

        /// <summary>
        /// ItemInstance를 위한 UI 뷰 생성
        /// </summary>
        private ItemInstanceView CreateItemDisplayView(GridInventorySlot slot)
        {
            var itemView = new ItemInstanceView(slot.itemInstance);

            // 위치와 크기 설정
            itemView.style.position = Position.Absolute;
            itemView.style.left = 10f + slot.slotX * (slotSize + slotSpacing);
            itemView.style.top = 10f + slot.slotY * (slotSize + slotSpacing);
            itemView.style.width = slot.sizeX * slotSize + (slot.sizeX - 1) * slotSpacing;
            itemView.style.height = slot.sizeY * slotSize + (slot.sizeY - 1) * slotSpacing;

            // 드래그 이벤트 등록
            itemView.OnStartDrag += OnItemDragStart;

            return itemView;
        }

        /// <summary>
        /// 아이템 드래그 시작
        /// </summary>
        private void OnItemDragStart(Vector2 position, ItemInstanceView itemView)
        {
            isDragging = true;
            draggedItem = itemView;

            if (ghostIcon != null)
            {
                // 원본 순서대로: 먼저 위치 설정
                SetGhostIconPosition(position, itemView);
                
                // 원본처럼 new StyleBackground 사용
                ghostIcon.style.backgroundImage = new StyleBackground(itemView.ItemInstance.Data.itemIcon);
                ghostIcon.style.width = itemView.style.width;
                ghostIcon.style.height = itemView.style.height;

                // 원본처럼 visibility 사용
                itemView.style.visibility = Visibility.Hidden;
                ghostIcon.style.visibility = Visibility.Visible;
                ghostIcon.BringToFront();
            }

            // 마우스 이벤트 등록
            root.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            root.RegisterCallback<PointerUpEvent>(OnPointerUp);
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!isDragging || draggedItem == null) return;

            // 원본의 SetGhostIconPosition 로직 사용
            SetGhostIconPosition(evt.position, draggedItem);
        }
        
        // 원본 StorageView의 SetGhostIconPosition 메서드 복사
        private void SetGhostIconPosition(Vector2 position, ItemInstanceView originItem)
        {
            if (originItem != null && ghostIcon != null)
            {
                ghostIcon.style.top = position.y - (originItem.layout.height / 2);
                ghostIcon.style.left = position.x - (originItem.layout.width / 2);
            }
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!isDragging) return;

            // 원본처럼 ghostIcon의 worldBound를 사용해서 드롭 위치 계산
            Vector2 checkPosition = new Vector2(
                ghostIcon.worldBound.xMin,
                ghostIcon.worldBound.yMin  
            );

            // 원본처럼 약간 안쪽으로 조정해서 엣지 스냅핑 이슈 방지
            checkPosition.x += slotSize / 2f;
            checkPosition.y += slotSize / 2f;

            // 드롭 위치 계산 및 처리
            ProcessDrop(checkPosition);

            // 원본처럼 무조건 드래그 상태 리셋 (성공/실패 무관)
            isDragging = false;
            if (draggedItem != null)
            {
                draggedItem.style.visibility = Visibility.Visible;
                draggedItem = null;
            }
            ghostIcon.style.visibility = Visibility.Hidden;
            
            // 마우스 이벤트 해제
            root.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
            root.UnregisterCallback<PointerUpEvent>(OnPointerUp);
        }

        private void ProcessDrop(Vector2 checkPosition)
        {
            // 그리드 좌표로 변환
            Vector2 localPos = slotsContainer.WorldToLocal(checkPosition);
            
            int gridX = Mathf.FloorToInt((localPos.x - 10f) / (slotSize + slotSpacing));
            int gridY = Mathf.FloorToInt((localPos.y - 10f) / (slotSize + slotSpacing));

            // 유효한 범위 체크하고 이동 시도
            if (gridX >= 0 && gridX < gridWidth && gridY >= 0 && gridY < gridHeight)
            {
                // 실제 아이템 이동 처리 - 성공/실패와 관계없이 OnPointerUp에서 상태 리셋할 예정
                TryMoveItem(draggedItem.ItemInstance, gridX, gridY);
            }
        }
        
        private void TryMoveItem(ItemInstance item, int targetX, int targetY)
        {
            // 현재 슬롯 찾기
            var currentSlot = inventorySystem.GetAllItems().Find(s => s.itemInstance == item);
            if (currentSlot == null)
            {
                return; // OnPointerUp에서 상태 리셋 처리됨
            }
            
            int originalX = currentSlot.slotX;
            int originalY = currentSlot.slotY;
            
            // 아이템 크기 계산
            var gridShape = item.Data.gridShape;
            int itemWidth = 1, itemHeight = 1;
            
            if (gridShape != null && gridShape.basePositions != null && gridShape.basePositions.Count > 0)
            {
                var positions = gridShape.GetRotatedPositions(item.currentRotation);
                var bounds = GetBounds(positions);
                itemWidth = bounds.Item2 - bounds.Item1 + 1;
                itemHeight = bounds.Item4 - bounds.Item3 + 1;
            }
            
            // 경계 체크
            if (targetX < 0 || targetY < 0 || 
                targetX + itemWidth > gridWidth || 
                targetY + itemHeight > gridHeight)
            {
                return; // OnPointerUp에서 상태 리셋 처리됨
            }
            
            // 현재 위치에서 제거
            inventorySystem.RemoveItem(item);
            
            // 새 위치에 배치 시도
            if (TryAddItemAt(item, targetX, targetY))
            {
                // UI 새로고침은 OnInventoryChanged 이벤트로 자동 처리됨
            }
            else
            {
                // 실패하면 원래 위치로 복구
                TryAddItemAt(item, originalX, originalY);
                // 복구 후에도 OnPointerUp에서 상태 리셋 처리됨
            }
        }

        // GetBounds 메서드를 GridInventoryUI에도 추가 (GridInventorySystem의 private 메서드를 복사)
        private (int minX, int maxX, int minY, int maxY) GetBounds(System.Collections.Generic.List<Vector2Int> positions)
        {
            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;

            foreach (var pos in positions)
            {
                minX = Mathf.Min(minX, pos.x);
                maxX = Mathf.Max(maxX, pos.x);
                minY = Mathf.Min(minY, pos.y);
                maxY = Mathf.Max(maxY, pos.y);
            }

            return (minX, maxX, minY, maxY);
        }
        
        private bool TryAddItemAt(ItemInstance item, int targetX, int targetY)
        {
            // GridInventorySystem의 새로운 메서드 사용
            return inventorySystem.TryAddItemAt(item, targetX, targetY);
        }

        private void ResetDragState()
        {
            isDragging = false;
            
            if (draggedItem != null)
            {
                draggedItem.style.visibility = Visibility.Visible; // 원본처럼 visibility 복원
                draggedItem = null;
            }
            
            if (ghostIcon != null)
            {
                ghostIcon.style.visibility = Visibility.Hidden; // 원본처럼 visibility로 숨김
            }
            
            // 마우스 이벤트 해제
            root.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
            root.UnregisterCallback<PointerUpEvent>(OnPointerUp);
        }

        private void OnDestroy()
        {
            if (inventorySystem != null)
            {
                inventorySystem.OnInventoryChanged -= RefreshUI;
            }
            
            // UI 이벤트 해제
            if (root != null)
            {
                root.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
                root.UnregisterCallback<PointerUpEvent>(OnPointerUp);
            }
        }
    }

    /// <summary>
    /// ItemInstance를 표시하는 UI 요소
    /// </summary>
    public class ItemInstanceView : VisualElement
    {
        public ItemInstance ItemInstance { get; private set; }
        public Image Icon { get; private set; }
        public Label StackLabel { get; private set; }

        public event System.Action<Vector2, ItemInstanceView> OnStartDrag;

        public ItemInstanceView(ItemInstance itemInstance)
        {
            ItemInstance = itemInstance;
            
            AddToClassList("item-view");
            
            // 드래그를 위한 중요 설정들
            pickingMode = PickingMode.Position;
            style.cursor = StyleKeyword.Initial;

            // 아이콘
            Icon = new Image();
            Icon.AddToClassList("item-icon");
            Icon.style.flexGrow = 1;
            Icon.pickingMode = PickingMode.Ignore; // 아이콘은 이벤트 차단하지 않도록
            Add(Icon);

            // 스택 라벨
            StackLabel = new Label();
            StackLabel.AddToClassList("stack-label");
            StackLabel.style.position = Position.Absolute;
            StackLabel.style.bottom = 2;
            StackLabel.style.right = 4;
            StackLabel.style.color = Color.white;
            StackLabel.style.fontSize = 14;
            StackLabel.pickingMode = PickingMode.Ignore; // 라벨도 이벤트 차단하지 않도록
            Add(StackLabel);

            UpdateDisplay();

            // 드래그 이벤트
            RegisterCallback<PointerDownEvent>(OnPointerDown);
        }

        private void UpdateDisplay()
        {
            if (ItemInstance?.Data != null)
            {
                Icon.sprite = ItemInstance.Data.itemIcon;
                
                if (ItemInstance.currentStackCount > 1)
                {
                    StackLabel.text = ItemInstance.currentStackCount.ToString();
                    StackLabel.style.display = DisplayStyle.Flex;
                }
                else
                {
                    StackLabel.style.display = DisplayStyle.None;
                }
            }
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0) return;
            
            OnStartDrag?.Invoke(evt.position, this);
            evt.StopPropagation();
        }
    }
}