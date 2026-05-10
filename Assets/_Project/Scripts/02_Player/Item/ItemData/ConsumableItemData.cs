using FMODUnity;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Consumable", menuName = "Item Data/Consumable")]
public class ConsumableItemData : ItemData
{
    public override ItemType Type => ItemType.Consumable;

    [Header("Effect Settings")]
    public List<StatusEffectData> effects = new List<StatusEffectData>();

    [Header("Noise Settings")]
    public NoiseData.NoiseType consumeNoiseType;

    [Header("Sound Settings")]
    public EventReference consumeSound;

    private void Reset()
    {
        useAnimationType = ItemUseAnimationType.ConsumeItem;
    }
}