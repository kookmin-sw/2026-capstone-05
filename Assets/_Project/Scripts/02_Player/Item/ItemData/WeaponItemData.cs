using UnityEngine;

public abstract class WeaponItemData : ItemData
{
    public override ItemType Type => ItemType.Weapon;

    [Header("Base Weapon Stats")]
    public float damage;
    public float attackRange; // 근접 무기는 공격 범위, 총기류는 사거리로 사용
    public float attackHitRadius; // 공격이 명중하는 범위 (근접 무기는 타격 범위, 총기류는 탄착 범위)
}

// 근접 무기 (도끼, 칼 등)
[CreateAssetMenu(fileName = "New Melee Weapon", menuName = "Item Data/Weapon/Melee")]
public class MeleeWeaponItemData : WeaponItemData
{
    [Header("Melee Specifics")]
    public float staminaConsumePerSwing;

    [Header("Noise Settings")]
    public NoiseData.NoiseType swingNoiseType;
}

// 총기류 (원체스터 등)
[CreateAssetMenu(fileName = "New Firearm", menuName = "Item Data/Weapon/Firearm")]
public class FirearmItemData : WeaponItemData
{
    [Header("Firearm Specifics")]
    public float attackRate; // 발사 속도 (RPM)
    public ItemData requiredAmmoType;
    public int maxMagazineSize; // 탄창 최대 크기
    public float reloadTime; // 재장전 시간 (초)

    [Header("Noise Settings")]
    public NoiseData.NoiseType shootNoiseType;
    public NoiseData.NoiseType reloadNoiseType;
}