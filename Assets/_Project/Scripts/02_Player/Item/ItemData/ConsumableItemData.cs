using FMODUnity;
using System.Collections.Generic;
using System.Text;
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

    public override string DescriptionString
    {
        get
        {
            string baseDesc = base.DescriptionString;

            if (effects == null || effects.Count == 0)
            {
                return baseDesc;
            }

            StringBuilder result = new StringBuilder(baseDesc);

            result.AppendLine();
            result.AppendLine();

            // TOOD: use icons for stats instead of text
            foreach (var effect in effects)
            {
                string iconTag = GetStatIconTag(effect.targetStat);
                string sign = effect.totalAmount > 0 ? "+" : "";
                string durationText = effect.duration > 0f ? $" ({effect.duration}s)" : "";

                result.AppendLine($"{iconTag} {sign}{effect.totalAmount}{durationText}");
            }

            return result.ToString();
        }
    }

    private void Reset()
    {
        useAnimationType = ItemUseAnimationType.ConsumeItem;
    }

    private string GetStatIconTag(ConditionType type)
    {
        const string assetName = "StatIcons";

        return type switch
        {
            ConditionType.Health => $"<sprite=\"{assetName}\" name=\"Health\">",
            ConditionType.Stamina => $"<sprite=\"{assetName}\" name=\"Stamina\">",
            ConditionType.Satiety => $"<sprite=\"{assetName}\" name=\"Satiety\">",
            ConditionType.Coldness => $"<sprite=\"{assetName}\" name=\"Coldness\">",
            _ => ""
        };
    }
}