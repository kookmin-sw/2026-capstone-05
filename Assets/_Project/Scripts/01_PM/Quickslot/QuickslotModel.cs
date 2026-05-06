using System;
using Systems.GridInventory;

public class QuickslotModel
{
    private const int MaxSlots = 4;
    private ItemInstance[] slots = new ItemInstance[MaxSlots];
    
    public event Action<int> OnSlotChanged;

    public void Initialize()
    {
        slots = new ItemInstance[MaxSlots];

        ItemEventManager.OnItemStackChangedGlobal += HandleItemStackChanged;
        ItemEventManager.OnItemDestroyedGlobal += HandleItemDestroyed;
    }

    public void Dispose()
    {
        ItemEventManager.OnItemStackChangedGlobal -= HandleItemStackChanged;
        ItemEventManager.OnItemDestroyedGlobal -= HandleItemDestroyed;
    }

    private void HandleItemStackChanged(ItemInstance item)
    {
        for (int i = 0; i < MaxSlots; i++)
        {
            // 참조(레퍼런스) 비교: 이벤트로 넘어온 아이템이 내 슬롯에 있는 그 아이템인가?
            if (slots[i] == item) 
            {
                OnSlotChanged?.Invoke(i); // 숫자 텍스트만 갱신하도록 UI에 알림
                break; // 같은 아이템이 여러 슬롯에 들어갈 수 없다면 break로 최적화
            }
        }
    }

    private void HandleItemDestroyed(ItemInstance item)
    {
        for (int i = 0; i < MaxSlots; i++)
        {
            if (slots[i] == item)
            {
                Set(i, null); // 슬롯을 완전히 비움 (내부적으로 OnSlotChanged 자동 호출됨)
                break;
            }
        }
    }

    public ItemInstance Get(int index)
    {
        if (index >= 0 && index < MaxSlots)
            return slots[index] == null || slots[index].Data == null ? null : slots[index];
        return null;
    }

    public void Set(int index, ItemInstance item)
    {
        if (index >= 0 && index < MaxSlots)
        {
            slots[index] = item;
            OnSlotChanged?.Invoke(index);
        }
    }

    public void MergeOrSwap(int sourceIdx, int targetIdx)
    {
        var src = Get(sourceIdx);
        var tgt = Get(targetIdx);

        if (src == null) return;

        if (tgt != null && tgt.Data == src.Data && tgt.Data.maxStackSize > 1)
        {
            int total = src.currentStackCount + tgt.currentStackCount;
            if (total <= tgt.Data.maxStackSize)
            {
                tgt.currentStackCount = total;
                Set(sourceIdx, null);
            }
            else
            {
                tgt.currentStackCount = tgt.Data.maxStackSize;
                src.currentStackCount = total - tgt.Data.maxStackSize;
                Set(sourceIdx, src); // 잔여량 업데이트
            }
            Set(targetIdx, tgt);
        }
        else
        {
            // Swap
            slots[targetIdx] = src;
            slots[sourceIdx] = tgt;
            OnSlotChanged?.Invoke(targetIdx);
            OnSlotChanged?.Invoke(sourceIdx);
        }
    }

    public bool AddToEmpty(ItemInstance item)
    {
        for (int i = 0; i < MaxSlots; i++)
        {
            if (slots[i] == null || slots[i].Data == null)
            {
                Set(i, item);
                return true;
            }
        }
        return false;
    }
}