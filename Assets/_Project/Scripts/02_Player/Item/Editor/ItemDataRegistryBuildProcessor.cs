using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class ItemDataRegistryBuildProcessor : IPreprocessBuildWithReport
{
    private const string RegistryDirectory = "Assets/_Project/Resources";
    private const string RegistryPath = RegistryDirectory + "/ItemDataRegistry.asset";

    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        RebuildRegistry();
    }

    [MenuItem("Tools/Items/Rebuild Item Data Registry")]
    public static void RebuildRegistry()
    {
        Directory.CreateDirectory(RegistryDirectory);

        ItemDataRegistryAsset registry = AssetDatabase.LoadAssetAtPath<ItemDataRegistryAsset>(RegistryPath);
        if (registry == null)
        {
            registry = ScriptableObject.CreateInstance<ItemDataRegistryAsset>();
            AssetDatabase.CreateAsset(registry, RegistryPath);
        }

        List<ItemData> items = AssetDatabase.FindAssets("t:ItemData")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<ItemData>)
            .Where(item => item != null)
            .OrderBy(item => item.itemID)
            .ThenBy(item => item.name)
            .ToList();

        registry.SetItems(items);
        EditorUtility.SetDirty(registry);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        LogDuplicateIds(items);
        Debug.Log($"[ItemDataRegistry] Rebuilt runtime registry with {items.Count} items at {RegistryPath}.");
    }

    private static void LogDuplicateIds(IEnumerable<ItemData> items)
    {
        foreach (IGrouping<string, ItemData> group in items
                     .Where(item => !string.IsNullOrEmpty(item.itemID))
                     .GroupBy(item => item.itemID)
                     .Where(group => group.Count() > 1))
        {
            string assets = string.Join(", ", group.Select(item => item.name));
            Debug.LogWarning($"[ItemDataRegistry] Duplicate itemID '{group.Key}' found in: {assets}");
        }
    }
}
