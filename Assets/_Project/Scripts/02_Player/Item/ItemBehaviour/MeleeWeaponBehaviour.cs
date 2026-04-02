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

        NoiseManager.Instance.GenerateNoise(transform.position, data.swingNoiseType);
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
        Ray ray = new Ray(originTransform.position, originTransform.forward);

        if (Physics.SphereCast(ray, data.attackHitRadius, out RaycastHit hit, data.attackRange, player.Equipment.hitLayerMask))
        {
            if (hit.collider.TryGetComponent(out IDamageable target))
            {
                target.TakeDamage(data.damage);
            }
        }
    }
}
