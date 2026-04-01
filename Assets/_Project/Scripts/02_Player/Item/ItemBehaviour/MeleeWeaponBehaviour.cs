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
}
