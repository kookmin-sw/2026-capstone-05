using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Systems.Loot
{
    public static class LootSaveManager
    {
        private const string HostSelectedSlotPrefKey = "HostSaveSlot";

        public static LootSaveData LoadLoot(string lootId, int width, int height)
        {
            string path = GetLootPath(lootId);
            if (!File.Exists(path))
            {
                return CreateEmpty(lootId, width, height);
            }

            string json = File.ReadAllText(path);
            LootSaveData data = JsonUtility.FromJson<LootSaveData>(json);
            if (data == null)
            {
                return CreateEmpty(lootId, width, height);
            }

            data.lootId = string.IsNullOrWhiteSpace(data.lootId) ? lootId : data.lootId;
            data.width = data.width > 0 ? data.width : width;
            data.height = data.height > 0 ? data.height : height;
            data.items ??= new List<LootItemData>();
            return data;
        }

        public static bool HasLootSave(string lootId)
        {
            return !string.IsNullOrWhiteSpace(lootId) && File.Exists(GetLootPath(lootId));
        }

        public static void SaveLoot(LootSaveData data)
        {
            if (data == null || string.IsNullOrWhiteSpace(data.lootId))
            {
                return;
            }

            string path = GetLootPath(data.lootId);
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

        public static LootSaveData CreateEmpty(string lootId, int width, int height)
        {
            return new LootSaveData
            {
                lootId = lootId,
                width = width,
                height = height,
                items = new List<LootItemData>()
            };
        }

        public static void ClearSaveForSlot(int slot)
        {
            int safeSlot = Mathf.Clamp(slot, 1, 3);
            string dirPath = Path.Combine(Application.persistentDataPath, "LootSystem", $"slot_{safeSlot}");
            if (Directory.Exists(dirPath))
            {
                try
                {
                    Directory.Delete(dirPath, true);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[LootSystem] Failed to clear save for slot {safeSlot}: {e.Message}");
                }
            }
        }

        private static string GetLootPath(string lootId)
        {
            int slot = Mathf.Clamp(PlayerPrefs.GetInt(HostSelectedSlotPrefKey, 1), 1, 3);
            string safeId = MakeSafeFileName(lootId);
            return Path.Combine(Application.persistentDataPath, "LootSystem", $"slot_{slot}", $"{safeId}.json");
        }

        private static string MakeSafeFileName(string value)
        {
            string result = string.IsNullOrWhiteSpace(value) ? "loot" : value;
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                result = result.Replace(invalid, '_');
            }

            return result;
        }
    }
}
