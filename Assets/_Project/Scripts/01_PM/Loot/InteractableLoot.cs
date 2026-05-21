using System;
using FMODUnity;
using UnityEngine;
using Systems.GridInventory;
using UnityEngine.Localization.Settings;


#if UNITY_EDITOR
using UnityEditor;
#endif

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
        [Tooltip("Container type definition. Multiple containers can share the same definition.")]
        [SerializeField] private LootConfiguration lootConfiguration;

        [Header("Generated Storage")]
        [Tooltip("Unique save/network key for this placed container. Auto-generated for scene instances.")]
        [SerializeField] private string generatedStorageId;

        [Header("Save Settings")]
        [Tooltip("If true, this storage will be saved and loaded. If false, it will be reset every time.")]
        [SerializeField] private bool isBaseStorage = false;

        [Header("Sound")]
        [SerializeField] private EventReference interactionSoundEvent;
        [SerializeField] private Transform soundOrigin;

        public bool HasConfiguration => lootConfiguration != null;
        public string StorageId => NormalizeStorageId(generatedStorageId);
        public bool IsBaseStorage => isBaseStorage;
        public int Width => lootConfiguration.Width;
        public int Height => lootConfiguration.Height;
        private string LootTitle => lootConfiguration.LootTitle;

        private readonly string interactPromptTable = "InteractPrompts";
        private readonly string interactPromptKey = "OpenLoot";

        private bool isInitialized = false;

        private void Reset()
        {
            EnsureStorageId();
        }

        private void OnValidate()
        {
            EnsureStorageId();
#if UNITY_EDITOR
            EnsureUniqueStorageIdInScene();
#endif

            if (lootConfiguration == null)
            {
                Debug.LogWarning($"[InteractableLoot] Loot Configuration is missing on {name}.", this);
            }
        }

        private void Awake()
        {
            EnsureStorageId();
        }

        public void InitializeItemsIfNeeded()
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
                if (AuthSession.IsOffline)
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

            bool isEmpty = true;
            for (int i = 0; i < model.Items.Length; i++)
            {
                if (model.Items[i] != null)
                {
                    isEmpty = false;
                    break;
                }
            }

            bool canSeedLoot = LootNetworkSync.Instance.HasStateAuthority || AuthSession.IsOffline;
            if (isEmpty && canSeedLoot)
            {
                bool shouldPopulate = true;
                if (isBaseStorage && LootSaveManager.HasLootSave(resolvedStorageId))
                {
                    shouldPopulate = false;
                }

                if (shouldPopulate)
                {
                    lootConfiguration.PopulateModel(model, resolvedStorageId);

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

            if (LootNetworkSync.Instance != null && !AuthSession.IsOffline)
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
            return LocalizationSettings.StringDatabase.GetLocalizedString(interactPromptTable, interactPromptKey);
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
                if (AuthSession.IsOffline)
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
                PlayInteractionSound(player);
            }
            else
            {
                Debug.LogWarning("[InteractableLoot] LootController.Instance is missing. Cannot open loot.");
            }
        }

        private void PlayInteractionSound(PlayerController player)
        {
            if (interactionSoundEvent.IsNull)
            {
                return;
            }

            Vector3 position = player != null
                ? player.transform.position
                : soundOrigin != null ? soundOrigin.position : transform.position;
            RuntimeManager.PlayOneShot(interactionSoundEvent, position);
            NoiseManager.Instance.GenerateNoise(position, NoiseData.NoiseType.Box);
        }

        public void SaveRemainingItems()
        {
            // Now handled by LootNetworkSync
        }

#if UNITY_EDITOR
        [ContextMenu("Regenerate Storage Id")]
        private void RegenerateStorageId()
        {
            generatedStorageId = BuildGeneratedStorageId();
            EditorUtility.SetDirty(this);
        }
#endif

        private void EnsureStorageId()
        {
            if (!string.IsNullOrWhiteSpace(generatedStorageId))
            {
                generatedStorageId = generatedStorageId.Trim();
                return;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying && EditorUtility.IsPersistent(gameObject))
            {
                return;
            }
#endif

            generatedStorageId = BuildGeneratedStorageId();
        }

#if UNITY_EDITOR
        private void EnsureUniqueStorageIdInScene()
        {
            if (Application.isPlaying ||
                EditorUtility.IsPersistent(gameObject) ||
                string.IsNullOrWhiteSpace(generatedStorageId))
            {
                return;
            }

            InteractableLoot[] loots = FindObjectsByType<InteractableLoot>(FindObjectsSortMode.None);
            foreach (InteractableLoot loot in loots)
            {
                if (loot == null || loot == this)
                {
                    continue;
                }

                if (loot.gameObject.scene != gameObject.scene)
                {
                    continue;
                }

                if (loot.generatedStorageId == generatedStorageId)
                {
                    generatedStorageId = BuildGeneratedStorageId();
                    EditorUtility.SetDirty(this);
                    return;
                }
            }
        }
#endif

        private string BuildGeneratedStorageId()
        {
            string prefix = lootConfiguration != null
                ? lootConfiguration.StorageIdPrefix
                : "Loot";
            string safePrefix = SanitizeStorageIdPart(prefix);

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                return $"{safePrefix}_{Guid.NewGuid():N}".Substring(0, safePrefix.Length + 9);
            }
#endif

            return $"{safePrefix}_{BuildStableObjectHash()}";
        }

        private string BuildStableObjectHash()
        {
            unchecked
            {
                int hash = (int)2166136261;
                AppendStableHash(ref hash, gameObject.scene.path);
                AppendStableHash(ref hash, BuildHierarchyPath(transform));
                hash = hash * 16777619 ^ Mathf.RoundToInt(transform.position.x * 100f);
                hash = hash * 16777619 ^ Mathf.RoundToInt(transform.position.y * 100f);
                hash = hash * 16777619 ^ Mathf.RoundToInt(transform.position.z * 100f);
                return hash == 0 ? "00000001" : hash.ToString("x8");
            }
        }

        private static string NormalizeStorageId(string id)
        {
            return string.IsNullOrWhiteSpace(id) ? "Loot_0" : id.Trim();
        }

        private static string SanitizeStorageIdPart(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Loot";
            }

            char[] chars = value.Trim().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_' && chars[i] != '-')
                {
                    chars[i] = '_';
                }
            }

            return new string(chars);
        }

        private static string BuildHierarchyPath(Transform target)
        {
            if (target == null)
            {
                return string.Empty;
            }

            string path = target.name;
            Transform parent = target.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return path;
        }

        private static void AppendStableHash(ref int hash, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            unchecked
            {
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= 16777619;
                }
            }
        }
    }
}
