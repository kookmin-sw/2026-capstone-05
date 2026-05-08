using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Fusion;
using Systems.GridInventory;
using UnityEngine;

[Serializable]
public class BackendInventorySaveFileData
{
    public int saveSlotIndex;
    public string savedAt;
    public List<BackendPlayerInventorySaveData> players = new List<BackendPlayerInventorySaveData>();
}

[Serializable]
public class BackendPlayerInventorySaveData
{
    public int playerRefRaw;
    public string playerId;
    public int saveSlotIndex;
    public List<BackendInventoryItemSaveData> items = new List<BackendInventoryItemSaveData>();
}

[Serializable]
public class BackendInventoryItemSaveData
{
    public int slotIndex;
    public int gridX;
    public int gridY;
    public bool isQuickslot;
    public int quickslotIndex;
    public string itemId;
    public int amount;
    public int rotation;
    public int width;
    public int height;
}

public sealed class BackendInventorySaveManager : MonoBehaviour
{
    private const int QuickslotCount = 4;
    private static readonly Dictionary<string, ItemData> ItemDataCache = new Dictionary<string, ItemData>();

    public void SaveAllPlayerInventories(int saveSlotIndex)
    {
        NetworkRunner runner = FindFirstObjectByType<NetworkRunner>();
        if (runner == null || !runner.IsServer)
        {
            return;
        }

        BackendInventorySaveFileData fileData = CollectAllPlayerData(runner, saveSlotIndex);
        WriteFile(saveSlotIndex, fileData);
    }

    public void LoadAllPlayerInventories(int saveSlotIndex)
    {
        NetworkRunner runner = FindFirstObjectByType<NetworkRunner>();
        if (runner == null || !runner.IsServer)
        {
            return;
        }

        if (!TryReadFile(saveSlotIndex, out BackendInventorySaveFileData fileData) || fileData == null)
        {
            return;
        }

        foreach (PlayerRef playerRef in runner.ActivePlayers)
        {
            if (!runner.TryGetPlayerObject(playerRef, out NetworkObject playerObject) || playerObject == null)
            {
                continue;
            }

            GridInventory inventory = playerObject.GetComponentInChildren<GridInventory>(true);
            if (inventory == null || inventory.Model == null)
            {
                continue;
            }

            LoadInventoryForPlayer(playerRef, saveSlotIndex, inventory.Model, fileData);
        }
    }

    public void LoadInventoryForPlayer(PlayerRef playerRef, int saveSlotIndex, GridInventoryModel model)
    {
        if (!TryReadFile(saveSlotIndex, out BackendInventorySaveFileData fileData) || fileData == null)
        {
            return;
        }

        LoadInventoryForPlayer(playerRef, saveSlotIndex, model, fileData);
    }

    public IEnumerator SaveAllPlayerInventoriesRoutine(int saveSlotIndex)
    {
        SaveAllPlayerInventories(saveSlotIndex);
        yield return null;
    }

    public IEnumerator LoadAllPlayerInventoriesRoutine(int saveSlotIndex)
    {
        LoadAllPlayerInventories(saveSlotIndex);
        yield return null;
    }

    private static BackendInventorySaveFileData CollectAllPlayerData(NetworkRunner runner, int saveSlotIndex)
    {
        BackendInventorySaveFileData fileData = new BackendInventorySaveFileData
        {
            saveSlotIndex = saveSlotIndex,
            savedAt = DateTime.UtcNow.ToString("o")
        };

        foreach (PlayerRef playerRef in runner.ActivePlayers)
        {
            if (!runner.TryGetPlayerObject(playerRef, out NetworkObject playerObject) || playerObject == null)
            {
                continue;
            }

            GridInventory gridInventory = playerObject.GetComponentInChildren<GridInventory>(true);
            if (gridInventory == null || gridInventory.Model == null)
            {
                continue;
            }

            fileData.players.Add(BuildPlayerData(gridInventory.Model, playerRef, saveSlotIndex));
        }

        return fileData;
    }

    private static void LoadInventoryForPlayer(PlayerRef playerRef, int saveSlotIndex, GridInventoryModel model, BackendInventorySaveFileData fileData)
    {
        if (model == null || fileData.players == null)
        {
            return;
        }

        BackendPlayerInventorySaveData playerData = fileData.players.Find(p => p.playerRefRaw == playerRef.RawEncoded && p.saveSlotIndex == saveSlotIndex);
        if (playerData == null)
        {
            return;
        }

        model.Clear();
        ClearQuickslots();

        foreach (BackendInventoryItemSaveData itemData in playerData.items)
        {
            ItemData item = ResolveItemData(itemData.itemId);
            if (item == null)
            {
                Debug.LogWarning($"[BackendInventorySaveManager] Unknown item id. itemId={itemData.itemId}");
                continue;
            }

            ItemInstance instance = new ItemInstance(item, itemData.amount);
            instance.currentRotation = (ItemRotation)itemData.rotation;

            if (itemData.isQuickslot)
            {
                if (QuickslotUIController.Instance != null && itemData.quickslotIndex >= 0 && itemData.quickslotIndex < QuickslotCount)
                {
                    QuickslotUIController.Instance.SetItemInSlot(itemData.quickslotIndex, instance);
                }
                continue;
            }

            if (!model.PlaceItem(instance, itemData.gridX, itemData.gridY))
            {
                Debug.LogWarning($"[BackendInventorySaveManager] Failed to place item. itemId={itemData.itemId}, x={itemData.gridX}, y={itemData.gridY}");
            }
        }
    }

    private static BackendPlayerInventorySaveData BuildPlayerData(GridInventoryModel model, PlayerRef playerRef, int saveSlotIndex)
    {
        BackendPlayerInventorySaveData playerData = new BackendPlayerInventorySaveData
        {
            playerRefRaw = playerRef.RawEncoded,
            playerId = playerRef.PlayerId.ToString(),
            saveSlotIndex = saveSlotIndex
        };

        HashSet<ItemInstance> processedItems = new HashSet<ItemInstance>();
        for (int i = 0; i < model.Items.Length; i++)
        {
            ItemInstance item = model.Items[i];
            if (item == null || item.Data == null || processedItems.Contains(item))
            {
                continue;
            }

            processedItems.Add(item);
            var pos = model.GetItemAnchorPosition(item);
            var shape = item.Data.gridShape.GetRotatedPositions(item.currentRotation);
            GetItemSize(shape, out int width, out int height);

            playerData.items.Add(new BackendInventoryItemSaveData
            {
                slotIndex = i,
                gridX = pos.x,
                gridY = pos.y,
                isQuickslot = false,
                quickslotIndex = -1,
                itemId = item.Data.itemID,
                amount = item.currentStackCount,
                rotation = (int)item.currentRotation,
                width = width,
                height = height
            });
        }

        if (QuickslotUIController.Instance != null)
        {
            for (int i = 0; i < QuickslotCount; i++)
            {
                ItemInstance quickslotItem = QuickslotUIController.Instance.GetItem(i);
                if (quickslotItem == null || quickslotItem.Data == null)
                {
                    continue;
                }

                playerData.items.Add(new BackendInventoryItemSaveData
                {
                    slotIndex = -1,
                    gridX = 0,
                    gridY = 0,
                    isQuickslot = true,
                    quickslotIndex = i,
                    itemId = quickslotItem.Data.itemID,
                    amount = quickslotItem.currentStackCount,
                    rotation = (int)quickslotItem.currentRotation,
                    width = 0,
                    height = 0
                });
            }
        }

        return playerData;
    }

    private static void GetItemSize(IReadOnlyList<Vector2Int> shape, out int width, out int height)
    {
        if (shape == null || shape.Count == 0)
        {
            width = 0;
            height = 0;
            return;
        }

        int minX = int.MaxValue;
        int maxX = int.MinValue;
        int minY = int.MaxValue;
        int maxY = int.MinValue;
        for (int i = 0; i < shape.Count; i++)
        {
            var p = shape[i];
            minX = Mathf.Min(minX, p.x);
            maxX = Mathf.Max(maxX, p.x);
            minY = Mathf.Min(minY, p.y);
            maxY = Mathf.Max(maxY, p.y);
        }

        width = maxX - minX + 1;
        height = maxY - minY + 1;
    }

    private static void ClearQuickslots()
    {
        if (QuickslotUIController.Instance == null)
        {
            return;
        }

        for (int i = 0; i < QuickslotCount; i++)
        {
            QuickslotUIController.Instance.RemoveItemFromSlot(i);
        }
    }

    private static ItemData ResolveItemData(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
        {
            return null;
        }

        if (ItemDataCache.TryGetValue(itemId, out ItemData cached))
        {
            return cached;
        }

        foreach (ItemData data in Resources.LoadAll<ItemData>(string.Empty))
        {
            if (data != null && !string.IsNullOrEmpty(data.itemID) && !ItemDataCache.ContainsKey(data.itemID))
            {
                ItemDataCache[data.itemID] = data;
            }
        }

        ItemDataCache.TryGetValue(itemId, out ItemData found);
        return found;
    }

    private static bool TryReadFile(int saveSlotIndex, out BackendInventorySaveFileData fileData)
    {
        fileData = null;

        string path = GetSavePath(saveSlotIndex);
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            fileData = JsonUtility.FromJson<BackendInventorySaveFileData>(File.ReadAllText(path));
            return fileData != null;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[BackendInventorySaveManager] Save file parse failed. path={path}, message={ex.Message}");
            return false;
        }
    }

    private static void WriteFile(int saveSlotIndex, BackendInventorySaveFileData fileData)
    {
        string path = GetSavePath(saveSlotIndex);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? Application.persistentDataPath);
        File.WriteAllText(path, JsonUtility.ToJson(fileData, true));
        Debug.Log($"[BackendInventorySaveManager] Saved. slot={saveSlotIndex}, players={fileData.players.Count}, path={path}");
    }

    private static string GetSavePath(int slot)
    {
        return Path.Combine(Application.persistentDataPath, $"inventory_save_slot_{Mathf.Max(0, slot)}.json");
    }
}
