#if UNITY_EDITOR
using Fusion;
using Systems.StorageSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

public static class StorageSystemSceneSetup
{
    private const string GameScenePath = "Assets/_Project/Scenes/00_General/SingleDemoScene_StaticMap.unity";
    private const string GridInventoryUssPath = "Assets/_Project/Scripts/01_PM/GridInventory/Inventory/UI/GridInventory.uss";

    [MenuItem("Tools/Storage System/Setup Game Scene")]
    public static void SetupGameScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        EditorSceneManager.OpenScene(GameScenePath);

        StorageNetworkSync networkSync = EnsureStorageNetworkSync();
        StorageUI storageUI = EnsureStorageUI();
        LinkStorageBoxes(storageUI, networkSync);
        ConfigureRackStorageBoxes(storageUI, networkSync);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[StorageSystemSceneSetup] Storage system objects configured in game scene.");
    }

    private static StorageNetworkSync EnsureStorageNetworkSync()
    {
        StorageNetworkSync sync = FindFirst<StorageNetworkSync>();
        if (sync != null)
        {
            return sync;
        }

        GameObject go = new GameObject("StorageNetworkSync");
        Undo.RegisterCreatedObjectUndo(go, "Create StorageNetworkSync");
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        NetworkObject networkObject = go.AddComponent<NetworkObject>();
        StorageNetworkSync storageNetworkSync = go.AddComponent<StorageNetworkSync>();

        SerializedObject serializedSync = new SerializedObject(storageNetworkSync);
        serializedSync.FindProperty("storageWidth").intValue = 9;
        serializedSync.FindProperty("storageHeight").intValue = 18;
        SerializedProperty ids = serializedSync.FindProperty("defaultStorageIds");
        ids.arraySize = 2;
        ids.GetArrayElementAtIndex(0).stringValue = "Storage_0";
        ids.GetArrayElementAtIndex(1).stringValue = "Storage_1";
        serializedSync.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(networkObject);
        EditorUtility.SetDirty(storageNetworkSync);
        return storageNetworkSync;
    }

    private static StorageUI EnsureStorageUI()
    {
        StorageUI storageUI = FindFirst<StorageUI>();
        if (storageUI != null)
        {
            return storageUI;
        }

        GameObject go = new GameObject("StorageUI");
        Undo.RegisterCreatedObjectUndo(go, "Create StorageUI");
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        UIDocument uiDocument = go.AddComponent<UIDocument>();
        StorageUI ui = go.AddComponent<StorageUI>();

        UIDocument sourceDocument = FindSourceUIDocument();
        if (sourceDocument != null)
        {
            uiDocument.panelSettings = sourceDocument.panelSettings;
        }

        StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(GridInventoryUssPath);
        SerializedObject serializedUi = new SerializedObject(ui);
        serializedUi.FindProperty("document").objectReferenceValue = uiDocument;
        serializedUi.FindProperty("styleSheet").objectReferenceValue = styleSheet;
        serializedUi.FindProperty("panelName").stringValue = "Storage";
        serializedUi.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(uiDocument);
        EditorUtility.SetDirty(ui);
        return ui;
    }

    private static UIDocument FindSourceUIDocument()
    {
        UIDocument[] documents = Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (UIDocument document in documents)
        {
            if (document != null && document.gameObject.name == "GridInventoryUI")
            {
                return document;
            }
        }

        foreach (UIDocument document in documents)
        {
            if (document != null && document.panelSettings != null)
            {
                return document;
            }
        }

        return null;
    }

    private static T FindFirst<T>() where T : Object
    {
        T[] objects = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        return objects.Length > 0 ? objects[0] : null;
    }

    private static void LinkStorageBoxes(StorageUI storageUI, StorageNetworkSync networkSync)
    {
        StorageBox[] boxes = Object.FindObjectsByType<StorageBox>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < boxes.Length; i++)
        {
            SerializedObject serializedBox = new SerializedObject(boxes[i]);
            serializedBox.FindProperty("storageUI").objectReferenceValue = storageUI;
            serializedBox.FindProperty("storageNetworkSync").objectReferenceValue = networkSync;

            SerializedProperty storageId = serializedBox.FindProperty("storageId");
            if (string.IsNullOrWhiteSpace(storageId.stringValue))
            {
                storageId.stringValue = $"Storage_{i}";
            }

            serializedBox.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(boxes[i]);
        }
    }

    private static void ConfigureRackStorageBoxes(StorageUI storageUI, StorageNetworkSync networkSync)
    {
        ConfigureRack("Rack", "Storage_0", storageUI, networkSync);
        ConfigureRack("Rack.001", "Storage_1", storageUI, networkSync);
    }

    private static void ConfigureRack(string rackName, string storageId, StorageUI storageUI, StorageNetworkSync networkSync)
    {
        Transform rack = FindRack(rackName);
        if (rack == null)
        {
            Debug.LogWarning($"[StorageSystemSceneSetup] Rack not found: {rackName}");
            return;
        }

        StorageBox storageBox = rack.GetComponent<StorageBox>();
        if (storageBox == null)
        {
            storageBox = Undo.AddComponent<StorageBox>(rack.gameObject);
        }

        storageBox.Configure(storageId, storageUI, networkSync);
        EditorUtility.SetDirty(storageBox);
    }

    private static Transform FindRack(string rackName)
    {
        Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Transform transform in transforms)
        {
            if (transform == null || transform.name != rackName)
            {
                continue;
            }

            Transform current = transform.parent;
            while (current != null)
            {
                if (current.name == "bunker_scene")
                {
                    return transform;
                }

                current = current.parent;
            }
        }

        return null;
    }
}
#endif
