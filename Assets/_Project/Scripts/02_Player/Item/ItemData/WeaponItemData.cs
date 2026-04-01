using UnityEngine;

public abstract class WeaponItemData : ItemData
{
    public override ItemType Type => ItemType.Weapon;

    [Header("Base Weapon Stats")]
    public float damage;
    public float attackRange;
    public float attackRate;
    public float attackNoiseDB;
}

// 근접 무기 (도끼, 칼 등)
[CreateAssetMenu(fileName = "New Melee Weapon", menuName = "Item Data/Weapon/Melee")]
public class MeleeWeaponItemData : WeaponItemData
{
    [Header("Melee Specifics")]
    public float staminaConsumePerSwing;
    public float attackCooldown;

    [Header("Noise Settings")]
    public NoiseData.NoiseType swingNoiseType;
}

// 총기류 (원체스터 등)
[CreateAssetMenu(fileName = "New Firearm", menuName = "Item Data/Weapon/Firearm")]
public class FirearmItemData : WeaponItemData
{
    [Header("Firearm Specifics")]
    public ItemData requiredAmmoType;
    public int maxMagazineSize;
    public float reloadTime;

    [Header("Noise Settings")]
    public NoiseData.NoiseType shootNoiseType;
    public NoiseData.NoiseType reloadNoiseType;
}