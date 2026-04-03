using UnityEngine;

/// <summary>
/// 필드에 존재하는 아이템 클래스
/// </summary>
public class ItemPickup : MonoBehaviour, IInteractable
{
    public ItemInstance itemInstance;

    public void OnInteract(PlayerController player)
    {
        // 1. 플레이어 인벤토리에 아이템 추가 시도
        // 2. 성공하면 월드에서 이 오브젝트 파괴 (Destroy)
        // 3. 줍는 소음(Noise) 발생 등
    }
}