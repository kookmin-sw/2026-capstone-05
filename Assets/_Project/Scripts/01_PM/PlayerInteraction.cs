using UnityEngine;

/// <summary>
/// 플레이어의 상호작용을 관리하는 클래스 (레거시 코드 기반 새 시스템)
/// </summary>
public class PlayerInteraction : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerController player;

    [Header("Interaction Settings")]
    [SerializeField] private float interactionRange = 3f;
    [SerializeField] private LayerMask interactableLayers = -1;
    [SerializeField] private float interactionCooldown = 0.3f; // 상호작용 쿨다운 시간
    
    private float lastInteractionTime = 0f;
    
    private void Awake()
    {
        if (player == null)
            player = GetComponent<PlayerController>();
    }

    private void Update()
    {
        // Player Controller 와 InputHandler 가 잘 존재하는지 확인
        if (player != null && player.InputHandler != null)
        {
            // 사용자가 상호작용 키(E 등)를 눌렀는지 확인
            if (player.InputHandler.InteractTriggered)
            {
                PerformInteraction();
                
                // 중복해서 여러 오브젝트와 한 번에 상호작용하지 않게 입력 소모
                player.InputHandler.ConsumeInteract();
            }
        }
    }
    
    /// <summary>
    /// 상호작용을 수행하는 공용 메서드 - 레거시 로직 유지 (가장 가까운 객체 찾기)
    /// </summary>
    public void PerformInteraction()
    {
        // 쿨다운 체크
        if (Time.time - lastInteractionTime < interactionCooldown)
        {
            return;
        }
        
        lastInteractionTime = Time.time;
        
        IInteractable nearestInteractable = null;
        float nearestDistance = interactionRange;
        
        // 플레이어 주변의 모든 상호작용 가능한 오브젝트 확인
        Collider[] colliders = Physics.OverlapSphere(transform.position, interactionRange, interactableLayers);
        
        foreach (Collider col in colliders)
        {
            // 한 오브젝트에 여러 IInteractable이 있을 수 있으므로 모두 확인
            IInteractable[] interactables = col.GetComponents<IInteractable>();
            
            foreach (IInteractable interactable in interactables)
            {
                if (interactable != null && interactable.CanInteract(player))
                {
                    float distance = Vector3.Distance(transform.position, col.transform.position);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearestInteractable = interactable;
                    }
                }
            }
        }
        
        // 상호작용 대상이 있다면 실행
        if (nearestInteractable != null)
        {
            nearestInteractable.OnInteract(player);
        }
    }
    private void OnDrawGizmosSelected()
    {
        // 상호작용 범위 시각화
        Gizmos.color = Color.yellow; 
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}