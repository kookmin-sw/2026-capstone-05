using UnityEngine;

public class PlayerEquipment : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerController player;
    [SerializeField] private Transform handSocket1P;

    public EquippedItemBehaviour CurrentItem { get; private set; }

    /// <summary>
    /// 인벤토리(퀵슬롯)에서 아이템을 선택했을 때 호출됩니다.
    /// </summary>
    public void EquipItem(ItemInstance itemInstance)
    {
        if (CurrentItem != null)
        {
            Destroy(CurrentItem.gameObject);
        }

        if (itemInstance.Data.equipPrefab == null)
        {
            Debug.Log("이 아이템은 손에 들 수 없습니다.");
            return;
        }

        GameObject spawnedItem = Instantiate(itemInstance.Data.equipPrefab, handSocket1P);

        CurrentItem = spawnedItem.GetComponent<EquippedItemBehaviour>();
        if (CurrentItem != null)
        {
            CurrentItem.Initialize(player, itemInstance);
        }
    }

    /// <summary>
    /// PlayerInputHandler에서 좌클릭(Action)을 감지했을 때 호출됩니다.
    /// </summary>
    public void UseCurrentItem()
    {
        if (CurrentItem != null)
        {
            CurrentItem.Use();
        }
    }
}