using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Systems.StorageSystem
{
    public static class StorageSaveManager
    {
        private const string HostSelectedSlotPrefKey = "HostSaveSlot";

        public static StorageSaveData LoadStorage(string storageId, int width, int height)
        {
            string path = GetStoragePath(storageId);
            if (!File.Exists(path))
            {
                return CreateEmpty(storageId, width, height);
            }

            string json = File.ReadAllText(path);
            StorageSaveData data = JsonUtility.FromJson<StorageSaveData>(json);
            if (data == null)
            {
                return CreateEmpty(storageId, width, height);
            }

            data.storageId = string.IsNullOrWhiteSpace(data.storageId) ? storageId : data.storageId;
            data.width = data.width > 0 ? data.width : width;
            data.height = data.height > 0 ? data.height : height;
            data.items ??= new List<StorageItemData>();
            return data;
        }

        public static void SaveStorage(StorageSaveData data)
        {
            if (data == null || string.IsNullOrWhiteSpace(data.storageId))
            {
                return;
            }

            string path = GetStoragePath(data.storageId);
            string directory = Path.GetDirectoryName(path);
            Directory.CreateDirectory(directory);

            string tempPath = path + ".tmp";
            string backupPath = path + ".bak";
            string json = JsonUtility.ToJson(data, true);

            File.WriteAllText(tempPath, json);
            if (File.Exists(path))
            {
                File.Copy(path, backupPath, true);
            }

            if (File.Exists(path))
            {
                File.Delete(path);
            }

            File.Move(tempPath, path);
        }

        public static StorageSaveData CreateEmpty(string storageId, int width, int height)
        {
            return new StorageSaveData
            {
                storageId = storageId,
                width = width,
                height = height,
                items = new List<StorageItemData>()
            };
        }

        private static string GetStoragePath(string storageId)
        {
            int slot = Mathf.Clamp(PlayerPrefs.GetInt(HostSelectedSlotPrefKey, 1), 1, 3);
            string safeId = MakeSafeFileName(storageId);
            return Path.Combine(Application.persistentDataPath, "StorageSystem", $"slot_{slot}", $"{safeId}.json");
        }

        private static string MakeSafeFileName(string value)
        {
            string result = string.IsNullOrWhiteSpace(value) ? "storage" : value;
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                result = result.Replace(invalid, '_');
            }

            return result;
        }
    }
}
