using UnityEngine;

/// <summary>
/// 플레이어의 인벤토리를 관리하는 클래스
/// </summary>
public class PlayerInventory : MonoBehaviour
{
    [Header("Inventory Settings")]
    [SerializeField] private int keyCount = 0;

    [Header("UI Settings")]
    [SerializeField] private GameObject keyUIObject; // 열쇠 UI 오브젝트

    private void Start()
    {
        UpdateKeyUI();
    }
    
    /// <summary>
    /// 열쇠를 인벤토리에 추가
    /// </summary>
    /// <param name="amount">추가할 열쇠 개수</param>
    public void AddKeys(int amount)
    {
        keyCount += amount;
        Debug.Log($"🔑 {amount}개의 열쇠를 획득했습니다! 현재 열쇠: {keyCount}개");
        UpdateKeyUI();
    }
    
    /// <summary>
    /// 열쇠를 가지고 있는지 확인
    /// </summary>
    /// <returns>열쇠를 가지고 있으면 true</returns>
    public bool HasKey()
    {
        return keyCount > 0;
    }
    
    /// <summary>
    /// 열쇠를 사용하여 인벤토리에서 제거
    /// </summary>
    /// <returns>열쇠를 성공적으로 사용했으면 true</returns>
    public bool UseKey()
    {
        if (keyCount > 0)
        {
            keyCount--;
            Debug.Log($"🔓 열쇠를 사용했습니다! 남은 열쇠: {keyCount}개");
            UpdateKeyUI();
            return true;
        }
        Debug.Log("❌ 사용할 수 있는 열쇠가 없습니다!");
        return false;
    }

    private void UpdateKeyUI()
    {
        if (keyUIObject != null)
        {
            keyUIObject.SetActive(keyCount > 0);
        }
    }
    
    /// <summary>
    /// 현재 가지고 있는 열쇠 개수를 반환
    /// </summary>
    /// <returns>열쇠 개수</returns>
    public int GetKeyCount()
    {
        return keyCount;
    }
}