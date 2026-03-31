using UnityEngine;

[CreateAssetMenu(fileName = "New Throwable", menuName = "Item Data/Throwable")]
public class ThrowableItemData : ItemData
{
    public override ItemType Type => ItemType.Throwable;

    [Header("Throw Settings")]
    public GameObject thrownPrefab; // 실제로 날아가는 물리 오브젝트 프리팹
    public float throwForce;

    [Header("Impact Settings")]
    public bool explodesOnImpact;
    public float impactDamage;

    [Header("Noise Settings")]
    public NoiseData.NoiseType throwNoiseType;
    public NoiseData.NoiseType impactNoiseType;
}