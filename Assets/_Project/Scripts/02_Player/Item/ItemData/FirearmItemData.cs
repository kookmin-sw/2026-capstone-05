// 총기류 (원체스터 등)
using FMODUnity;
using UnityEngine;

[CreateAssetMenu(fileName = "New Firearm", menuName = "Item Data/Weapon/Firearm")]
public class FirearmItemData : WeaponItemData
{
    [Header("Firearm Specifics")]
    public float attackRate; // 발사 속도 (RPM)
    public ItemData requiredAmmoType;
    public int maxMagazineSize; // 탄창 최대 크기
    public float reloadTime; // 재장전 시간 (초)
    public float recoilForce; // 반동 세기

    [Header("Noise Settings")]
    public NoiseData.NoiseType shootNoiseType = NoiseData.NoiseType.GunShot;
    public NoiseData.NoiseType reloadNoiseType = NoiseData.NoiseType.GunReload;
    public NoiseData.NoiseType dryFireNoiseType = NoiseData.NoiseType.DryFire;

    [Header("Sound Settings")]
    public EventReference shootSound;
    public EventReference dryFireSound;

    private void Reset()
    {
        useAnimationType = ItemUseAnimationType.None;
        poseType = ItemPoseType.Rifle;
    }
}