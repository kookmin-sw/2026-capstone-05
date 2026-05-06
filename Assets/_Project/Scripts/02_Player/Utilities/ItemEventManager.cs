using System;

public static class ItemEventManager
{
    // 스택 수가 변경되었을 때
    public static event Action<ItemInstance> OnItemStackChangedGlobal;

    // 아이템이 완전히 소진되거나 파괴되었을 때
    public static event Action<ItemInstance> OnItemDestroyedGlobal;

    public static void TriggerItemStackChanged(ItemInstance item)
    {
        OnItemStackChangedGlobal?.Invoke(item);
    }

    public static void TriggerItemDestroyed(ItemInstance item)
    {
        OnItemDestroyedGlobal?.Invoke(item);
    }
}