using UnityEngine;

public class MeleeWeaponBehaviour : EquippedItemBehaviour
{
    public override void Use()
    {
        MeleeWeaponItemData data = itemInstance.Data as MeleeWeaponItemData;
        if (data == null)
        {
            return;
        }
        if (!player.Condition.UseStamina(data.staminaConsumePerSwing))
        {
            Debug.Log("Not enough stamina to attack.");
            return;
        }

        base.Use();
    }

    public override void OnAnimationEventTriggered()
    {
        MeleeWeaponItemData data = itemInstance.Data as MeleeWeaponItemData;
        if (data == null)
        {
            return;
        }

        base.OnAnimationEventTriggered();

        Transform originTransform = player.CameraTransform != null ? player.CameraTransform : Camera.main.transform;
        Vector3 checkCenter = originTransform.position + originTransform.forward * data.attackRange;

        Collider[] hitColliders = Physics.OverlapSphere(checkCenter, data.attackHitRadius, player.Equipment.hitLayerMask);
        if (hitColliders.Length > 0)
        {
            foreach (var hit in hitColliders)
            {
                if (hit.TryGetComponent(out IDamageable target))
                {
                    target.TakeDamage(data.damage);
                }
            }
            NoiseManager.Instance.GenerateNoise(checkCenter, data.hitNoiseType);
        }
        else
        {
            NoiseManager.Instance.GenerateNoise(checkCenter, data.swingNoiseType);
        }
    }
}
