using UnityEngine;

public class ConsumableBehaviour : EquippedItemBehaviour
{
    public override void Use()
    {
        ConsumableItemData data = itemInstance.Data as ConsumableItemData;
        if (data == null)
        {
            return;
        }

        base.Use();
    }

    public override void OnAnimationEventTriggered()
    {
        ConsumableItemData data = itemInstance.Data as ConsumableItemData;
        if (data == null)
        {
            return;
        }

        base.OnAnimationEventTriggered();

        itemInstance.currentStackCount--;

        foreach (var effect in data.effects)
        {
            player.Condition.ApplyEffect(effect);
        }

        NoiseManager.Instance.GenerateNoise(player.transform.position, data.consumeNoiseType);
        Debug.Log($"Consumed {itemInstance.Data.itemName}, remaining stack count: {itemInstance.currentStackCount}");

        if (itemInstance.currentStackCount <= 0)
        {
            // TODO: 인벤토리 매니저에 이 아이템을 리스트에서 완전히 지우라고 알림
            Destroy(gameObject);
        }
    }
}