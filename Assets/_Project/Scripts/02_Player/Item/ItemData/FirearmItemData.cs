// 총기류 (원체스터 등)
using UnityEngine;

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