// using UnityEngine;
// using Systems.Inventory;

// /// <summary>
// /// 월드에 배치된 아이템 픽업 오브젝트.
// /// 플레이어가 상호작용하면 인벤토리에 아이템을 추가하고 오브젝트를 제거합니다.
// /// </summary>
// public class WorldItemPickup : MonoBehaviour, IInteractable
// {
//     [Header("Item Settings")]
//     [Tooltip("인벤토리에 추가할 아이템 (직접 방식)")]
//     [SerializeField] private ItemDetails itemDetails;
    
//     [Tooltip("ItemData를 ItemDetails로 변환하는 어댑터 (ItemData 사용 시)")]
//     [SerializeField] private ItemDataAdapter itemDataAdapter;

//     [Tooltip("획득할 수량")]
//     [SerializeField] private int quantity = 1;

//     [Header("Highlight Settings")]
//     [SerializeField] private Renderer[] renderers;
//     [ColorUsage(true, true)]
//     [SerializeField] private Color highlightColor = Color.yellow;
//     [Range(0f, 5f)]
//     [SerializeField] private float highlightIntensity = 0.3f;

//     private Systems.Inventory.Inventory mainInventory;
//     private bool isHighlighted = false;

//     private void Start()
//     {
//         mainInventory = FindFirstObjectByType<Systems.Inventory.Inventory>();

//         var actualItemDetails = GetActualItemDetails();
//         if (actualItemDetails == null)
//         {
//             Debug.LogWarning($"[WorldItemPickup] '{gameObject.name}'에 ItemDetails나 ItemDataAdapter가 설정되지 않았습니다.");
//         }
//     }
    
//     /// <summary>
//     /// 실제 사용할 ItemDetails를 반환합니다.
//     /// itemDetails가 우선이고, 없으면 itemDataAdapter에서 변환된 것을 사용합니다.
//     /// </summary>
//     private ItemDetails GetActualItemDetails()
//     {
//         // 직접 설정된 ItemDetails가 있으면 우선 사용
//         if (itemDetails != null)
//         {
//             return itemDetails;
//         }
        
//         // ItemDataAdapter를 통한 변환 시도
//         if (itemDataAdapter != null)
//         {
//             return itemDataAdapter.GetItemDetails();
//         }
        
//         return null;
//     }

//     public bool CanInteract(PlayerController player)
//     {
//         var actualItemDetails = GetActualItemDetails();
//         if (actualItemDetails == null || mainInventory == null)
//             return false;

//         return true;
//     }

//     public void OnInteract(PlayerController player)
//     {
//         if (!CanInteract(player))
//         {
//             if (mainInventory == null)
//                 Debug.LogWarning("[WorldItemPickup] 인벤토리를 찾을 수 없습니다.");
//             return;
//         }

//         var actualItemDetails = GetActualItemDetails();
//         bool added = mainInventory.AddItem(actualItemDetails, quantity);

//         if (added)
//         {
//             Debug.Log($"[WorldItemPickup] '{actualItemDetails.Name}' x{quantity} 획득!");
//             Destroy(gameObject);
//         }
//         else
//         {
//             Debug.Log($"[WorldItemPickup] 인벤토리가 가득 차서 '{actualItemDetails.Name}'을(를) 주울 수 없습니다.");
//         }
//     }

//     private void SetHighlight(bool active)
//     {
//         if (renderers == null || renderers.Length == 0) return;

//         foreach (Renderer rend in renderers)
//         {
//             if (rend == null) continue;
//             foreach (Material mat in rend.materials)
//             {
//                 if (active)
//                 {
//                     mat.EnableKeyword("_EMISSION");
//                     mat.SetColor("_EmissionColor", highlightColor * highlightIntensity);
//                 }
//                 else
//                 {
//                     mat.DisableKeyword("_EMISSION");
//                     mat.SetColor("_EmissionColor", Color.black);
//                 }
//             }
//         }
//     }

//     private void OnTriggerEnter(Collider other)
//     {
//         if (other.CompareTag("Player") && !isHighlighted)
//         {
//             isHighlighted = true;
//             SetHighlight(true);
//         }
//     }

//     private void OnTriggerExit(Collider other)
//     {
//         if (other.CompareTag("Player") && isHighlighted)
//         {
//             isHighlighted = false;
//             SetHighlight(false);
//         }
//     }
// }
