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
            return Path.Combine(Application.dataPath, "_Project", "Json", "01_PM", "GridInventory", "player_inventory_items.json");
        }

        public static void SaveInventory(GridInventoryModel model)
        {
            InventorySaveData saveData = new InventorySaveData();

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

            string json = JsonUtility.ToJson(saveData, true);
            string savePath = GetSavePath();
            
            // Create directory if it doesn't exist
            Directory.CreateDirectory(Path.GetDirectoryName(savePath));
            File.WriteAllText(savePath, json);
            Debug.Log($"[Inventory] Saved to: {savePath}");
        }

        public static void LoadInventory(GridInventoryModel model)
        {
            string savePath = GetSavePath();
            if (!File.Exists(savePath))
            {
                Debug.LogWarning("[Inventory] Save file not found.");
                return;
            }

            string json = File.ReadAllText(savePath);
            InventorySaveData saveData = JsonUtility.FromJson<InventorySaveData>(json);

            if (saveData == null || saveData.items == null) return;

            model.Clear(); // 전체 아이템 비우기 (OnModelChanged Invoke는 추후 처리)

            // 퀵슬롯 비우기
            if (QuickslotUIController.Instance != null)
            {
                for (int i = 0; i < 4; i++)
                {
                    QuickslotUIController.Instance.RemoveItemFromSlot(i);
                }
            }

            // 아이템 데이터를 로드하기 위한 Resource/ScriptableObject 매퍼 필요 (간단히 Resources 검색 또는 Resources.Load. 구조에 따라 변경)
            // 임시로 모든 ItemData를 찾아 매핑
            ItemData[] allItems = Resources.LoadAll<ItemData>(""); // Resource 폴더에 있을 경우, Addressable/다른 방식일 경우 수정 필요

            foreach (var itemDataSave in saveData.items)
            {
                // TODO: ItemData 로드 로직은 실제 프로젝트 설계(AssetDatabase/Resources/Addressables)에 따라 최적화 요망
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
        }

        // Runtime 환경에서 ItemData를 찾기 위한 임시 헬퍼. 실제 프로젝트 상황에 맞게 커스텀.
        private static ItemData FindItemDataByID(string id)
        {
#if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:ItemData");
            foreach (var guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                ItemData data = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemData>(path);
                if (data != null && data.itemID == id) return data;
            }
#else
            ItemData[] allItems = Resources.LoadAll<ItemData>("");
            foreach (var data in allItems)
            {
                if (data.itemID == id) return data;
            }
#endif
            return null;
        }
    }
}