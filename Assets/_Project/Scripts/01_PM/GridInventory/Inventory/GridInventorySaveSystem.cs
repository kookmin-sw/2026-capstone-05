using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Systems.GridInventory {
    [Serializable]
    public class InventorySaveData
    {
        public int version = 2;
        public string host_user_id = string.Empty;
        public int host_slot = 1;
        public string saved_at_utc = string.Empty;
        public List<string> saved_user_ids = new List<string>();
        public List<InventoryItemSaveData> items = new List<InventoryItemSaveData>();
    }

    [Serializable]
    public class InventoryItemSaveData
    {
        public long id;
        public string host_user_id;
        public int host_slot;
        public string user_id;
        public string player_ref;
        public bool is_host_player;
        public string item_id;
        public int quantity;
        public string acquired_at;
        public string updated_at;
        public int slot_x;
        public int slot_y;
        public int rotated;
        public bool is_quickslot;
        public int quickslot_index;
    }

    public class GridInventorySaveSystem
    {
        private const string HostSelectedSlotPrefKey = "HostSaveSlot";
        private const string DefaultUserId = "anonymous-user";
        private const string DefaultHostId = "anonymous-host";
        private const int QuickslotCount = 4;

        private static Dictionary<string, ItemData> itemDataCache;
        private static InventorySaveData pendingHostRoundSave;
        private static int pendingHostSlot = -1;

        private static string CurrentHostId => NormalizeIdentity(AuthSession.IsLoggedIn ? AuthSession.CurrentUserId : DefaultHostId, DefaultHostId);
        private static string CurrentUserId => NormalizeIdentity(AuthSession.IsLoggedIn ? AuthSession.CurrentUserId : DefaultUserId, DefaultUserId);
        private static int ActiveHostSlot => Mathf.Clamp(PlayerPrefs.GetInt(HostSelectedSlotPrefKey, 1), 1, 3);

        private static string GetSavePath(int slot, string hostUserId)
        {
            string safeHostId = SanitizePathSegment(NormalizeIdentity(hostUserId, DefaultHostId));
            int safeSlot = Mathf.Clamp(slot, 1, 3);
            return Path.Combine(Application.persistentDataPath, "GridInventory", "HostSaves", safeHostId, $"slot{safeSlot}", "player_inventory_items.json");
        }

        private static string GetLegacySavePath()
        {
            return Path.Combine(Application.dataPath, "_Project", "Json", "01_PM", "GridInventory", "player_inventory_items.json");
        }

        public static void SaveInventory(GridInventoryModel model)
        {
            int slot = ActiveHostSlot;
            string userId = CurrentUserId;
            List<InventoryItemSaveData> items = CaptureInventoryItems(model, userId, slot);

            InventorySaveData saveData = LoadHostSaveData(slot, CurrentHostId);
            UpsertPlayerItems(saveData, userId, items, string.Empty, true);
            WriteHostSaveData(saveData);
        }

        public static void LoadInventory(GridInventoryModel model)
        {
            if (TryGetSavedItemsForUser(ActiveHostSlot, CurrentUserId, out List<InventoryItemSaveData> items))
            {
                ApplyItemsToInventory(model, items);
                Debug.Log($"[Inventory] Loaded user inventory. userId={CurrentUserId}, slot={ActiveHostSlot}, count={items.Count}");
                return;
            }

            string legacyPath = GetLegacySavePath();
            if (!File.Exists(legacyPath))
            {
                Debug.LogWarning("[Inventory] Save file not found.");
                return;
            }

            string json = File.ReadAllText(legacyPath);
            InventorySaveData legacyData = JsonUtility.FromJson<InventorySaveData>(json);
            ApplyItemsToInventory(model, legacyData?.items);
            Debug.Log($"[Inventory] Loaded legacy inventory from: {legacyPath}");
        }

        public static List<InventoryItemSaveData> CaptureLocalInventoryItems()
        {
            return CaptureInventoryItems(GridInventory.Instance?.Controller?.Model, CurrentUserId, ActiveHostSlot);
        }

        public static List<InventoryItemSaveData> CaptureInventoryItems(GridInventoryModel model, string userId, int hostSlot)
        {
            List<InventoryItemSaveData> items = new List<InventoryItemSaveData>();
            if (model == null)
                return items;

            string normalizedUserId = NormalizeIdentity(userId, DefaultUserId);
            string hostId = CurrentHostId;
            string timestamp = DateTime.UtcNow.ToString("o");
            long baseId = DateTime.UtcNow.Ticks;

            HashSet<ItemInstance> processedItems = new HashSet<ItemInstance>();
            for (int i = 0; i < model.Items.Length; i++)
            {
                ItemInstance item = model.Items[i];
                if (item == null || item.Data == null || processedItems.Contains(item))
                    continue;

                processedItems.Add(item);
                var pos = model.GetItemAnchorPosition(item);
                items.Add(CreateItemSaveData(baseId + items.Count, hostId, hostSlot, normalizedUserId, item, timestamp, pos.x, pos.y, false, -1));
            }

            if (QuickslotUIController.Instance != null)
            {
                for (int i = 0; i < QuickslotCount; i++)
                {
                    ItemInstance item = QuickslotUIController.Instance.GetItem(i);
                    if (item == null || item.Data == null)
                        continue;

                    items.Add(CreateItemSaveData(baseId + items.Count, hostId, hostSlot, normalizedUserId, item, timestamp, 0, 0, true, i));
                }
            }

            return items;
        }

        public static void BeginHostRoundSave(int slot)
        {
            int safeSlot = Mathf.Clamp(slot, 1, 3);
            pendingHostSlot = safeSlot;
            pendingHostRoundSave = LoadHostSaveData(safeSlot, CurrentHostId);
            pendingHostRoundSave.host_user_id = CurrentHostId;
            pendingHostRoundSave.host_slot = safeSlot;
            pendingHostRoundSave.saved_at_utc = DateTime.UtcNow.ToString("o");
        }

        public static void UpsertHostPlayerSnapshot(string userId, List<InventoryItemSaveData> items, string playerRef, bool isHostPlayer)
        {
            int slot = pendingHostSlot > 0 ? pendingHostSlot : ActiveHostSlot;
            if (pendingHostRoundSave == null || pendingHostSlot != slot)
            {
                BeginHostRoundSave(slot);
            }

            UpsertPlayerItems(pendingHostRoundSave, userId, items, playerRef, isHostPlayer);
        }

        public static void CommitHostRoundSave()
        {
            if (pendingHostRoundSave == null)
                return;

            pendingHostRoundSave.saved_at_utc = DateTime.UtcNow.ToString("o");
            WriteHostSaveData(pendingHostRoundSave);
            Debug.Log($"[Inventory] Host round inventory saved. host={pendingHostRoundSave.host_user_id}, slot={pendingHostRoundSave.host_slot}, items={pendingHostRoundSave.items.Count}");

            pendingHostRoundSave = null;
            pendingHostSlot = -1;
        }

        public static void ClearHostSlotSave(int slot)
        {
            string path = GetSavePath(slot, CurrentHostId);
            if (File.Exists(path))
            {
                File.Delete(path);
                Debug.Log($"[Inventory] Cleared host inventory save. path={path}");
            }
        }

        public static bool TryGetSavedItemsForUser(int slot, string userId, out List<InventoryItemSaveData> items)
        {
            InventorySaveData saveData = LoadHostSaveData(slot, CurrentHostId);
            string normalizedUserId = NormalizeIdentity(userId, DefaultUserId);
            items = new List<InventoryItemSaveData>();

            if (saveData?.items == null)
                return false;

            foreach (InventoryItemSaveData item in saveData.items)
            {
                if (string.Equals(NormalizeIdentity(item.user_id, DefaultUserId), normalizedUserId, StringComparison.OrdinalIgnoreCase))
                {
                    items.Add(CloneItem(item));
                }
            }

            return items.Count > 0 || HasSavedUserEntry(saveData, normalizedUserId);
        }

        public static void ApplyItemsToLocalInventory(List<InventoryItemSaveData> items)
        {
            ApplyItemsToInventory(GridInventory.Instance?.Controller?.Model, items);
        }

        private static void ApplyItemsToInventory(GridInventoryModel model, IEnumerable<InventoryItemSaveData> items)
        {
            if (model == null)
            {
                Debug.LogWarning("[Inventory] Inventory model is missing. Loaded items could not be applied.");
                return;
            }

            model.Clear();

            if (QuickslotUIController.Instance != null)
            {
                for (int i = 0; i < QuickslotCount; i++)
                {
                    QuickslotUIController.Instance.RemoveItemFromSlot(i);
                }
            }

            if (items == null)
                return;

            foreach (InventoryItemSaveData itemDataSave in items)
            {
                ItemData matchedData = FindItemDataByID(itemDataSave.item_id);
                if (matchedData == null)
                {
                    Debug.LogWarning($"[Inventory] ItemData with ID {itemDataSave.item_id} not found.");
                    continue;
                }

                ItemInstance newItem = new ItemInstance(matchedData, Mathf.Max(1, itemDataSave.quantity));
                newItem.currentRotation = (ItemRotation)Mathf.Clamp(itemDataSave.rotated, 0, 3);

                if (itemDataSave.is_quickslot)
                {
                    if (QuickslotUIController.Instance != null)
                    {
                        QuickslotUIController.Instance.SetItemInSlot(itemDataSave.quickslot_index, newItem);
                    }

                    continue;
                }

                if (!model.PlaceItem(newItem, itemDataSave.slot_x, itemDataSave.slot_y))
                {
                    Debug.LogWarning($"[Inventory] Failed to place loaded item {matchedData.itemID} at {itemDataSave.slot_x},{itemDataSave.slot_y}");
                }
            }
        }

        private static InventoryItemSaveData CreateItemSaveData(
            long id,
            string hostId,
            int hostSlot,
            string userId,
            ItemInstance item,
            string timestamp,
            int slotX,
            int slotY,
            bool isQuickslot,
            int quickslotIndex)
        {
            return new InventoryItemSaveData
            {
                id = id,
                host_user_id = hostId,
                host_slot = Mathf.Clamp(hostSlot, 1, 3),
                user_id = userId,
                item_id = item.Data.itemID,
                quantity = item.currentStackCount,
                acquired_at = timestamp,
                updated_at = timestamp,
                slot_x = slotX,
                slot_y = slotY,
                rotated = (int)item.currentRotation,
                is_quickslot = isQuickslot,
                quickslot_index = quickslotIndex
            };
        }

        private static void UpsertPlayerItems(InventorySaveData saveData, string userId, List<InventoryItemSaveData> items, string playerRef, bool isHostPlayer)
        {
            if (saveData.items == null)
                saveData.items = new List<InventoryItemSaveData>();
            if (saveData.saved_user_ids == null)
                saveData.saved_user_ids = new List<string>();

            string normalizedUserId = NormalizeIdentity(userId, DefaultUserId);
            saveData.items.RemoveAll(item => string.Equals(NormalizeIdentity(item.user_id, DefaultUserId), normalizedUserId, StringComparison.OrdinalIgnoreCase));
            if (!saveData.saved_user_ids.Exists(savedUserId => string.Equals(NormalizeIdentity(savedUserId, DefaultUserId), normalizedUserId, StringComparison.OrdinalIgnoreCase)))
            {
                saveData.saved_user_ids.Add(normalizedUserId);
            }

            if (items == null)
                return;

            for (int i = 0; i < items.Count; i++)
            {
                InventoryItemSaveData item = CloneItem(items[i]);
                item.host_user_id = saveData.host_user_id;
                item.host_slot = saveData.host_slot;
                item.user_id = normalizedUserId;
                item.player_ref = playerRef ?? string.Empty;
                item.is_host_player = isHostPlayer;
                item.updated_at = DateTime.UtcNow.ToString("o");
                saveData.items.Add(item);
            }
        }

        private static InventorySaveData LoadHostSaveData(int slot, string hostUserId)
        {
            string path = GetSavePath(slot, hostUserId);
            if (!File.Exists(path))
            {
                return new InventorySaveData
                {
                    host_user_id = NormalizeIdentity(hostUserId, DefaultHostId),
                    host_slot = Mathf.Clamp(slot, 1, 3),
                    saved_at_utc = DateTime.UtcNow.ToString("o")
                };
            }

            string json = File.ReadAllText(path);
            InventorySaveData saveData = JsonUtility.FromJson<InventorySaveData>(json) ?? new InventorySaveData();
            saveData.host_user_id = NormalizeIdentity(string.IsNullOrWhiteSpace(saveData.host_user_id) ? hostUserId : saveData.host_user_id, DefaultHostId);
            saveData.host_slot = saveData.host_slot <= 0 ? Mathf.Clamp(slot, 1, 3) : saveData.host_slot;
            if (saveData.saved_user_ids == null)
                saveData.saved_user_ids = new List<string>();
            if (saveData.items == null)
                saveData.items = new List<InventoryItemSaveData>();
            return saveData;
        }

        private static void WriteHostSaveData(InventorySaveData saveData)
        {
            if (saveData == null)
                return;

            saveData.version = 2;
            saveData.host_user_id = NormalizeIdentity(saveData.host_user_id, DefaultHostId);
            saveData.host_slot = Mathf.Clamp(saveData.host_slot, 1, 3);
            saveData.saved_at_utc = DateTime.UtcNow.ToString("o");

            string savePath = GetSavePath(saveData.host_slot, saveData.host_user_id);
            Directory.CreateDirectory(Path.GetDirectoryName(savePath));
            File.WriteAllText(savePath, JsonUtility.ToJson(saveData, true));
        }

        private static bool HasSavedUserEntry(InventorySaveData saveData, string userId)
        {
            if (saveData?.items == null)
                return false;

            if (saveData.saved_user_ids != null)
            {
                foreach (string savedUserId in saveData.saved_user_ids)
                {
                    if (string.Equals(NormalizeIdentity(savedUserId, DefaultUserId), userId, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            foreach (InventoryItemSaveData item in saveData.items)
            {
                if (string.Equals(NormalizeIdentity(item.user_id, DefaultUserId), userId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static InventoryItemSaveData CloneItem(InventoryItemSaveData item)
        {
            return new InventoryItemSaveData
            {
                id = item.id,
                host_user_id = item.host_user_id,
                host_slot = item.host_slot,
                user_id = item.user_id,
                player_ref = item.player_ref,
                is_host_player = item.is_host_player,
                item_id = item.item_id,
                quantity = item.quantity,
                acquired_at = item.acquired_at,
                updated_at = item.updated_at,
                slot_x = item.slot_x,
                slot_y = item.slot_y,
                rotated = item.rotated,
                is_quickslot = item.is_quickslot,
                quickslot_index = item.quickslot_index
            };
        }

        private static ItemData FindItemDataByID(string id)
        {
            ItemData registeredData = ItemDataRegistry.Find(id);
            if (registeredData != null)
            {
                return registeredData;
            }

            if (itemDataCache == null)
            {
                itemDataCache = new Dictionary<string, ItemData>();
#if UNITY_EDITOR
                string[] guids = UnityEditor.AssetDatabase.FindAssets("t:ItemData");
                foreach (string guid in guids)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                    ItemData data = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemData>(path);
                    CacheItemData(data);
                }
#else
                ItemData[] allItems = Resources.LoadAll<ItemData>("");
                foreach (ItemData data in allItems)
                {
                    CacheItemData(data);
                }
#endif
            }

            return itemDataCache.TryGetValue(id, out ItemData result) ? result : null;
        }

        private static void CacheItemData(ItemData data)
        {
            if (data == null || string.IsNullOrWhiteSpace(data.itemID))
                return;

            if (!itemDataCache.ContainsKey(data.itemID))
            {
                itemDataCache.Add(data.itemID, data);
            }
            else
            {
                Debug.LogWarning($"[Inventory] Duplicate itemID found: {data.itemID}. Asset: {data.name}");
            }
        }

        private static string NormalizeIdentity(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToLowerInvariant();
        }

        private static string SanitizePathSegment(string value)
        {
            string sanitized = value;
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                sanitized = sanitized.Replace(invalid, '_');
            }

            return sanitized;
        }
    }
}
