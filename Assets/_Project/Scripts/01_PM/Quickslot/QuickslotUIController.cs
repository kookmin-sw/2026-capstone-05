using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Systems.GridInventory;

public class QuickslotUIController : MonoBehaviour
{
    public static QuickslotUIController Instance { get; private set; }
    public static event System.Action OnInitialized;

    [SerializeField] private UIDocument uiDocument;

    private const int MaxSlots = 4;
    private QuickslotModel model;

    private class QuickslotViewElement
    {
        public VisualElement Root;
        public VisualElement Icon;
        public Label StackLabel;
    }

    private List<QuickslotViewElement> slotViews = new List<QuickslotViewElement>();

    private int selectedSlotIndex = -1;

    private VisualElement dragGhostIcon;
    private int draggingSlotIndex = -1;
    private bool isDragging = false;
    private bool uiReady = false;
    private Vector2 lastPointerPos;
    private ItemRotation originalDragRotation;

    private PlayerEquipment localPlayerEquipment;
    private Coroutine initCoroutine;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // 도메인 리로드 비활성화 시 캐시가 남는 현상을 방지
        if (model == null) model = new QuickslotModel();
        model.Initialize();
        model.OnSlotChanged += RefreshSlotVisual;

        selectedSlotIndex = -1;
        slotViews.Clear();
        dragGhostIcon = null;
        isDragging = false;
        draggingSlotIndex = -1;
        uiReady = false;

        // 같은 PanelSettings를 공유하는 UIDocument들 사이에서 퀵슬롯이 항상 최상위에 렌더링되도록 설정.
        // sortingOrder가 동일하면 도메인 리로드 여부에 따라 순서가 바뀌어
        // 인벤토리 패널이 퀵슬롯 위에 올라가 이벤트를 가로챌 수 있음.
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
        if (uiDocument != null) uiDocument.sortingOrder = 10f;

        OnInitialized?.Invoke();
    }

    private void OnEnable()
    {
        // 이전 초기화 코루틴이 남아있으면 중단
        if (initCoroutine != null)
        {
            StopCoroutine(initCoroutine);
            initCoroutine = null;
        }

        // UI가 유효하면 재초기화 불필요 (씬 리로드 비활성화 시 두번째 실행)
        if (uiReady && slotViews.Count == MaxSlots && slotViews[0] != null && slotViews[0].Root.panel != null)
            return;

        uiReady = false;
        slotViews.Clear();
        dragGhostIcon = null;
        isDragging = false;
        draggingSlotIndex = -1;

        initCoroutine = StartCoroutine(InitializeUI());
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            OnItemDroppedGlobal = null;
            OnInitialized = null;
        }
    }

    /// <summary>
    /// 인벤토리(GridStorageView)와 동일한 코루틴 기반 초기화 패턴.
    /// Visual Tree 접근 → yield return null (1프레임 대기) → 이벤트 콜백 등록
    /// </summary>
    private IEnumerator InitializeUI()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        // 1단계: UIDocument의 rootVisualElement가 준비될 때까지 대기
        while (uiDocument == null || uiDocument.rootVisualElement == null)
        {
            yield return null;
            if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
        }

        var root = uiDocument.rootVisualElement;

        // 2단계: UXML 파싱이 완료되어 슬롯 요소가 트리에 존재할 때까지 대기
        while (root.Q<VisualElement>("Quickslot0") == null)
        {
            yield return null;
        }

        // 3단계: 슬롯 요소 탐색, 캐싱 및 picking mode 설정 (인벤토리의 InitializeView에 해당)
        slotViews.Clear();
        for (int i = 0; i < MaxSlots; i++)
        {
            var slot = root.Q<VisualElement>($"Quickslot{i}");
            if (slot == null) continue;

            slot.pickingMode = PickingMode.Position;

            var iconNode = slot.Q<VisualElement>("Icon");
            if (iconNode == null) 
            {
                iconNode = slot.CreateChild("Icon");
            }
            iconNode.pickingMode = PickingMode.Ignore;

            var stackLabel = slot.Q<Label>("StackLabel");
            if (stackLabel == null)
            {
                stackLabel = new Label();
                stackLabel.name = "StackLabel";
                stackLabel.pickingMode = PickingMode.Ignore;
                stackLabel.style.position = Position.Absolute;
                stackLabel.style.bottom = 2;
                stackLabel.style.right = 4;
                stackLabel.style.color = Color.white;
                stackLabel.style.fontSize = 14;
                stackLabel.style.textShadow = new TextShadow {
                    blurRadius = 0,
                    color = Color.black,
                    offset = new Vector2(1, 1)
                };
                slot.Add(stackLabel);
            }
            stackLabel.visible = false;

            // Icon, Label 등 자식 요소가 포인터 이벤트를 가로채지 않도록 설정
            foreach (var child in slot.Children())
            {
                child.pickingMode = PickingMode.Ignore;
            }

            slotViews.Add(new QuickslotViewElement { Root = slot, Icon = iconNode, StackLabel = stackLabel });
        }

        // 4단계: 1프레임 대기 — 패널의 이벤트 시스템이 완전히 활성화된 후 콜백 등록
        // (인벤토리의 InitializeView 마지막 yield return null과 동일한 역할)
        yield return null;

        // 5단계: 이벤트 콜백 등록 (인벤토리의 RegisterEventCallbacks에 해당)
        for (int i = 0; i < slotViews.Count; i++)
        {
            int slotIndex = i;
            var slotElement = slotViews[slotIndex].Root;
            slotElement.RegisterCallback<PointerDownEvent>(evt => OnPointerDown(evt, slotIndex));
            slotElement.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            slotElement.RegisterCallback<PointerUpEvent>(OnPointerUp);
        }

        // 고스트 아이콘 생성
        dragGhostIcon = new VisualElement();
        dragGhostIcon.style.position = Position.Absolute;
        dragGhostIcon.style.visibility = Visibility.Hidden;
        dragGhostIcon.style.width = 60;
        dragGhostIcon.style.height = 60;
        dragGhostIcon.pickingMode = PickingMode.Ignore;
        root.Add(dragGhostIcon);

        uiReady = true;
        initCoroutine = null;

        // 코루틴 대기 중 SetItemInSlot 등으로 데이터가 변경되었을 수 있으므로 전체 슬롯 UI 동기화
        for (int i = 0; i < MaxSlots; i++)
        {
            RefreshSlotVisual(i);
        }
    }

    public static event System.Action<ItemInstance, Vector2> OnItemDragUpdateGlobal;
    public static event System.Action OnItemDragEndGlobal;

    private void OnPointerDown(PointerDownEvent evt, int slotIndex)
    {
        var itemInstance = model.Get(slotIndex);
        if (evt.button != 0 || itemInstance == null) return;

        isDragging = true;
        draggingSlotIndex = slotIndex;
        lastPointerPos = evt.position;
        originalDragRotation = itemInstance.currentRotation;

        var slotElement = slotViews[slotIndex].Root;
        slotElement.CapturePointer(evt.pointerId);

        GridInventoryDragHelper.GetGhostSizeAndPivot(itemInstance, out float baseW, out float baseH, out float baseAnchorXRatio, out float baseAnchorYRatio);

        dragGhostIcon.style.backgroundImage = new StyleBackground(itemInstance.Data.itemIcon);
        dragGhostIcon.style.width = baseW;
        dragGhostIcon.style.height = baseH;

        dragGhostIcon.style.transformOrigin = new TransformOrigin(
            new Length(baseAnchorXRatio * 100f, LengthUnit.Percent),
            new Length(baseAnchorYRatio * 100f, LengthUnit.Percent)
        );

        float visualAngle = (int)itemInstance.currentRotation * 90f;
        dragGhostIcon.style.rotate = new Rotate(new Angle(visualAngle));
        dragGhostIcon.style.visibility = Visibility.Visible;
        dragGhostIcon.BringToFront();
        dragGhostIcon.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(BackgroundSizeType.Contain));

        GridInventoryDragHelper.UpdateGhostPosition(dragGhostIcon, evt.position);

        evt.StopPropagation();
        
        OnItemDragUpdateGlobal?.Invoke(itemInstance, evt.position);
    }

    private void OnPointerMove(PointerMoveEvent evt)
    {
        if (!isDragging || dragGhostIcon == null) return;

        lastPointerPos = evt.position;
        GridInventoryDragHelper.UpdateGhostPosition(dragGhostIcon, evt.position);
        evt.StopPropagation();

        var itemInstance = draggingSlotIndex >= 0 ? model.Get(draggingSlotIndex) : null;
        if (itemInstance != null) {
            OnItemDragUpdateGlobal?.Invoke(itemInstance, evt.position);
        }
    }

    public static event System.Action<ItemInstance, int, Vector2> OnItemDroppedGlobal;

    private void OnPointerUp(PointerUpEvent evt)
    {
        if (!isDragging) return;

        isDragging = false;
        if (draggingSlotIndex >= 0 && draggingSlotIndex < slotViews.Count)
        {
            slotViews[draggingSlotIndex].Root.ReleasePointer(evt.pointerId);
        }

        if (dragGhostIcon != null)
        {
            dragGhostIcon.style.visibility = Visibility.Hidden;
        }

        OnItemDragEndGlobal?.Invoke();

        if (draggingSlotIndex >= 0 && draggingSlotIndex < MaxSlots)
        {
            var item = model.Get(draggingSlotIndex);
            if (item != null)
            {
                int targetQuickslot = GetSlotIndexAtPosition(evt.position);
                bool droppedOnInventory = false;

                if (GridInventoryView.Instance != null && GridInventoryView.Instance.isActiveAndEnabled) {
                    var slot = GridInventoryView.Instance.GetGridSlotAtPosition(evt.position);
                    if (slot != null) {
                        droppedOnInventory = true;
                    }
                }

                if (targetQuickslot >= 0 && targetQuickslot != draggingSlotIndex)
                {
                    model.MergeOrSwap(draggingSlotIndex, targetQuickslot);
                    item.currentRotation = originalDragRotation; // 퀵슬롯으로 드롭된 경우 회전 원복
                }
                else if (droppedOnInventory)
                {
                    OnItemDroppedGlobal?.Invoke(item, draggingSlotIndex, evt.position);
                }
                else 
                {
                    item.currentRotation = originalDragRotation; // 허공에 버리거나 드롭 실패 시 회전 무효화
                }
            }
        }

        draggingSlotIndex = -1;
        evt.StopPropagation();
    }

    private float playerSearchTimer = 0f;

    private void Update()
    {
        if (isDragging && dragGhostIcon != null && draggingSlotIndex >= 0)
        {
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.rKey.wasPressedThisFrame)
            {
                var itemInstance = model.Get(draggingSlotIndex);
                if (itemInstance != null && itemInstance.Data != null)
                {
                    itemInstance.currentRotation = (ItemRotation)(((int)itemInstance.currentRotation + 1) % 4);
                    
                    GridInventoryDragHelper.GetGhostSizeAndPivot(itemInstance, out float baseW, out float baseH, out float baseAnchorXRatio, out float baseAnchorYRatio);
                    
                    dragGhostIcon.style.width = baseW;
                    dragGhostIcon.style.height = baseH;

                    dragGhostIcon.style.transformOrigin = new TransformOrigin(
                        new Length(baseAnchorXRatio * 100f, LengthUnit.Percent),
                        new Length(baseAnchorYRatio * 100f, LengthUnit.Percent)
                    );

                    float visualAngle = (int)itemInstance.currentRotation * 90f;
                    dragGhostIcon.style.rotate = new Rotate(new Angle(visualAngle));
                    
                    GridInventoryDragHelper.UpdateGhostPosition(dragGhostIcon, lastPointerPos);
                    OnItemDragUpdateGlobal?.Invoke(itemInstance, lastPointerPos);
                }
            }
        }

        if (localPlayerEquipment == null)
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
                        localPlayerEquipment = controller.Equipment;
                        break;
                    }
                }
            }
        }

        if (localPlayerEquipment == null || Keyboard.current == null) return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame) EquipFromQuickslot(0);
        else if (Keyboard.current.digit2Key.wasPressedThisFrame) EquipFromQuickslot(1);
        else if (Keyboard.current.digit3Key.wasPressedThisFrame) EquipFromQuickslot(2);
        else if (Keyboard.current.digit4Key.wasPressedThisFrame) EquipFromQuickslot(3);
    }

    private void EquipFromQuickslot(int index)
    {
        SelectSlot(index);
        ItemInstance item = GetItem(index);

        if (item != null)
        {
            localPlayerEquipment.EquipItem(item);
        }
        else
        {
            localPlayerEquipment.UnequipItem();
        }
    }

    public bool AddItemToEmptySlot(ItemInstance item)
    {
        return model.AddToEmpty(item);
    }

    public int GetSlotIndexAtPosition(Vector2 screenPosition)
    {
        if (!uiReady) return -1;
        if (uiDocument != null && uiDocument.panelSettings != null)
        {
            for (int i = 0; i < slotViews.Count; i++)
            {
                var slot = slotViews[i].Root;
                if (slot != null && slot.worldBound.Contains(screenPosition))
                {
                    return i;
                }
            }
        }
        return -1;
    }

    public void SetItemInSlot(int index, ItemInstance item)
    {
        model.Set(index, item);
    }

    public void RemoveItemFromSlot(int index)
    {
        model.Set(index, null);
    }

    public ItemInstance GetItem(int index)
    {
        return model.Get(index);
    }

    public void SelectSlot(int index)
    {
        if (!uiReady) return;
        if (index >= 0 && index < MaxSlots)
        {
            selectedSlotIndex = index;
            for (int i = 0; i < slotViews.Count; i++)
            {
                if (i == selectedSlotIndex)
                {
                    slotViews[i].Root.AddToClassList("selected-slot");
                }
                else
                {
                    slotViews[i].Root.RemoveFromClassList("selected-slot");
                }
            }
        }
    }

    public void RefreshSlotVisual(int index)
    {
        if (!uiReady) return;
        if (index < 0 || index >= slotViews.Count) return;

        var view = slotViews[index];
        var item = model.Get(index);

        if (item != null && item.Data != null && item.Data.itemIcon != null)
        {
            view.Icon.style.backgroundImage = new StyleBackground(item.Data.itemIcon);
            view.Icon.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(BackgroundSizeType.Contain));

            view.StackLabel.text = item.currentStackCount > 1 ? item.currentStackCount.ToString() : string.Empty;
            view.StackLabel.visible = item.currentStackCount > 1;
        }
        else
        {
            view.Icon.style.backgroundImage = null;
            view.StackLabel.visible = false;
        }
    }
}
