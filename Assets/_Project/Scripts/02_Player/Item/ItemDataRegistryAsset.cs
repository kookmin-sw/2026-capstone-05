using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemDataRegistry", menuName = "Items/Item Data Registry")]
public class ItemDataRegistryAsset : ScriptableObject
{
    [SerializeField] private List<ItemData> items = new List<ItemData>();

    public IReadOnlyList<ItemData> Items => items;

#if UNITY_EDITOR
    public void SetItems(IEnumerable<ItemData> itemData)
    {
        items.Clear();
        if (itemData == null)
        {
            return;
        }

        foreach (ItemData item in itemData)
        {
            if (item != null && !items.Contains(item))
            {
                items.Add(item);
            }
        }
    }
#endif
}
