using UnityEngine;

/// <summary>
/// 플레이어의 상호작용 입력을 감지하여 가장 가까운 IInteractable을 호출하는 시스템.
/// PlayerController와 같은 GameObject에 부착합니다.
/// </summary>
public class InteractionSystem : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerController player;

    [Header("Interaction Settings")]
    [SerializeField] private float interactionRange = 3f;
    [SerializeField] private LayerMask interactableLayers = -1;
    [SerializeField] private float interactionCooldown = 0.3f;

    private float lastInteractionTime;

    private void Awake()
    {
        if (player == null)
            player = GetComponent<PlayerController>();
    }

    private void Update()
    {
        if (player == null || player.InputHandler == null) return;

        if (player.InputHandler.InteractTriggered)
        {
            player.InputHandler.ConsumeInteract();

            if (Time.time - lastInteractionTime >= interactionCooldown)
            {
                lastInteractionTime = Time.time;
                TryInteract();
            }
        }
    }

    private void TryInteract()
    {
        IInteractable nearest = null;
        float nearestDist = interactionRange;

        Collider[] colliders = Physics.OverlapSphere(transform.position, interactionRange, interactableLayers);

        foreach (Collider col in colliders)
        {
            IInteractable[] interactables = col.GetComponents<IInteractable>();

            foreach (IInteractable interactable in interactables)
            {
                if (interactable == null || !interactable.CanInteract(player)) continue;

                float dist = Vector3.Distance(transform.position, col.transform.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = interactable;
                }
            }
        }

        nearest?.OnInteract(player);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}
