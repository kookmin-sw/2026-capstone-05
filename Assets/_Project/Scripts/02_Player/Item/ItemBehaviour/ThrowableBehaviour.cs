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
        Vector3 spawnPos = originTransform.position + originTransform.forward * 0.5f + originTransform.right * 0.5f;

        if (data.thrownPrefab != null)
        {
            GameObject projectile = Instantiate(data.thrownPrefab, spawnPos, originTransform.rotation);
            if (projectile.TryGetComponent(out ThrownItem thrownItem))
            {
                ItemInstance instanceCopy = new ItemInstance(itemInstance.Data, 1)
                {
                    currentRotation = itemInstance.currentRotation
                };
                thrownItem.Initialize(instanceCopy);
            }
            if (projectile.TryGetComponent(out Rigidbody rb))
            {
                Vector3 force = (originTransform.forward * data.throwForce) + (originTransform.up * 2f); // 2f는 upwardForce로 빼면 좋음
                Vector3 randomTorque = new Vector3(Random.Range(-5f, 5f), Random.Range(-5f, 5f), Random.Range(-5f, 5f));
                rb.AddForce(force, ForceMode.VelocityChange);
                rb.AddTorque(randomTorque, ForceMode.VelocityChange);
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