using FMODUnity;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;
using Systems.GridInventory;

public class ItemPickup : MonoBehaviour, IInteractable
{
    private static readonly Dictionary<int, ItemPickup> PickupsByKey = new Dictionary<int, ItemPickup>();

    public ItemInstance itemInstance;

    private int _pickupKey;
    private bool _isPickedUp;

    public int PickupKey => _pickupKey;
    public bool IsPickedUp => _isPickedUp;

    private readonly string interactPromptTable = "InteractPrompts";
    private readonly string interactPromptKey = "PickUp";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticRuntimeState()
    {
        ClearRuntimeState();
    }

    public static void ClearRuntimeState()
    {
        PickupsByKey.Clear();
    }

    private void Awake()
    {
        _pickupKey = BuildStablePickupKey();
        RegisterPickup();
        RegisterItemData();
    }

    private void OnEnable()
    {
        RegisterPickup();
        RegisterItemData();
    }

    private void OnDisable()
    {
        if (_isPickedUp)
            return;

        if (PickupsByKey.TryGetValue(_pickupKey, out ItemPickup pickup) && pickup == this)
        {
            PickupsByKey.Remove(_pickupKey);
        }
    }

    public bool CanInteract(PlayerController player)
    {
        return !_isPickedUp;
    }

    public void OnInteract(PlayerController player)
    {
        if (_isPickedUp)
            return;

        if (!CanStorePickupItem(itemInstance))
        {
            Debug.LogWarning("No empty quickslot or inventory space is available.");
            return;
        }

        BackendPlayerNetworkSync networkSync = player != null ? player.GetComponent<BackendPlayerNetworkSync>() : null;
        if (networkSync != null && networkSync.IsNetworkReady)
        {
            networkSync.RequestPickup(this);
            return;
        }

        TryGrantLocalPickup(player);
    }

    public bool TryGrantLocalPickup(PlayerController player)
    {
        if (_isPickedUp)
            return false;

        if (!TryStorePickupItem(itemInstance))
        {
            Debug.LogWarning("No empty quickslot or inventory space is available.");
            return false;
        }

        MarkPickedUp();

        if (player != null && player.NoiseEmitter != null)
        {
            // Hook pickup noise here when the noise system is ready for item pickups.
            RuntimeManager.PlayOneShot("event:/SFX/Player/Grab", player.transform.position);
            NoiseManager.Instance.GenerateNoise(player.transform.position, NoiseData.NoiseType.ItemPickup);
        }

        return true;
    }

    public void MarkPickedUpFromNetwork()
    {
        MarkPickedUp();
    }

    public static bool TryGetPickup(int pickupKey, out ItemPickup pickup)
    {
        return PickupsByKey.TryGetValue(pickupKey, out pickup) && pickup != null;
    }

    public static void ApplyNetworkPickup(
        int pickupKey,
        bool shouldGrantToLocalPlayer,
        PlayerController localPlayer,
        string itemId,
        int stackCount)
    {
        if (!TryGetPickup(pickupKey, out ItemPickup pickup))
            return;

        if (shouldGrantToLocalPlayer)
        {
            pickup.GrantApprovedLocalPickup(localPlayer, itemId, stackCount);
        }
        else
        {
            pickup.MarkPickedUpFromNetwork();
        }
    }

    public string GetInteractPrompt()
    {
        return _isPickedUp ? string.Empty : LocalizationSettings.StringDatabase.GetLocalizedString(interactPromptTable, interactPromptKey);
    }

    public string GetObjectName()
    {
        return itemInstance?.Data?.ItemNameString ?? "Unknown";
    }

    private void MarkPickedUp()
    {
        _isPickedUp = true;
        gameObject.SetActive(false);
    }

    private void GrantApprovedLocalPickup(PlayerController player, string itemId, int stackCount)
    {
        ItemData itemData = itemInstance?.Data != null ? itemInstance.Data : ItemDataRegistry.Find(itemId);
        if (itemData == null)
        {
            Debug.LogWarning($"Approved pickup item data was not found. itemId={itemId}");
            MarkPickedUp();
            return;
        }

        ItemDataRegistry.Register(itemData);
        ItemInstance grantedItem = new ItemInstance(itemData, Mathf.Max(1, stackCount));
        if (!TryStorePickupItem(grantedItem))
        {
            Debug.LogWarning("No empty quickslot or inventory space is available for approved pickup.");
        }

        MarkPickedUp();

        if (player != null && player.NoiseEmitter != null)
        {
            // Hook pickup noise here when the noise system is ready for item pickups.
        }
    }

    private static bool TryStorePickupItem(ItemInstance item)
    {
        if (item == null || item.Data == null)
        {
            return false;
        }

        if (QuickslotUIController.Instance != null && QuickslotUIController.Instance.AddItemToEmptySlot(item))
        {
            return true;
        }

        var inventoryModel = GridInventory.Instance?.Controller?.Model;
        return inventoryModel != null && inventoryModel.TryAdd(item);
    }

    private static bool CanStorePickupItem(ItemInstance item)
    {
        if (item == null || item.Data == null)
        {
            return false;
        }

        if (QuickslotUIController.Instance != null && QuickslotUIController.Instance.HasEmptySlot())
        {
            return true;
        }

        var inventoryModel = GridInventory.Instance?.Controller?.Model;
        if (inventoryModel == null)
        {
            return false;
        }

        for (int y = 0; y < inventoryModel.Height; y++)
        {
            for (int x = 0; x < inventoryModel.Width; x++)
            {
                if (inventoryModel.CanPlaceItem(item, x, y))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void RegisterPickup()
    {
        if (_pickupKey == 0)
            _pickupKey = BuildStablePickupKey();

        PickupsByKey[_pickupKey] = this;
    }

    private void RegisterItemData()
    {
        ItemDataRegistry.Register(itemInstance?.Data);
    }

    private int BuildStablePickupKey()
    {
        unchecked
        {
            int hash = 17;
            Scene scene = gameObject.scene;
            hash = hash * 31 + StableStringHash(scene.path);
            hash = hash * 31 + StableStringHash(BuildHierarchyPath(transform));
            hash = hash * 31 + Mathf.RoundToInt(transform.position.x * 100f);
            hash = hash * 31 + Mathf.RoundToInt(transform.position.y * 100f);
            hash = hash * 31 + Mathf.RoundToInt(transform.position.z * 100f);
            string itemId = itemInstance?.Data != null ? itemInstance.Data.itemID : string.Empty;
            hash = hash * 31 + StableStringHash(itemId);
            return hash == 0 ? 1 : hash;
        }
    }

    private static string BuildHierarchyPath(Transform target)
    {
        if (target == null)
            return string.Empty;

        string path = target.name;
        Transform parent = target.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }

        return path;
    }

    private static int StableStringHash(string value)
    {
        unchecked
        {
            int hash = (int)2166136261;
            if (!string.IsNullOrEmpty(value))
            {
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= 16777619;
                }
            }

            return hash;
        }
    }
}
