using UnityEngine;
using Systems.Inventory;

namespace Systems.Inventory
{
    /// <summary>
    /// ItemData 기반 월드 아이템 픽업 시스템
    /// Player 담당 개발자가 설계한 ItemData/ItemInstance 구조를 따름
    /// </summary>
    public class WorldItemPickup_ItemData : MonoBehaviour, IInteractable
    {
        [Header("Item Data Settings")]
        [Tooltip("픽업할 아이템의 데이터")]
        [SerializeField] private ItemData itemData;
        
        [Tooltip("픽업할 수량")]
        [SerializeField] private int quantity = 1;

        [Header("Highlight Settings")]
        [SerializeField] private Renderer[] renderers;
        [ColorUsage(true, true)]
        [SerializeField] private Color highlightColor = Color.yellow;
        [Range(0f, 5f)]
        [SerializeField] private float highlightIntensity = 0.3f;

        private GridInventorySystem gridInventory;
        private bool isHighlighted = false;

        private void Start()
        {
            // PM 인벤토리 시스템 찾기
            gridInventory = FindFirstObjectByType<GridInventorySystem>();

            if (itemData == null)
            {
                Debug.LogWarning($"[WorldItemPickup_ItemData] '{gameObject.name}'에 ItemData가 설정되지 않았습니다.");
            }
        }

        public bool CanInteract(PlayerController player)
        {
            if (itemData == null || gridInventory == null)
                return false;

            return true;
        }

        public void OnInteract(PlayerController player)
        {
            if (!CanInteract(player))
            {
                if (gridInventory == null)
                    Debug.LogWarning("[WorldItemPickup_ItemData] GridInventorySystem을 찾을 수 없습니다.");
                return;
            }

            // ItemInstance 생성 (Player 개발자의 구조 따름)
            ItemInstance itemInstance = new ItemInstance(itemData, quantity);

            // 그리드 인벤토리에 추가 시도
            bool added = gridInventory.TryAddItem(itemInstance);

            if (added)
            {
                Debug.Log($"[WorldItemPickup_ItemData] '{itemData.itemName}' x{quantity} 획득!");
                Destroy(gameObject);
            }
            else
            {
                Debug.Log($"[WorldItemPickup_ItemData] 인벤토리 공간이 부족하여 '{itemData.itemName}'을(를) 주울 수 없습니다.");
            }
        }

        private void SetHighlight(bool active)
        {
            if (renderers == null || renderers.Length == 0) return;

            foreach (Renderer rend in renderers)
            {
                if (rend == null) continue;
                foreach (Material mat in rend.materials)
                {
                    if (active)
                    {
                        mat.EnableKeyword("_EMISSION");
                        mat.SetColor("_EmissionColor", highlightColor * highlightIntensity);
                    }
                    else
                    {
                        mat.DisableKeyword("_EMISSION");
                        mat.SetColor("_EmissionColor", Color.black);
                    }
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player") && !isHighlighted)
            {
                isHighlighted = true;
                SetHighlight(true);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player") && isHighlighted)
            {
                isHighlighted = false;
                SetHighlight(false);
            }
        }
    }
}