using System.Collections.Generic;
using UnityEngine;
using Systems.GridInventory;

namespace Systems.Loot
{
    [System.Serializable]
    public class LootItemSetup
    {
        public ItemData itemData;
        public int quantity = 1;
    }

    public class InteractableLoot : MonoBehaviour, IInteractable
    {
        [Header("Loot Configuration")]
        [SerializeField] private string storageId = "Loot_0";
        [SerializeField] private string lootTitle = "Supply Chest";
        
        [Header("Initial Items (Only spawned if storage is empty)")]
        [SerializeField] private List<LootItemSetup> initialItems;
        
        public string StorageId => storageId;
        
        private void Awake()
        {
        }
        
        private bool isInitialized = false;

        private void InitializeItemsIfNeeded()
        {
            if (isInitialized) return;
            isInitialized = true;
            
            if (StorageSystem.StorageNetworkSync.Instance == null) return;
            
            var model = StorageSystem.StorageNetworkSync.Instance.GetOrCreateModel(storageId);
            
            // Only seed items if the storage is completely empty (could check if first time initialization)
            // But since model could be loaded empty, let's check if there are no items.
            bool isEmpty = true;
            for (int i = 0; i < model.Items.Length; i++)
            {
                if (model.Items[i] != null)
                {
                    isEmpty = false;
                    break;
                }
            }
            
            if (isEmpty && initialItems != null && initialItems.Count > 0)
            {
                // This will only work correctly if the server has authority, 
                // but for now we just seed locally to the model if empty. 
                // A true multiplayer setup would have the host seed it.
                if (StorageSystem.StorageNetworkSync.Instance.HasStateAuthority || PlayerNetworkSetup.IsOfflineTestMode)
                {
                    foreach (var setup in initialItems)
                    {
                        if (setup.itemData != null)
                        {
                            var item = new ItemInstance(setup.itemData, setup.quantity);
                            model.TryAdd(item);
                        }
                    }
                    StorageSystem.StorageNetworkSync.Instance.SubmitStorageSnapshot(storageId, 
                        StorageSystem.StorageGridSerializer.ToSaveData(storageId, model));
                }
            }
        }

        public bool CanInteract(PlayerController player)
        {
            // 루팅 창이 이미 열려있지 않다면 상호작용 가능
            return LootController.Instance != null && !LootController.Instance.IsOpen;
        }

        public string GetInteractPrompt()
        {
            return "열기";
        }

        public string GetObjectName()
        {
            return lootTitle;
        }

        public void OnInteract(PlayerController player)
        {
            if (StorageSystem.StorageNetworkSync.Instance == null)
            {
                Debug.LogWarning("[InteractableLoot] StorageNetworkSync is missing.");
                return;
            }
            
            InitializeItemsIfNeeded();
            
            if (LootController.Instance != null)
            {
                LootController.Instance.OpenLoot(this, storageId, lootTitle, StorageSystem.StorageNetworkSync.Instance);
            }
        }

        public void SaveRemainingItems()
        {
            // Now handled by StorageNetworkSync
        }
    }
}
