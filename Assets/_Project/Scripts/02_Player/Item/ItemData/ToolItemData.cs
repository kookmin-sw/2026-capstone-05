using UnityEngine;

[CreateAssetMenu(fileName = "New Tool", menuName = "Item Data/Tool")]
public class ToolItemData : ItemData
{
    public override ItemType Type => ItemType.Tool;

    [Header("Tool Stats")]
    public float useDuration; // 사용하는 데 걸리는 시간
    // TODO: 내구도?

    [Header("Noise Settings")]
    public NoiseData.NoiseType useNoiseType;
}