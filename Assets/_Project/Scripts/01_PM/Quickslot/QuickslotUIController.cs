using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Systems.GridInventory;

public class QuickslotUIController : MonoBehaviour
{
    public static QuickslotUIController Instance { get; private set; }

    [SerializeField] private UIDocument uiDocument;
    
    private const int MaxSlots = 4;
    private ItemInstance[] quickslots = new ItemInstance[MaxSlots];
    private int selectedSlotIndex = -1;

    private List<VisualElement> slotElements = new List<VisualElement>();
    private VisualElement dragGhostIcon;
    private int draggingSlotIndex = -1;
    private bool isDragging = false;

    private PlayerEquipment localPlayerEquipment;

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

        // 도메인 리로드 비활성화 시 캐시가 남는 현상을 방지하기 위해 Awake에서 명시적으로 배열을 재할당합니다.
        quickslots = new ItemInstance[MaxSlots];
        selectedSlotIndex = -1;
        slotElements.Clear();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void EnsureUIInitialized()
    {
        if (slotElements.Count == MaxSlots) return;

        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (uiDocument != null && uiDocument.rootVisualElement != null)
        {
            slotElements.Clear();
            var root = uiDocument.rootVisualElement;
            for (int i = 0; i < MaxSlots; i++)
            {
                var slot = root.Q<VisualElement>($"Quickslot{i + 1}");
                if (slot != null)
                {
                    slotElements.Add(slot);
                    
                    int slotIndex = i;
                    slot.RegisterCallback<PointerDownEvent>(evt => OnPointerDown(evt, slotIndex));
                    slot.RegisterCallback<PointerMoveEvent>(OnPointerMove);
                    slot.RegisterCallback<PointerUpEvent>(OnPointerUp);
                    slot.pickingMode = PickingMode.Position;
                }
            }

            if (dragGhostIcon == null)
            {
                dragGhostIcon = new VisualElement();
                dragGhostIcon.style.position = Position.Absolute;
                dragGhostIcon.style.visibility = Visibility.Hidden;
                dragGhostIcon.style.width = 60; // quickslot size
                dragGhostIcon.style.height = 60;
                dragGhostIcon.pickingMode = PickingMode.Ignore;
                root.Add(dragGhostIcon);
            }
        }
    }

    private void Start()
    {
        EnsureUIInitialized();
    }

    private void OnPointerDown(PointerDownEvent evt, int slotIndex)
    {
        if (evt.button != 0 || quickslots[slotIndex] == null || quickslots[slotIndex].Data == null) return;
        
        isDragging = true;
        draggingSlotIndex = slotIndex;
        
        var slotElement = slotElements[slotIndex];
        slotElement.CapturePointer(evt.pointerId);
        
        // Setup Ghost
        dragGhostIcon.style.backgroundImage = new StyleBackground(quickslots[slotIndex].Data.itemIcon);
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
            dragGhostIcon.style.left = position.x - (dragGhostIcon.style.width.value.value / 2);
            dragGhostIcon.style.top = position.y - (dragGhostIcon.style.height.value.value / 2);
        }
    }

    public delegate void QuickslotItemDropAction(ItemInstance item, int sourceQuickslotIndex, Vector2 screenPosition);
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
                    // Swap within quickslots
                    var temp = quickslots[targetQuickslot];
                    quickslots[targetQuickslot] = item;
                    quickslots[draggingSlotIndex] = temp;
                    UpdateSlotUI(targetQuickslot);
                    UpdateSlotUI(draggingSlotIndex);
                }
                else
                {
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

    /// <summary>
    /// 빈 슬롯에 아이템을 추가합니다.
    /// </summary>
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
        EnsureUIInitialized();
        if (uiDocument != null && uiDocument.panelSettings != null)
        {
            // Vector2 screenPosition usually from Input/Pointer events is (0,0) at top-left or bottom-left?
            // UIElements pointer events have (0,0) at top-left.
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
        EnsureUIInitialized();
        if (index >= 0 && index < MaxSlots)
        {
            selectedSlotIndex = index;
            // UI에 선택된 슬롯 표시 (Highlight 등)
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

    private void UpdateSlotUI(int index)
    {
        EnsureUIInitialized();
        if (index >= 0 && index < slotElements.Count)
        {
            var slot = slotElements[index];
            var item = quickslots[index];
            
            var iconNode = slot.Q<VisualElement>("Icon");
            if (iconNode != null)
            {
                if (item != null && item.Data != null && item.Data.itemIcon != null)
                {
                    iconNode.style.backgroundImage = new StyleBackground(item.Data.itemIcon);
                    // 이미지가 깨지지 않게 비율 유지하도록 스케일 모드 변경
                    iconNode.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(BackgroundSizeType.Contain));
                }
                else
                {
                    iconNode.style.backgroundImage = null;
                }
            }
        }
    }
}
