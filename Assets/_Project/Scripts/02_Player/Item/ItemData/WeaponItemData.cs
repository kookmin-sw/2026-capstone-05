using UnityEngine;

public abstract class WeaponItemData : ItemData
{
    public override ItemType Type => ItemType.Weapon;

    [Header("Base Weapon Stats")]
    public float damage;
    public float attackRange; // 근접 무기는 공격 범위, 총기류는 사거리로 사용
    public float attackHitRadius; // 공격이 명중하는 범위 (근접 무기는 타격 범위, 총기류는 탄착 범위)
}