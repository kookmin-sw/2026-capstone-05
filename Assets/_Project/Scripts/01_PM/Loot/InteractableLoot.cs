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
        [Header("Loot Configuration Asset")]
        [Tooltip("When assigned, this asset overrides the fallback fields below.")]
        [SerializeField] private LootConfiguration lootConfiguration;

        [Header("Loot Configuration")]
        [Tooltip("Used as the save/network key when no Loot Configuration asset is assigned.")]
        [SerializeField] private string storageId = "Loot_0";
        [SerializeField] private string lootTitle = "Supply Chest";
        [SerializeField, Min(1)] private int lootWidth = 9;
        [SerializeField, Min(1)] private int lootHeight = 18;
        
        [Header("Initial Items (Only spawned if storage is empty)")]
        [SerializeField] private List<LootItemSetup> initialItems;
        
        public string StorageId => lootConfiguration != null ? lootConfiguration.StorageId : NormalizeStorageId(storageId);
        public int Width => lootConfiguration != null ? lootConfiguration.Width : Mathf.Max(1, lootWidth);
        public int Height => lootConfiguration != null ? lootConfiguration.Height : Mathf.Max(1, lootHeight);
        private string LootTitle => lootConfiguration != null ? lootConfiguration.LootTitle : lootTitle;
        private IReadOnlyList<LootItemSetup> InitialItems => lootConfiguration != null ? lootConfiguration.InitialItems : initialItems;
        
        private void Awake()
        {
        }

        private void OnValidate()
        {
            lootWidth = Mathf.Max(1, lootWidth);
            lootHeight = Mathf.Max(1, lootHeight);
        }
        
        private bool isInitialized = false;

        private void InitializeItemsIfNeeded()
        {
            if (isInitialized) return;
            isInitialized = true;
            
            if (LootNetworkSync.Instance == null)
            {
                if (PlayerNetworkSetup.IsOfflineTestMode)
                {
                    var instance = LootNetworkSync.Instance; // Property getter handles creation
                    if (instance == null) return;
                }
                else
                {
                    return;
                }
            }
            
            string resolvedStorageId = StorageId;
            var model = LootNetworkSync.Instance.GetOrCreateModel(resolvedStorageId, Width, Height);
            
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
            
            IReadOnlyList<LootItemSetup> resolvedInitialItems = InitialItems;
            if (isEmpty && resolvedInitialItems != null && resolvedInitialItems.Count > 0)
            {
                // This will only work correctly if the server has authority, 
                // but for now we just seed locally to the model if empty. 
                // A true multiplayer setup would have the host seed it.
                if (LootNetworkSync.Instance.HasStateAuthority || PlayerNetworkSetup.IsOfflineTestMode)
                {
                    foreach (var setup in resolvedInitialItems)
                    {
                        if (setup.itemData != null)
                        {
                            var item = new ItemInstance(setup.itemData, setup.quantity);
                            model.TryAdd(item);
                        }
                    }
                    LootNetworkSync.Instance.SubmitLootSnapshot(resolvedStorageId, 
                        LootGridSerializer.ToSaveData(resolvedStorageId, model));
                }
            }
        }

        public bool CanInteract(PlayerController player)
        {
            if (LootController.Instance != null && LootController.Instance.IsOpen)
            {
                return false;
            }

            if (LootNetworkSync.Instance != null && !PlayerNetworkSetup.IsOfflineTestMode)
            {
                if (LootNetworkSync.Instance.ActiveLootUsers.TryGet(StorageId, out Fusion.PlayerRef currentUser))
                {
                    if (LootNetworkSync.Instance.Runner != null && currentUser != LootNetworkSync.Instance.Runner.LocalPlayer)
                    {
                        return false; // 누군가 사용 중이면 상호작용 불가
                    }
                }
            }

            return true;
        }

        public string GetInteractPrompt()
        {
            return "열기";
        }

        public string GetObjectName()
        {
            return LootTitle;
        }

        public void OnInteract(PlayerController player)
        {
            if (LootNetworkSync.Instance == null)
            {
                Debug.LogWarning("[InteractableLoot] LootNetworkSync is missing.");
                // 오프라인 모드일 때 인스턴스 강제 생성 시도
                if (PlayerNetworkSetup.IsOfflineTestMode)
                {
                    var instance = LootNetworkSync.Instance; // Property getter handles creation
                    if (instance == null) return;
                }
                else
                {
                    return;
                }
            }
            
            InitializeItemsIfNeeded();
            
            if (LootController.Instance != null)
            {
                LootController.Instance.RequestOpenLoot(this, StorageId, LootTitle, LootNetworkSync.Instance);
            }
            else
            {
                Debug.LogWarning("[InteractableLoot] LootController.Instance is missing. Cannot open loot.");
            }
        }

        public void SaveRemainingItems()
        {
            // Now handled by LootNetworkSync
        }

        private static string NormalizeStorageId(string id)
        {
            return string.IsNullOrWhiteSpace(id) ? "Loot_0" : id.Trim();
        }
    }
}
