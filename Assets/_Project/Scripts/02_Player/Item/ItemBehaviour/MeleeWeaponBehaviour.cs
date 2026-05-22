using FMODUnity;
using UnityEngine;

public class MeleeWeaponBehaviour : EquippedItemBehaviour
{
    public override bool Use()
    {
        MeleeWeaponItemData data = itemInstance.Data as MeleeWeaponItemData;
        if (data == null)
        {
            return false;
        }
        if (!player.Condition.UseStamina(data.staminaConsumePerSwing))
        {
            Debug.Log("Not enough stamina to attack.");
            return false;
        }

        if (!base.Use())
        {
            return false;
        }

        RuntimeManager.PlayOneShot(data.swingSound, player.transform.position);

        return true;
    }

    public override void OnAnimationEventTriggered()
    {
        MeleeWeaponItemData data = itemInstance.Data as MeleeWeaponItemData;
        if (data == null)
        {
            return;
        }

        base.OnAnimationEventTriggered();

        if (!PlayerAimRayProvider.TryGetAimRay(player, out Ray aimRay))
        {
            return;
        }

        Vector3 checkCenter = aimRay.origin + aimRay.direction * data.attackRange;

        Collider[] hitColliders = Physics.OverlapSphere(checkCenter, data.attackHitRadius, player.Equipment.hitLayerMask);
        if (hitColliders.Length > 0)
        {
            foreach (var hit in hitColliders)
            {
                if (hit.TryGetComponent(out IDamageable target))
                {
                    DamageInfo damageInfo = new DamageInfo
                    {
                        damageAmount = data.damage,
                        hitPoint = hit.ClosestPoint(checkCenter),
                        hitNormal = (hit.ClosestPoint(checkCenter) - checkCenter).normalized,
                        attacker = player.gameObject
                    };
                    target.TakeDamage(damageInfo);
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
