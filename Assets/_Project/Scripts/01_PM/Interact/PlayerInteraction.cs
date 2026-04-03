using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 플레이어의 상호작용을 관리하는 클래스
/// </summary>
public class PlayerInteraction : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private float interactionRange = 3f;
    [SerializeField] private LayerMask interactableLayers = -1;
    [SerializeField] private float interactionCooldown = 0.3f; // 상호작용 쿨다운 시간
    
    private Camera playerCamera;
    private PlayerInput playerInput;
    private float lastInteractionTime = 0f;
    
    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
    }
    
    private void Start()
    {
        playerCamera = Camera.main;
        if (playerCamera == null)
        {
            playerCamera = FindFirstObjectByType<Camera>();
        }
    }
    

    


    
    /// <summary>
    /// 상호작용을 수행하는 공용 메서드 - E키 누를 때만 가장 가까운 객체 찾기
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
            // 한 오브젝트에 여러 IInteractable이 있을 수 있으므로 모두 확인 (예: LockedDoor + BoatInteraction)
            IInteractable[] interactables = col.GetComponents<IInteractable>();
            
            foreach (IInteractable interactable in interactables)
            {
                if (interactable != null && interactable.CanInteract())
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
        
        if (nearestInteractable != null)
        {
            nearestInteractable.Interact();
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        // 상호작용 범위 시각화
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}