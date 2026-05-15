using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using Systems.GridInventory;
using System.Linq;

namespace Systems.GridInventory {
    [Serializable]
    public class InventorySaveData
    {
        public int gold = -1;
        public List<InventoryItemSaveData> items = new List<InventoryItemSaveData>();
    }

    [Serializable]
    public class InventoryItemSaveData
    {
        public long id;               
        public string user_id;        
        public string item_id;        
        public int quantity;          
        public string acquired_at;    
        public string updated_at;     
        public int slot_x;            
        public int slot_y;            
        public int rotated;           
        
        // 퀵슬롯용 데이터
        public bool is_quickslot;
        public int quickslot_index;
    }

    public class GridInventorySaveSystem
    {
        private static string GetSavePath()
        {
            return Path.Combine(Application.persistentDataPath, "GridInventory", "player_inventory_items.json");
        }

        private static string GetLegacySavePath()
        {
            return Path.Combine(Application.dataPath, "_Project", "Json", "01_PM", "GridInventory", "player_inventory_items.json");
        }

        private static string GetReadableSavePath()
        {
            string savePath = GetSavePath();
            if (File.Exists(savePath))
                return savePath;

            string legacySavePath = GetLegacySavePath();
            return File.Exists(legacySavePath) ? legacySavePath : savePath;
        }

        public static void SaveInventory(GridInventoryModel model)
        {
            InventorySaveData saveData = GetSaveData(model);
            string json = JsonUtility.ToJson(saveData, true);
            string savePath = GetSavePath();
            
            Directory.CreateDirectory(Path.GetDirectoryName(savePath));
            File.WriteAllText(savePath, json);
            Debug.Log($"[Inventory] Saved to: {savePath}");
        }

        public static InventorySaveData GetSaveData(GridInventoryModel model)
        {
            InventorySaveData saveData = new InventorySaveData();
            
            bool shouldSaveGold = false;
            if (PlayerNetworkSetup.IsOfflineTestMode)
            {
                shouldSaveGold = true;
            }
            else if (BackendPlayerNetworkSync.LocalInstance != null && BackendPlayerNetworkSync.LocalInstance.HasStateAuthority)
            {
                shouldSaveGold = true;
            }

            if (shouldSaveGold)
            {
                saveData.gold = model.Gold;
            }
            else
            {
                saveData.gold = -1;
            }

            var processedItems = new HashSet<ItemInstance>();
            for (int i = 0; i < model.Items.Length; i++)
            {
                ItemInstance item = model.Items[i];
                if (item != null && item.Data != null && !processedItems.Contains(item))
                {
                    processedItems.Add(item);
                    var pos = model.GetItemAnchorPosition(item);

                    InventoryItemSaveData itemSaveData = new InventoryItemSaveData
                    {
                        id = DateTime.UtcNow.Ticks, // Temporary ID
                        user_id = "player1", // Temporary User ID
                        item_id = item.Data.itemID,
                        quantity = item.currentStackCount,
                        acquired_at = DateTime.UtcNow.ToString("o"),
                        updated_at = DateTime.UtcNow.ToString("o"),
                        slot_x = pos.x,
                        slot_y = pos.y,
                        rotated = (int)item.currentRotation,
                        is_quickslot = false
                    };

                    saveData.items.Add(itemSaveData);
                }
            }

            // Quickslot items 저장 추가
            if (QuickslotUIController.Instance != null)
            {
                for (int i = 0; i < 4; i++) // MaxSlots (Quickslots 0~3)
                {
                    var item = QuickslotUIController.Instance.GetItem(i);
                    if (item != null && item.Data != null)
                    {
                        InventoryItemSaveData quickItemData = new InventoryItemSaveData
                        {
                            id = DateTime.UtcNow.Ticks,
                            user_id = "player1",
                            item_id = item.Data.itemID,
                            quantity = item.currentStackCount,
                            acquired_at = DateTime.UtcNow.ToString("o"),
                            updated_at = DateTime.UtcNow.ToString("o"),
                            slot_x = 0,
                            slot_y = 0,
                            rotated = (int)item.currentRotation,
                            is_quickslot = true,
                            quickslot_index = i
                        };
                        saveData.items.Add(quickItemData);
                    }
                }
            }
            return saveData;
        }

        public static void SaveInventoryDataToDisk(string playerId, string json)
        {
            string savePath = Path.Combine(Application.persistentDataPath, "GridInventory", $"player_inventory_{playerId}.json");
            Directory.CreateDirectory(Path.GetDirectoryName(savePath));
            File.WriteAllText(savePath, json);
            Debug.Log($"[Inventory] Saved player {playerId} to: {savePath}");
        }

        public static string LoadInventoryDataFromDisk(string playerId)
        {
            string savePath = Path.Combine(Application.persistentDataPath, "GridInventory", $"player_inventory_{playerId}.json");
            if (File.Exists(savePath))
            {
                return File.ReadAllText(savePath);
            }
            return null;
        }

        public static void LoadInventory(GridInventoryModel model)
        {
            string savePath = GetReadableSavePath();
            if (!File.Exists(savePath))
            {
                Debug.LogWarning("[Inventory] Save file not found.");
                return;
            }

            string json = File.ReadAllText(savePath);
            InventorySaveData saveData = JsonUtility.FromJson<InventorySaveData>(json);

            ApplySaveData(model, saveData);
        }

        public static void ApplySaveData(GridInventoryModel model, InventorySaveData saveData)
        {
            if (saveData == null || saveData.items == null) return;

            model.Clear();
            
            bool shouldLoadGold = false;
            if (PlayerNetworkSetup.IsOfflineTestMode)
            {
                shouldLoadGold = true;
            }
            else if (BackendPlayerNetworkSync.LocalInstance != null && BackendPlayerNetworkSync.LocalInstance.HasStateAuthority)
            {
                shouldLoadGold = true;
            }

            if (shouldLoadGold)
            {
                if (saveData.gold < 0)
                {
                    model.SetGold(500);
                }
                else
                {
                    model.SetGold(saveData.gold);
                }

                if (BackendRoundManager.Instance != null && BackendRoundManager.Instance.HasStateAuthority)
                {
                    BackendRoundManager.Instance.SharedGold = model.Gold;
                }
            }

            // 퀵슬롯 비우기
            if (QuickslotUIController.Instance != null)
            {
                for (int i = 0; i < 4; i++)
                {
                    QuickslotUIController.Instance.RemoveItemFromSlot(i);
                }
            }

            // 아이템 데이터를 로드
            foreach (var itemDataSave in saveData.items)
            {
                ItemData matchedData = FindItemDataByID(itemDataSave.item_id);
                if (matchedData != null)
                {
                    ItemInstance newItem = new ItemInstance(matchedData, itemDataSave.quantity);
                    newItem.currentRotation = (ItemRotation)itemDataSave.rotated;
                    
                    if (itemDataSave.is_quickslot)
                    {
                        if (QuickslotUIController.Instance != null)
                        {
                            QuickslotUIController.Instance.SetItemInSlot(itemDataSave.quickslot_index, newItem);
                        }
                    }
                    else
                    {
                        // 위치 배치
                        if (!model.PlaceItem(newItem, itemDataSave.slot_x, itemDataSave.slot_y))
                        {
                            Debug.LogWarning($"[Inventory] Failed to place loaded item {matchedData.itemID} at {itemDataSave.slot_x},{itemDataSave.slot_y}");
                        }
                    }
                }
                else
                {
                    Debug.LogWarning($"[Inventory] ItemData with ID {itemDataSave.item_id} not found.");
                }
            }

            Debug.Log("[Inventory] Loaded successfully.");
            model.Items.Invoke();
        }

        // Runtime 환경에서 ItemData를 찾기 위한 최적화된 헬퍼
        private static Dictionary<string, ItemData> itemDataCache;

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
                foreach (var guid in guids)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                    ItemData data = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemData>(path);
                    if (data != null)
                    {
                        if (!itemDataCache.ContainsKey(data.itemID))
                        {
                            itemDataCache.Add(data.itemID, data);
                        }
                        else
                        {
                            Debug.LogWarning($"[Inventory] Duplicate itemID found: {data.itemID}. Asset: {data.name}");
                        }
                    }
                }
#else
                ItemData[] allItems = Resources.LoadAll<ItemData>("");
                foreach (var data in allItems)
                {
                    if (data != null)
                    {
                        if (!itemDataCache.ContainsKey(data.itemID))
                        {
                            itemDataCache.Add(data.itemID, data);
                        }
                        else
                        {
                            Debug.LogWarning($"[Inventory] Duplicate itemID found: {data.itemID}. Asset: {data.name}");
                        }
                    }
                }
#endif
            }

            if (itemDataCache.TryGetValue(id, out ItemData result))
            {
                return result;
            }
            return null;
        }
    }
}
