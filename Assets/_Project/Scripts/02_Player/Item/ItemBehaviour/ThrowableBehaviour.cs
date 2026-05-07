using UnityEngine;

public class ThrowableBehaviour : EquippedItemBehaviour
{
    public override bool Use()
    {
        ThrowableItemData data = itemInstance.Data as ThrowableItemData;
        if (data == null || itemInstance.currentStackCount <= 0)
        {
            return false;
        }

        if (!base.Use())
        {
            return false;
        }

        return true;
    }

    public override void OnAnimationEventTriggered()
    {
        ThrowableItemData data = itemInstance.Data as ThrowableItemData;
        if (data == null) return;

        base.OnAnimationEventTriggered();

        itemInstance.currentStackCount--;

        Transform originTransform = player.CameraTransform != null ? player.CameraTransform : Camera.main.transform;

        // TODO: 발사 위치 조정
        Vector3 spawnPos = originTransform.position + originTransform.forward * 0.5f;

        if (data.thrownPrefab != null)
        {
            GameObject projectile = Instantiate(data.thrownPrefab, spawnPos, originTransform.rotation);
            if (projectile.TryGetComponent(out Rigidbody rb))
            {
                Vector3 force = (originTransform.forward * data.throwForce) + (originTransform.up * 2f); // 2f는 upwardForce로 빼면 좋음
                rb.AddForce(force, ForceMode.VelocityChange);
            }
        }

        NoiseManager.Instance.GenerateNoise(transform.position, data.throwNoiseType);

        if (itemInstance.currentStackCount <= 0)
        {
            ItemEventManager.TriggerItemDestroyed(itemInstance);
            Destroy(gameObject);
        }
        else
        {
            ItemEventManager.TriggerItemStackChanged(itemInstance);
        }
    }
}