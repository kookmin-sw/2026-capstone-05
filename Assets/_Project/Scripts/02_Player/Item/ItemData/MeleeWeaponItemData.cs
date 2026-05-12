// 근접 무기 (도끼, 칼 등)
using UnityEngine;

[CreateAssetMenu(fileName = "New Melee Weapon", menuName = "Item Data/Weapon/Melee")]
public class MeleeWeaponItemData : WeaponItemData
{
    [Header("Melee Specifics")]
    public float staminaConsumePerSwing;

    [Header("Noise Settings")]
    public NoiseData.NoiseType swingNoiseType = NoiseData.NoiseType.MeleeSwing;
    public NoiseData.NoiseType hitNoiseType = NoiseData.NoiseType.MeleeHit;

    private void Reset()
    {
        useAnimationType = ItemUseAnimationType.MeleeAttack;
    }
}