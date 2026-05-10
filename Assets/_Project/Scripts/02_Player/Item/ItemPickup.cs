using FMODUnity;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ItemPickup : MonoBehaviour, IInteractable
{
    private static readonly Dictionary<int, ItemPickup> PickupsByKey = new Dictionary<int, ItemPickup>();

    public ItemInstance itemInstance;

    private int _pickupKey;
    private bool _isPickedUp;

    public int PickupKey => _pickupKey;
    public bool IsPickedUp => _isPickedUp;

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

        if (QuickslotUIController.Instance != null && !QuickslotUIController.Instance.HasEmptySlot())
        {
            Debug.LogWarning("No empty quickslot is available.");
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

        if (QuickslotUIController.Instance == null)
        {
            Debug.LogError("QuickslotUIController.Instance is missing. Item cannot be added to quickslot.");
            return false;
        }

        bool added = QuickslotUIController.Instance.AddItemToEmptySlot(itemInstance);
        if (!added)
        {
            Debug.LogWarning("No empty quickslot is available.");
            return false;
        }

        MarkPickedUp();

        if (player != null && player.NoiseEmitter != null)
        {
            // Hook pickup noise here when the noise system is ready for item pickups.
            RuntimeManager.PlayOneShot("event:/SFX/Player/Grab", player.transform.position);
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
        return _isPickedUp ? string.Empty : "[E] Pick up";
    }

    public string GetObjectName()
    {
        return itemInstance?.Data?.itemName ?? "Unknown Item";
    }

    private void MarkPickedUp()
    {
        _isPickedUp = true;
        gameObject.SetActive(false);
    }

    private void GrantApprovedLocalPickup(PlayerController player, string itemId, int stackCount)
    {
        if (QuickslotUIController.Instance == null)
        {
            Debug.LogError("QuickslotUIController.Instance is missing. Approved item cannot be added to quickslot.");
            MarkPickedUp();
            return;
        }

        ItemData itemData = itemInstance?.Data != null ? itemInstance.Data : ItemDataRegistry.Find(itemId);
        if (itemData == null)
        {
            Debug.LogWarning($"Approved pickup item data was not found. itemId={itemId}");
            MarkPickedUp();
            return;
        }

        ItemDataRegistry.Register(itemData);
        ItemInstance grantedItem = new ItemInstance(itemData, Mathf.Max(1, stackCount));
        bool added = QuickslotUIController.Instance.AddItemToEmptySlot(grantedItem);
        if (!added)
        {
            Debug.LogWarning("No empty quickslot is available for approved pickup.");
        }

        MarkPickedUp();

        if (player != null && player.NoiseEmitter != null)
        {
            // Hook pickup noise here when the noise system is ready for item pickups.
        }
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
