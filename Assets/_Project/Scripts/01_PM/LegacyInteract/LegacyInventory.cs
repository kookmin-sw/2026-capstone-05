using UnityEngine;
using Systems.GridInventory;

/// <summary>
/// 메인 인벤토리(Grid)와 상호작용(Key 등)을 연결해주는 브릿지 인벤토리 클래스
/// </summary>
public class LegacyInventory : MonoBehaviour
{
    [Header("Item Link Settings")]
    [Tooltip("열쇠로 사용할 아이템 데이터를 연결해주세요.")]
    [SerializeField] private ItemData keyItemAsset; 

    [Header("UI Settings")]
    [SerializeField] private GameObject keyUIObject; // 기존 UI 하위호환용 (선택)

    private Systems.GridInventory.GridInventory mainInventory;

    private void Start()
    {
        mainInventory = FindFirstObjectByType<Systems.GridInventory.GridInventory>();
        UpdateKeyUI();
    }
    
    /// <summary>
    /// 열쇠를 인벤토리에 추가
    /// </summary>
    /// <param name="amount">추가할 열쇠 개수</param>
    public void AddKeys(int amount)
    {
        if (mainInventory != null && keyItemAsset != null)
        {
            mainInventory.AddItem(keyItemAsset, amount);
            Debug.Log($"🔑 {amount}개의 열쇠({keyItemAsset.itemName})를 획득했습니다!");
            UpdateKeyUI();
        }
        else
        {
            Debug.LogWarning("메인 인벤토리 또는 Key Item Asset이 연결되지 않았습니다.");
        }
    }
    
    /// <summary>
    /// 열쇠를 가지고 있는지 확인
    /// </summary>
    /// <returns>열쇠를 가지고 있으면 true</returns>
    public bool HasKey()
    {
        if (mainInventory != null && keyItemAsset != null)
        {
            return mainInventory.HasItem(keyItemAsset, 1);
        }
        return false;
    }
    
    /// <summary>
    /// 열쇠를 사용하여 인벤토리에서 제거
    /// </summary>
    /// <returns>열쇠를 성공적으로 사용했으면 true</returns>
    public bool UseKey()
    {
        if (mainInventory != null && keyItemAsset != null)
        {
            if (mainInventory.ConsumeItem(keyItemAsset, 1))
            {
                Debug.Log($"🔓 열쇠를 사용했습니다!");
                UpdateKeyUI();
                return true;
            }
        }
        Debug.Log("❌ 사용할 수 있는 열쇠가 없거나 시스템에 연결되지 않았습니다!");
        return false;
    }

    private void UpdateKeyUI()
    {
        if (keyUIObject != null)
        {
            keyUIObject.SetActive(HasKey());
        }
    }
}