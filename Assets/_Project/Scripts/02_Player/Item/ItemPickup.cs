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
        // 1. 플레이어 인벤토리에 아이템 추가 시도
        // 2. 성공하면 월드에서 이 오브젝트 파괴 (Destroy)
        // 3. 줍는 소음(Noise) 발생 등
    }
}