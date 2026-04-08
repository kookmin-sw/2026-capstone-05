using UnityEngine;
using Systems.Inventory;

/// <summary>
/// 새로운 그리드 인벤토리 시스템 테스트용 스크립트
/// 기존 ItemDetails 시스템과 공존하면서 새 ItemData 시스템 테스트
/// </summary>
public class GridInventoryTester : MonoBehaviour
{
    [Header("Test Items (ItemData)")]
    [SerializeField] private ItemData[] testItems;
    [SerializeField] private int[] testQuantities = { 1, 5, 3 };
    
    [Header("기존 시스템과 구분을 위한 테스트")]
    [SerializeField] private bool useNewGridSystem = true;
    
    private GridInventorySystem inventorySystem;
    
    void Start()
    {
        if (useNewGridSystem)
        {
            // GridInventorySystem 찾기
            inventorySystem = FindFirstObjectByType<GridInventorySystem>();
            if (inventorySystem == null)
            {
                Debug.LogError("[GridInventoryTester] GridInventorySystem을 찾을 수 없습니다.");
                return;
            }
            
            //Debug.Log("[GridInventoryTester] 새 그리드 인벤토리 시스템 테스트 시작");
        }
        else
        {
            //Debug.Log("[GridInventoryTester] 기존 시스템과 공존 모드");
        }
    }
    
    void Update()
    {
        // T키로 테스트 아이템 추가
        if (Input.GetKeyDown(KeyCode.T))
        {
            AddTestItems();
        }
        
        // C키로 인벤토리 비우기
        if (Input.GetKeyDown(KeyCode.C))
        {
            ClearInventory();
        }
        
        // S키로 인벤토리 저장
        if (Input.GetKeyDown(KeyCode.S))
        {
            SaveInventory();
        }
        
        // L키로 인벤토리 로드
        if (Input.GetKeyDown(KeyCode.L))
        {
            LoadInventory();
        }
    }
    
    private void AddTestItems()
    {
        if (testItems == null || testItems.Length == 0 || inventorySystem == null) return;
        
        for (int i = 0; i < testItems.Length; i++)
        {
            if (testItems[i] != null)
            {
                int quantity = i < testQuantities.Length ? testQuantities[i] : 1;
                var itemInstance = new ItemInstance(testItems[i], quantity);
                
                if (inventorySystem.TryAddItem(itemInstance))
                {
                    //Debug.Log($"[GridInventoryTester] '{testItems[i].itemName}' x{quantity} 추가됨");
                }
                else
                {
                    Debug.LogWarning($"[GridInventoryTester] '{testItems[i].itemName}' 추가 실패 - 공간 부족");
                }
            }
        }
    }
    
    private void ClearInventory()
    {
        if (inventorySystem == null) return;
        
        inventorySystem.ClearInventory();
        
        Debug.Log("[GridInventoryTester] 인벤토리 비움");
    }
    
    private void SaveInventory()
    {
        if (inventorySystem == null) return;
        
        inventorySystem.SaveInventory();
    }
    
    private void LoadInventory()
    {
        if (inventorySystem == null) return;
        
        inventorySystem.LoadInventory();
    }
}