/// <summary>
/// 상호작용 가능한 오브젝트들이 구현해야 하는 인터페이스
/// </summary>
public interface IInteractable
{
    /// <summary>
    /// 상호작용이 가능한지 확인
    /// </summary>
    /// <returns>상호작용 가능하면 true</returns>
    bool CanInteract();
    
    /// <summary>
    /// 상호작용 실행
    /// </summary>
    void Interact();
}