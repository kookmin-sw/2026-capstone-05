using FMOD.Studio;
using FMODUnity;
using System.Collections;
using UnityEngine;

public class ConsumableBehaviour : EquippedItemBehaviour
{
    private Coroutine consumeCoroutine;
    private bool isConsuming = false;

    private EventInstance consumeSoundInstance;

    public override bool Use()
    {
        ConsumableItemData data = itemInstance.Data as ConsumableItemData;
        if (data == null)
        {
            return false;
        }

        if (!base.Use())
        {
            return false;
        }

        consumeSoundInstance = RuntimeManager.CreateInstance(data.consumeSound);
        RuntimeManager.AttachInstanceToGameObject(consumeSoundInstance, gameObject);

        consumeCoroutine = StartCoroutine(ConsumeRoutine(data));
        return true;
    }

    public override void OnAnimationEventTriggered()
    {
        ConsumableItemData data = itemInstance.Data as ConsumableItemData;
        if (data == null)
        {
            return;
        }

        base.OnAnimationEventTriggered();

        Consume();
    }

    private bool Consume()
    {
        ConsumableItemData data = itemInstance.Data as ConsumableItemData;
        if (data == null)
        {
            return false;
        }

        itemInstance.currentStackCount--;

        foreach (var effect in data.effects)
        {
            player.Condition.ApplyEffect(effect);
        }

        NoiseManager.Instance.GenerateNoise(player.transform.position, data.consumeNoiseType);
        Debug.Log($"Consumed {itemInstance.Data.itemName}, remaining stack count: {itemInstance.currentStackCount}");

        if (itemInstance.currentStackCount <= 0)
        {
            ItemEventManager.TriggerItemDestroyed(itemInstance);
            Destroy(gameObject);
        }
        else
        {
            ItemEventManager.TriggerItemStackChanged(itemInstance);
        }

        return true;
    }

    private IEnumerator ConsumeRoutine(ConsumableItemData data)
    {
        isConsuming = true;

        consumeSoundInstance.start();

        player.Animator.SetConsuming(true);

        yield return new WaitForSeconds(data.actionCooldown);

        consumeSoundInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        consumeSoundInstance.release();

        Consume();

        player.Animator.SetConsuming(false);

        isConsuming = false;
    }

    public override void CancelUse()
    {
        base.CancelUse();

        if (isConsuming)
        {
            if (consumeCoroutine != null)
            {
                StopCoroutine(consumeCoroutine);
                consumeCoroutine = null;
            }
            player.Animator.SetConsuming(false);
            isConsuming = false;

            if (consumeSoundInstance.isValid())
            {
                consumeSoundInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
                consumeSoundInstance.release();
            }
        }
    }
}