public interface IInteractable
{
    bool CanInteract(PlayerController player); // 상호작용 가능 여부
    void OnInteract(PlayerController player);
    // string GetInteractPrompt(); // UI 표시
}