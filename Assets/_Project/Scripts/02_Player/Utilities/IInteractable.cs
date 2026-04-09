public interface IInteractable
{
    bool CanInteract(PlayerController player); // 상호작용 가능 여부
    void OnInteract(PlayerController player);
    
    // UI 표시를 위한 메서드 추가
    string GetInteractPrompt(); 
    string GetObjectName();
}