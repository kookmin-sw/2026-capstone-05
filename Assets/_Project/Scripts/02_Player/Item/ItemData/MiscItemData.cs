using UnityEngine;

[CreateAssetMenu(fileName = "New Misc", menuName = "Item Data/Misc")]
public class MiscItemData : ItemData
{
    public override ItemType Type => ItemType.Misc;
}