using UnityEngine;

namespace Systems.StorageSystem
{
    public class StorageBox : MonoBehaviour, IInteractable
    {
        [Header("Storage")]
        [SerializeField] private string storageId = "Storage_0";
        [SerializeField] private StorageUI storageUI;
        [SerializeField] private StorageNetworkSync storageNetworkSync;

        [Header("Interaction")]
        [SerializeField] private string objectName = "\uCC3D\uACE0";
        [SerializeField] private string interactPrompt = "[E] \uCC3D\uACE0 \uC5F4\uAE30";
        [SerializeField] private bool autoCreateInteractionCollider = true;
        [SerializeField] private float assistColliderRadius = 3f;
        [SerializeField] private float maxInteractDistance = 4f;

        [Header("Focus Outline")]
        [SerializeField] private bool autoConfigureOutline = true;
        [SerializeField] private Outline.Mode outlineMode = Outline.Mode.OutlineAll;
        [SerializeField] private Color outlineColor = Color.white;
        [SerializeField] private float outlineWidth = 5f;

        private SphereCollider assistCollider;
        private Outline cachedOutline;

        public string StorageId => storageId;

        public void Configure(string newStorageId, StorageUI newStorageUI, StorageNetworkSync newStorageNetworkSync)
        {
            if (!string.IsNullOrWhiteSpace(newStorageId))
            {
                storageId = newStorageId;
            }

            storageUI = newStorageUI;
            storageNetworkSync = newStorageNetworkSync;
        }

        public bool CanInteract(PlayerController player)
        {
            if (player == null || player.InputHandler == null)
            {
                return false;
            }

            return Vector3.Distance(player.transform.position, transform.position) <= Mathf.Max(assistColliderRadius, maxInteractDistance);
        }

        public void OnInteract(PlayerController player)
        {
            ResolveReferences();
            if (storageUI == null || storageNetworkSync == null)
            {
                Debug.LogWarning($"[StorageBox] Storage UI or network sync is missing for '{storageId}'.");
                return;
            }

            storageUI.Open(storageId, storageNetworkSync);
        }

        public string GetInteractPrompt()
        {
            return interactPrompt;
        }

        public string GetObjectName()
        {
            return objectName;
        }

        private void Awake()
        {
            ResolveReferences();

            if (autoCreateInteractionCollider)
            {
                EnsureAssistCollider();
            }

            if (autoConfigureOutline)
            {
                EnsureOutlineComponent();
            }
        }

        private void OnValidate()
        {
            assistColliderRadius = Mathf.Max(0.5f, assistColliderRadius);
            maxInteractDistance = Mathf.Max(assistColliderRadius, maxInteractDistance);
            outlineWidth = Mathf.Max(0f, outlineWidth);

            if (assistCollider != null)
            {
                assistCollider.radius = assistColliderRadius;
            }

            ApplyOutlineStyle();
        }

        private void ResolveReferences()
        {
            if (storageUI == null)
            {
                StorageUI[] storageUis = FindObjectsByType<StorageUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                storageUI = storageUis.Length > 0 ? storageUis[0] : null;
            }

            if (storageNetworkSync == null)
            {
                storageNetworkSync = StorageNetworkSync.Instance ?? FindFirstObjectByType<StorageNetworkSync>();
            }
        }

        private void EnsureAssistCollider()
        {
            assistCollider = GetComponent<SphereCollider>();
            if (assistCollider == null)
            {
                assistCollider = gameObject.AddComponent<SphereCollider>();
            }

            assistCollider.isTrigger = true;
            assistCollider.radius = assistColliderRadius;
        }

        private void EnsureOutlineComponent()
        {
            cachedOutline = GetComponent<Outline>();
            if (cachedOutline == null)
            {
                cachedOutline = gameObject.AddComponent<Outline>();
            }

            ApplyOutlineStyle();
            cachedOutline.enabled = false;
        }

        private void ApplyOutlineStyle()
        {
            if (cachedOutline == null)
            {
                return;
            }

            cachedOutline.OutlineMode = outlineMode;
            cachedOutline.OutlineColor = outlineColor;
            cachedOutline.OutlineWidth = outlineWidth;
        }
    }
}
