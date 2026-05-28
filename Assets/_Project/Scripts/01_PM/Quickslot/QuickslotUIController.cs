using UnityEngine.InputSystem;
using System;
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
        public ItemInstance Item;
        public Sprite IconSprite;
        public int StackCount = -1;
    }

    private List<QuickslotViewElement> slotViews = new List<QuickslotViewElement>();

    private int selectedSlotIndex = -1;

    private VisualElement dragGhostIcon;
    private int draggingSlotIndex = -1;
    private int activeQuickslotPointerId = -1;
    private bool isDragging = false;
    private bool uiReady = false;
    private readonly List<int> highlightedSlotIndexes = new List<int>();
    private readonly HashSet<int> highlightedSlotSet = new HashSet<int>();
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
        FixedAspectRatioManager.RequestRefresh();
        
        // 퀵슬롯 UI는 평상시 보이되, 상점(Shop)이나 다른 모달이 열리면 가려질 수 있도록 상점 뷰에서는 sortingOrder를 높이거나,
        // 상점이 열릴 때 퀵슬롯을 끄는 로직을 추가.
        
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
        ItemDescriptionPanelController.HideImmediate();
        if (Instance == this)
        {
            model?.Dispose();
            Instance = null;
            OnQuickslotSplitRequested = null;
            OnInitialized = null;
        }
    }

    private void OnDisable()
    {
        ItemDescriptionPanelController.HideImmediate();
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
            slotElement.RegisterCallback<PointerEnterEvent>(evt => OnPointerEnter(evt, slotIndex));
            slotElement.RegisterCallback<PointerLeaveEvent>(evt => OnPointerLeave(evt, slotIndex));
        }

        // 고스트 아이콘 생성
        dragGhostIcon = new VisualElement();
        dragGhostIcon.style.position = Position.Absolute;
        dragGhostIcon.style.visibility = Visibility.Hidden;
        dragGhostIcon.style.width = 60;
        dragGhostIcon.style.height = 60;
        dragGhostIcon.pickingMode = PickingMode.Ignore;
        root.Add(dragGhostIcon);
        FixedAspectRatioManager.RequestRefresh();

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
    /// <summary>Emitted after cancelling quickslot drag (right-click quantity split).</summary>
    public static event Action<ItemInstance, int, Vector2> OnQuickslotSplitRequested;

    private void OnPointerEnter(PointerEnterEvent evt, int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slotViews.Count) return;

        var itemInstance = model.Get(slotIndex);
        if (itemInstance == null || itemInstance.Data == null)
        {
            ItemDescriptionPanelController.Hide(slotViews[slotIndex].Root);
            return;
        }

        ItemDescriptionPanelController.Show(itemInstance, slotViews[slotIndex].Root);
    }

    private void OnPointerLeave(PointerLeaveEvent evt, int slotIndex)
    {
        if (isDragging && draggingSlotIndex == slotIndex) return;
        if (slotIndex >= 0 && slotIndex < slotViews.Count)
        {
            ItemDescriptionPanelController.Hide(slotViews[slotIndex].Root);
        }
    }

    private void OnPointerDown(PointerDownEvent evt, int slotIndex)
    {
        var itemInstance = model.Get(slotIndex);
        if (evt.button != 0 || itemInstance == null) return;

        if (evt.shiftKey && GlobalDragDropRouter.TryQuickMoveQuickslotItemToInventory(slotIndex))
        {
            ItemDescriptionPanelController.Hide(slotViews[slotIndex].Root);
            evt.StopPropagation();
            return;
        }

        isDragging = true;
        draggingSlotIndex = slotIndex;
        activeQuickslotPointerId = evt.pointerId;
        lastPointerPos = evt.position;
        originalDragRotation = itemInstance.currentRotation;

        var slotElement = slotViews[slotIndex].Root;
        slotElement.CapturePointer(evt.pointerId);
        ItemDescriptionPanelController.BeginDragLock(itemInstance, slotElement);

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
        UpdateDropHighlight(itemInstance, DragSource.Quickslot, slotIndex, null, evt.position);
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
            UpdateDropHighlight(itemInstance, DragSource.Quickslot, draggingSlotIndex, null, evt.position);
        }
    }

    public void ReceiveDrop(ItemInstance item, Systems.GridInventory.DragSource source, int sourceIndex, int targetQuickslotIndex, GridInventoryModel sourceModel)
    {
        var targetItem = model.Get(targetQuickslotIndex);

        if (source == DragSource.Quickslot)
        {
            if (sourceIndex != targetQuickslotIndex)
            {
                model.MergeOrSwap(sourceIndex, targetQuickslotIndex);
                item.currentRotation = ItemRotation.Deg0;
            }
            else
            {
                item.currentRotation = originalDragRotation;
            }
        }
        else if (sourceModel != null) // From Grid (Inventory/Loot)
        {
            if (targetItem != null && targetItem.Data == item.Data && item.Data.maxStackSize > 1)
            {
                // Stack
                int spaceLeft = item.Data.maxStackSize - targetItem.currentStackCount;
                if (spaceLeft <= 0)
                {
                    GlobalDragDropRouter.RevertDrop(item, source, sourceIndex, sourceModel);
                    return;
                }

                int moveQty = Mathf.Min(item.currentStackCount, spaceLeft);
                targetItem.currentStackCount += moveQty;
                item.currentStackCount -= moveQty;

                if (item.currentStackCount <= 0)
                {
                    sourceModel.TryRemove(item);
                }
                else
                {
                    sourceModel.Items.Invoke();
                    GlobalDragDropRouter.RevertDrop(item, source, sourceIndex, sourceModel);
                }

                RefreshSlotVisual(targetQuickslotIndex);
            }
            else
            {
                // Swap or Place
                var sourceOldPos = sourceModel.GetItemAnchorPosition(item);
                sourceModel.TryRemove(item);

                item.currentRotation = ItemRotation.Deg0;

                if (targetItem != null)
                {
                    // Try to place the targetItem back into the source grid
                    if (sourceOldPos.x != -1 && sourceModel.CanPlaceItem(targetItem, sourceOldPos.x, sourceOldPos.y))
                    {
                        sourceModel.PlaceItem(targetItem, sourceOldPos.x, sourceOldPos.y);
                        SetItemInSlot(targetQuickslotIndex, item);
                        sourceModel.Items.Invoke();
                    }
                    else
                    {
                        if (!sourceModel.TryAdd(targetItem))
                        {
                            // Revert
                            sourceModel.PlaceItem(item, sourceOldPos.x, sourceOldPos.y);
                            GlobalDragDropRouter.RevertDrop(item, source, sourceIndex, sourceModel);
                            return;
                        }
                        else
                        {
                            SetItemInSlot(targetQuickslotIndex, item);
                            sourceModel.Items.Invoke();
                        }
                    }
                }
                else
                {
                    SetItemInSlot(targetQuickslotIndex, item);
                    sourceModel.Items.Invoke();
                }
            }
        }
    }

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

        ResetDropHighlights();
        OnItemDragEndGlobal?.Invoke();
        if (draggingSlotIndex >= 0 && draggingSlotIndex < slotViews.Count)
        {
            ItemDescriptionPanelController.EndDragLock(slotViews[draggingSlotIndex].Root);
        }

        if (draggingSlotIndex >= 0 && draggingSlotIndex < MaxSlots)
        {
            var item = model.Get(draggingSlotIndex);
            if (item != null)
            {
                GlobalDragDropRouter.ProcessDrop(item, DragSource.Quickslot, draggingSlotIndex, evt.position, null);
                
                // 이벤트 발생 후에도 아이템이 퀵슬롯에 그대로 남아있다면(아무도 처리하지 않았다면) 회전 상태를 원래대로 복구
                if (model.Get(draggingSlotIndex) == item)
                {
                    item.currentRotation = originalDragRotation;
                }
            }
        }

        draggingSlotIndex = -1;
        activeQuickslotPointerId = -1;
        evt.StopPropagation();
    }

    void FinishQuickslotDragForSplitRequest()
    {
        if (!isDragging || draggingSlotIndex < 0) return;

        int idx = draggingSlotIndex;
        Vector2 pos = lastPointerPos;
        var item = model.Get(idx);

        isDragging = false;
        if (activeQuickslotPointerId >= 0 && idx >= 0 && idx < slotViews.Count)
        {
            slotViews[idx].Root.ReleasePointer(activeQuickslotPointerId);
        }

        activeQuickslotPointerId = -1;
        draggingSlotIndex = -1;

        if (dragGhostIcon != null)
            dragGhostIcon.style.visibility = Visibility.Hidden;

        ResetDropHighlights();
        OnItemDragEndGlobal?.Invoke();
        if (idx >= 0 && idx < slotViews.Count)
        {
            ItemDescriptionPanelController.EndDragLock(slotViews[idx].Root);
        }

        if (item != null && item.currentStackCount > 1)
            OnQuickslotSplitRequested?.Invoke(item, idx, pos);
    }

    private float playerSearchTimer = 0f;

    private void Update()
    {
        if (isDragging && dragGhostIcon != null && draggingSlotIndex >= 0)
        {
            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            {
                FinishQuickslotDragForSplitRequest();
                return;
            }

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
                    UpdateDropHighlight(itemInstance, DragSource.Quickslot, draggingSlotIndex, null, lastPointerPos);
                }
            }
        }

        if (localPlayerEquipment == null)
        {
            playerSearchTimer -= Time.deltaTime;
            if (playerSearchTimer <= 0f)
            {
                playerSearchTimer = 1f; // 1초 간격으로 플레이어 탐색 (성능 최적화)
                if (LocalPlayerReferenceResolver.TryGetLocalPlayer(out PlayerController controller))
                {
                    localPlayerEquipment = controller.Equipment;
                }
            }
        }

        if (localPlayerEquipment == null || Keyboard.current == null) return;
        if (PauseMenuManager.IsAnyUIOpen()) return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame) EquipFromQuickslot(0);
        else if (Keyboard.current.digit2Key.wasPressedThisFrame) EquipFromQuickslot(1);
        else if (Keyboard.current.digit3Key.wasPressedThisFrame) EquipFromQuickslot(2);
        else if (Keyboard.current.digit4Key.wasPressedThisFrame) EquipFromQuickslot(3);
    }

    private void EquipFromQuickslot(int index)
    {
        localPlayerEquipment.CancelCurrentItemUse();
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

    public bool HasEmptySlot()
    {
        return model != null && model.HasEmptySlot();
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

    public void ResetDropHighlights()
    {
        if (!uiReady) return;

        for (int i = 0; i < highlightedSlotIndexes.Count; i++)
        {
            int index = highlightedSlotIndexes[i];
            if (index >= 0 && index < slotViews.Count && slotViews[index].Root != null)
            {
                slotViews[index].Root.style.backgroundColor = new StyleColor(StyleKeyword.Null);
            }
        }

        highlightedSlotIndexes.Clear();
        highlightedSlotSet.Clear();
    }

    public void UpdateDropHighlight(ItemInstance item, DragSource source, int sourceIndex, GridInventoryModel sourceModel,
        Vector2 screenPosition)
    {
        ResetDropHighlights();

        int targetQuickslotIndex = GetSlotIndexAtPosition(screenPosition);
        if (targetQuickslotIndex < 0) return;

        bool isValid = CanDropOnQuickslot(item, source, sourceIndex, targetQuickslotIndex, sourceModel);
        Color color = isValid ? new Color(0f, 1f, 0f, 0.3f) : new Color(1f, 0f, 0f, 0.3f);
        SetDropHighlight(targetQuickslotIndex, color);
    }

    private void SetDropHighlight(int index, Color color)
    {
        if (!uiReady || index < 0 || index >= slotViews.Count || slotViews[index].Root == null) return;

        slotViews[index].Root.style.backgroundColor = new StyleColor(color);
        if (highlightedSlotSet.Add(index))
        {
            highlightedSlotIndexes.Add(index);
        }
    }

    private bool CanDropOnQuickslot(ItemInstance item, DragSource source, int sourceIndex, int targetQuickslotIndex,
        GridInventoryModel sourceModel)
    {
        if (item == null || item.Data == null) return false;

        var targetItem = model.Get(targetQuickslotIndex);
        if (source == DragSource.Quickslot)
        {
            if (sourceIndex == targetQuickslotIndex) return true;
            if (targetItem == null) return true;
            if (targetItem.Data == item.Data && item.Data.maxStackSize > 1)
            {
                return targetItem.currentStackCount < targetItem.Data.maxStackSize;
            }

            return true;
        }

        if (sourceModel == null) return false;
        if (targetItem == null) return true;

        if (targetItem.Data == item.Data && item.Data.maxStackSize > 1)
        {
            return targetItem.currentStackCount < targetItem.Data.maxStackSize;
        }

        var sourceOldPos = sourceModel.GetItemAnchorPosition(item);
        if (sourceOldPos.x == -1 || sourceOldPos.y == -1) return false;

        return sourceModel.CanPlaceItem(targetItem, sourceOldPos.x, sourceOldPos.y, item);
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
            if (view.Item != item || view.IconSprite != item.Data.itemIcon)
            {
                view.Item = item;
                view.IconSprite = item.Data.itemIcon;
                view.Icon.style.backgroundImage = new StyleBackground(item.Data.itemIcon);
                view.Icon.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(BackgroundSizeType.Contain));
            }

            if (view.StackCount != item.currentStackCount)
            {
                view.StackCount = item.currentStackCount;
                view.StackLabel.text = item.currentStackCount > 1 ? item.currentStackCount.ToString() : string.Empty;
                view.StackLabel.visible = item.currentStackCount > 1;
            }
        }
        else
        {
            if (view.Item != null || view.IconSprite != null)
            {
                ItemDescriptionPanelController.Hide(view.Root);
                view.Item = null;
                view.IconSprite = null;
                view.StackCount = -1;
                view.Icon.style.backgroundImage = null;
                view.StackLabel.text = string.Empty;
                view.StackLabel.visible = false;
            }
        }

        if (index == selectedSlotIndex && localPlayerEquipment != null)
        {
            if (item == null)
            {
                localPlayerEquipment.UnequipItem();
            }
            else 
            {
                localPlayerEquipment.EquipItem(item);
            }
        }
    }
}
