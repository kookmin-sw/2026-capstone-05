using UnityEngine;

public class MeleeWeaponBehaviour : EquippedItemBehaviour
{
    private float lastAttackTime = 0f;

    public override void Use()
    {
        MeleeWeaponItemData data = itemInstance.Data as MeleeWeaponItemData;
        if (data == null)
        {
            return;
        }
        if (Time.time - lastAttackTime < data.attackCooldown)
        {
            Debug.Log("Attack is on cooldown.");
            return;
        }
        if (!player.Condition.UseStamina(data.staminaConsumePerSwing))
        {
            Debug.Log("Not enough stamina to attack.");
            return;
        }

        lastAttackTime = Time.time;

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
