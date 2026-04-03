using UnityEngine;

[CreateAssetMenu(fileName = "New Consumable", menuName = "Item Data/Consumable")]
public class ConsumableItemData : ItemData
{
    public override ItemType Type => ItemType.Consumable;

    [Header("Restoration")]
    public float healthRestore;
    public float staminaRestore;
    public float satietyRestore;
    public float coldnessReduce;

    [Header("Noise Settings")]
    public NoiseData.NoiseType consumeNoiseType;
}