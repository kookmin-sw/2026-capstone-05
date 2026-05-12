using UnityEngine;

namespace Systems.StorageSystem
{
    public static class StorageRackRuntimeBinder
    {
        private static readonly string[] RackPaths =
        {
            "Base/bunker_scene/Rack",
            "Base/bunker_scene/Rack.001"
        };

        public static void BindDefaultRacks(StorageNetworkSync networkSync)
        {
            if (networkSync == null)
            {
                return;
            }

            StorageUI storageUI = StorageUI.ActiveInstance;
            if (storageUI == null)
            {
                StorageUI[] storageUis = Object.FindObjectsByType<StorageUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                storageUI = storageUis.Length > 0 ? storageUis[0] : null;
            }

            for (int i = 0; i < RackPaths.Length; i++)
            {
                Transform rack = GameObject.Find(RackPaths[i])?.transform;
                if (rack == null)
                {
                    rack = FindByName(i == 0 ? "Rack" : "Rack.001");
                }

                if (rack == null)
                {
                    Debug.LogWarning($"[StorageSystem] Could not find storage rack path '{RackPaths[i]}'.");
                    continue;
                }

                StorageBox storageBox = rack.GetComponent<StorageBox>();
                if (storageBox == null)
                {
                    storageBox = rack.gameObject.AddComponent<StorageBox>();
                }

                storageBox.Configure($"Storage_{i}", storageUI, networkSync);
            }
        }

        private static Transform FindByName(string objectName)
        {
            Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Transform transform in transforms)
            {
                if (transform != null && transform.name == objectName && IsUnderBunkerScene(transform))
                {
                    return transform;
                }
            }

            return null;
        }

        private static bool IsUnderBunkerScene(Transform transform)
        {
            Transform current = transform.parent;
            while (current != null)
            {
                if (current.name == "bunker_scene")
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }
    }
}
