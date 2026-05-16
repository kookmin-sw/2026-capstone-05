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
        [Tooltip("Required. Defines the storage key, title, size, and initial items for this loot container.")]
        [SerializeField] private LootConfiguration lootConfiguration;
        
        public bool HasConfiguration => lootConfiguration != null;
        public string StorageId => lootConfiguration.StorageId;
        public int Width => lootConfiguration.Width;
        public int Height => lootConfiguration.Height;
        private string LootTitle => lootConfiguration.LootTitle;
        private IReadOnlyList<LootItemSetup> InitialItems => lootConfiguration.InitialItems;
        
        private bool isInitialized = false;

        private void OnValidate()
        {
            if (lootConfiguration == null)
            {
                Debug.LogWarning($"[InteractableLoot] Loot Configuration is missing on {name}.", this);
            }
        }

        private void InitializeItemsIfNeeded()
        {
            if (isInitialized) return;
            isInitialized = true;

            if (!HasConfiguration)
            {
                Debug.LogError($"[InteractableLoot] Loot Configuration is missing on {name}.", this);
                return;
            }
            
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
            
            // Only seed items if the storage is completely empty.
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
            if (!HasConfiguration)
            {
                return false;
            }

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
            return HasConfiguration ? LootTitle : string.Empty;
        }

        public void OnInteract(PlayerController player)
        {
            if (!HasConfiguration)
            {
                Debug.LogError($"[InteractableLoot] Loot Configuration is missing on {name}.", this);
                return;
            }

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
    }
}
