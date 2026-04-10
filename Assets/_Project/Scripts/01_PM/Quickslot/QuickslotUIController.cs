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
    private ItemInstance[] quickslots = new ItemInstance[MaxSlots];
    private int selectedSlotIndex = -1;

    private List<VisualElement> slotElements = new List<VisualElement>();
    private VisualElement dragGhostIcon;
    private int draggingSlotIndex = -1;
    private bool isDragging = false;
    private bool uiReady = false;

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
        quickslots = new ItemInstance[MaxSlots];
        selectedSlotIndex = -1;
        slotElements.Clear();
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
        if (uiReady && slotElements.Count == MaxSlots && slotElements[0] != null && slotElements[0].panel != null)
            return;

        uiReady = false;
        slotElements.Clear();
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

        // 3단계: 슬롯 요소 탐색 및 picking mode 설정 (인벤토리의 InitializeView에 해당)
        slotElements.Clear();
        for (int i = 0; i < MaxSlots; i++)
        {
            var slot = root.Q<VisualElement>($"Quickslot{i}");
            if (slot == null) continue;

            slotElements.Add(slot);
            slot.pickingMode = PickingMode.Position;

            // Icon, Label 등 자식 요소가 포인터 이벤트를 가로채지 않도록 설정
            foreach (var child in slot.Children())
            {
                child.pickingMode = PickingMode.Ignore;
            }
        }

        // 4단계: 1프레임 대기 — 패널의 이벤트 시스템이 완전히 활성화된 후 콜백 등록
        // (인벤토리의 InitializeView 마지막 yield return null과 동일한 역할)
        yield return null;

        // 5단계: 이벤트 콜백 등록 (인벤토리의 RegisterEventCallbacks에 해당)
        for (int i = 0; i < slotElements.Count; i++)
        {
            int slotIndex = i;
            var slot = slotElements[slotIndex];
            slot.RegisterCallback<PointerDownEvent>(evt => OnPointerDown(evt, slotIndex));
            slot.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            slot.RegisterCallback<PointerUpEvent>(OnPointerUp);
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

    private void OnPointerDown(PointerDownEvent evt, int slotIndex)
    {
        if (evt.button != 0 || quickslots[slotIndex] == null || quickslots[slotIndex].Data == null) return;

        isDragging = true;
        draggingSlotIndex = slotIndex;

        var slotElement = slotElements[slotIndex];
        slotElement.CapturePointer(evt.pointerId);

        var itemInstance = quickslots[slotIndex];

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

        UpdateGhostPosition(evt.position);

        evt.StopPropagation();
    }

    private void OnPointerMove(PointerMoveEvent evt)
    {
        if (!isDragging) return;

        UpdateGhostPosition(evt.position);
        evt.StopPropagation();
    }

    private void UpdateGhostPosition(Vector2 position)
    {
        if (dragGhostIcon != null)
        {
            GridInventoryDragHelper.UpdateGhostPosition(dragGhostIcon, position);
        }
    }

    public delegate void QuickslotItemDropAction(ItemInstance item, int sourceQuickslotIndex, Vector2 screenPosition);
    public static event QuickslotItemDropAction OnItemDroppedGlobal;
    public event QuickslotItemDropAction OnItemDropped;

    private void OnPointerUp(PointerUpEvent evt)
    {
        if (!isDragging) return;

        isDragging = false;
        if (draggingSlotIndex >= 0 && draggingSlotIndex < slotElements.Count)
        {
            slotElements[draggingSlotIndex].ReleasePointer(evt.pointerId);
        }

        if (dragGhostIcon != null)
        {
            dragGhostIcon.style.visibility = Visibility.Hidden;
        }

        if (draggingSlotIndex >= 0 && draggingSlotIndex < quickslots.Length)
        {
            var item = quickslots[draggingSlotIndex];
            if (item != null)
            {
                int targetQuickslot = GetSlotIndexAtPosition(evt.position);

                if (targetQuickslot >= 0 && targetQuickslot != draggingSlotIndex)
                {
                    var targetItem = quickslots[targetQuickslot];

                    if (targetItem != null && targetItem.Data == item.Data && targetItem.Data.maxStackSize > 1)
                    {
                        int total = item.currentStackCount + targetItem.currentStackCount;
                        if (total <= targetItem.Data.maxStackSize)
                        {
                            targetItem.currentStackCount = total;
                            quickslots[draggingSlotIndex] = null;
                        }
                        else
                        {
                            targetItem.currentStackCount = targetItem.Data.maxStackSize;
                            item.currentStackCount = total - targetItem.Data.maxStackSize;
                        }
                        UpdateSlotUI(targetQuickslot);
                        UpdateSlotUI(draggingSlotIndex);
                    }
                    else
                    {
                        var temp = quickslots[targetQuickslot];
                        quickslots[targetQuickslot] = item;
                        quickslots[draggingSlotIndex] = temp;
                        UpdateSlotUI(targetQuickslot);
                        UpdateSlotUI(draggingSlotIndex);
                    }
                }
                else
                {
                    OnItemDroppedGlobal?.Invoke(item, draggingSlotIndex, evt.position);
                    OnItemDropped?.Invoke(item, draggingSlotIndex, evt.position);
                }
            }
        }

        draggingSlotIndex = -1;
        evt.StopPropagation();
    }

    private void Update()
    {
        if (localPlayerEquipment == null)
        {
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

        if (localPlayerEquipment == null) return;

        if (Input.GetKeyDown(KeyCode.Alpha1)) EquipFromQuickslot(0);
        else if (Input.GetKeyDown(KeyCode.Alpha2)) EquipFromQuickslot(1);
        else if (Input.GetKeyDown(KeyCode.Alpha3)) EquipFromQuickslot(2);
        else if (Input.GetKeyDown(KeyCode.Alpha4)) EquipFromQuickslot(3);
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
        for (int i = 0; i < MaxSlots; i++)
        {
            if (quickslots[i] == null || quickslots[i].Data == null)
            {
                quickslots[i] = item;
                UpdateSlotUI(i);
                return true;
            }
        }
        return false;
    }

    public int GetSlotIndexAtPosition(Vector2 screenPosition)
    {
        if (!uiReady) return -1;
        if (uiDocument != null && uiDocument.panelSettings != null)
        {
            for (int i = 0; i < slotElements.Count; i++)
            {
                var slot = slotElements[i];
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
        if (index >= 0 && index < MaxSlots)
        {
            quickslots[index] = item;
            UpdateSlotUI(index);
        }
    }

    public void RemoveItemFromSlot(int index)
    {
        if (index >= 0 && index < MaxSlots)
        {
            quickslots[index] = null;
            UpdateSlotUI(index);
        }
    }

    public ItemInstance GetItem(int index)
    {
        if (index >= 0 && index < MaxSlots)
        {
            if (quickslots[index] != null && quickslots[index].Data == null)
            {
                return null;
            }
            return quickslots[index];
        }
        return null;
    }

    public void SelectSlot(int index)
    {
        if (!uiReady) return;
        if (index >= 0 && index < MaxSlots)
        {
            selectedSlotIndex = index;
            for (int i = 0; i < slotElements.Count; i++)
            {
                if (i == selectedSlotIndex)
                {
                    slotElements[i].AddToClassList("selected-slot");
                }
                else
                {
                    slotElements[i].RemoveFromClassList("selected-slot");
                }
            }
        }
    }

    public void UpdateSlotUI(int index)
    {
        if (!uiReady) return;
        RefreshSlotVisual(index);
    }

    /// <summary>
    /// 개별 슬롯의 아이콘/스택 라벨을 데이터에 맞게 갱신합니다.
    /// </summary>
    private void RefreshSlotVisual(int index)
    {
        if (index < 0 || index >= slotElements.Count) return;

        var slot = slotElements[index];
        var item = quickslots[index];

        var iconNode = slot.Q<VisualElement>("Icon");
        if (iconNode == null) return;

        iconNode.pickingMode = PickingMode.Ignore;

        if (item != null && item.Data != null && item.Data.itemIcon != null)
        {
            iconNode.style.backgroundImage = new StyleBackground(item.Data.itemIcon);
            iconNode.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(BackgroundSizeType.Contain));

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
            stackLabel.text = item.currentStackCount > 1 ? item.currentStackCount.ToString() : string.Empty;
            stackLabel.visible = item.currentStackCount > 1;
        }
        else
        {
            iconNode.style.backgroundImage = null;
            var stackLabel = slot.Q<Label>("StackLabel");
            if (stackLabel != null) stackLabel.visible = false;
        }
    }
}
