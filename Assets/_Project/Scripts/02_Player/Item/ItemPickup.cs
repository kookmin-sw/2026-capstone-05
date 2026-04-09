using UnityEngine;

/// <summary>
/// 필드에 존재하는 아이템 클래스
/// </summary>
public class ItemPickup : MonoBehaviour, IInteractable
{
    public ItemInstance itemInstance;

    public bool CanInteract(PlayerController player)
    {
        // 1. 플레이어가 아이템을 줍는 범위 내에 있는지 체크
        // 2. 플레이어 인벤토리에 아이템을 추가할 공간이 있는지 체크
        return true; // 임시로 항상 상호작용 가능하도록 설정
    }

    public void OnInteract(PlayerController player)
    {
        if (QuickslotUIController.Instance == null)
        {
            Debug.LogError("QuickslotUIController.Instance 가 없습니다! 씬에 QuickslotUIController가 배치되어 있는지 확인해주세요.");
            return;
        }

        // 1. 플레이어 인벤토리(퀵슬롯)에 아이템 추가 시도
        bool added = QuickslotUIController.Instance.AddItemToEmptySlot(itemInstance);
        
        if (added)
        {
            // 2. 성공하면 월드에서 이 오브젝트 파괴 (Destroy)
            Destroy(gameObject);
            
            // 3. 줍는 소음(Noise) 발생 등
            if (player.NoiseEmitter != null)
            {
                // 소음 발생 로직 예시
                // player.NoiseEmitter.EmitNoise(1.5f, transform.position);
            }
        }
        else
        {
            Debug.LogWarning("퀵슬롯에 빈 자리가 없습니다!");
        }
    }

    public string GetInteractPrompt()
    {
        return "[E] 줍기";
    }

    public string GetObjectName()
    {
        return itemInstance?.Data?.itemName ?? "알 수 없는 아이템";
    }
}