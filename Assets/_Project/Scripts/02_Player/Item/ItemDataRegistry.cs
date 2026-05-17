using System.Collections.Generic;
using UnityEngine;

public static class ItemDataRegistry
{
    private const string RegistryResourcePath = "ItemDataRegistry";

    private static readonly Dictionary<string, ItemData> ItemsById = new Dictionary<string, ItemData>();
    private static bool _loadedProjectItems;

    public static void Register(ItemData itemData)
    {
        if (itemData == null || string.IsNullOrEmpty(itemData.itemID))
            return;

        if (!ItemsById.ContainsKey(itemData.itemID))
        {
            ItemsById.Add(itemData.itemID, itemData);
        }
    }

    public static ItemData Find(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
            return null;

        if (ItemsById.TryGetValue(itemId, out ItemData cached))
            return cached;

        LoadProjectItems();
        ItemsById.TryGetValue(itemId, out cached);
        return cached;
    }

    private static void LoadProjectItems()
    {
        if (_loadedProjectItems)
            return;

        _loadedProjectItems = true;

#if UNITY_EDITOR
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:ItemData");
        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            Register(UnityEditor.AssetDatabase.LoadAssetAtPath<ItemData>(path));
        }
#else
        ItemDataRegistryAsset registryAsset = Resources.Load<ItemDataRegistryAsset>(RegistryResourcePath);
        if (registryAsset != null)
        {
            foreach (ItemData itemData in registryAsset.Items)
            {
                Register(itemData);
            }
        }

        ItemData[] allItems = Resources.LoadAll<ItemData>(string.Empty);
        foreach (ItemData itemData in allItems)
        {
            Register(itemData);
        }
#endif
    }
}
