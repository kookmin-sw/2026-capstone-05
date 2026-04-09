using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class QuickslotUIController : MonoBehaviour
{
    public static QuickslotUIController Instance { get; private set; }

    [SerializeField] private UIDocument uiDocument;
    
    private const int MaxSlots = 4;
    private ItemInstance[] quickslots = new ItemInstance[MaxSlots];
    private int selectedSlotIndex = -1;

    private List<VisualElement> slotElements = new List<VisualElement>();
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
                }
            }
        }
    }

    private void Start()
    {
        EnsureUIInitialized();
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
