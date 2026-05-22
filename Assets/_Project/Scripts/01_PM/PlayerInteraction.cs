using UnityEngine;
using UnityEngine.Localization.Settings;

/// <summary>
/// 플레이어의 상호작용을 관리하는 클래스 (레거시 코드 기반 새 시스템)
/// </summary>
public class PlayerInteraction : MonoBehaviour, IPlayerNetworkConfigurable
{
    [Header("References")]
    [SerializeField] private PlayerController player;
    [SerializeField] private Transform cameraTransform;

    [Header("Interaction Settings")]
    [SerializeField] private float interactionRange = 3f;
    [SerializeField] private LayerMask interactableLayers = -1;
    [SerializeField] private float interactionCooldown = 0.3f; // 상호작용 쿨다운 시간
    
    private float lastInteractionTime = 0f;
    private IInteractable currentInteractable = null;
    private GameObject currentLookObject = null;
    
    // 외곽선 효과를 위한 변수
    private Outline currentOutline;
    private float holdProgressSeconds;
    private bool holdTriggered;
    private bool networkConfigured;
    private readonly RaycastHit[] interactionHits = new RaycastHit[64];

    private readonly string interactPromptTable = "InteractPrompts";
    private readonly string holdPrefixKey = "HoldPrefix";

    private void Awake()
    {
        if (player == null)
            player = GetComponentInParent<PlayerController>(); // PlayerController가 부모나 상위에 있을 가능성 대비
        
        if (player == null)
            player = FindAnyObjectByType<PlayerController>();
        
        ResolveInteractionCamera();
    }
    private void Update()
    {
        if (player == null || player.InputHandler == null)
        {
            return;
        }

        if (networkConfigured && !player.IsLocalPlayer)
        {
            ClearCurrentInteractable();
            return;
        }

        CheckInteractionFocus();

        if (currentInteractable is IHoldInteractable holdInteractable)
        {
            UpdateHoldInteraction(holdInteractable);
            return;
        }

        if (player.InputHandler.InteractTriggered)
        {
            PerformInteraction();
            player.InputHandler.ConsumeInteract();
        }
    }

    /// <summary>
    /// 플레이어의 시선을 추적하여 상호작용 가능한 객체에 외곽선과 UI를 표시합니다.
    /// </summary>
    private void CheckInteractionFocus()
    {
        if (!PlayerAimRayProvider.TryGetAimRay(player, out Ray ray))
        {
            ClearCurrentInteractable();
            return;
        }

        if (Physics.Raycast(ray, out RaycastHit hit, interactionRange, interactableLayers))
        {
            // 콜라이더 자체나 부모 객체에 IInteractable 인터페이스가 있는지 확인
            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();

            if (interactable != null && interactable.CanInteract(player))
            {
                GameObject targetObj = (interactable as MonoBehaviour)?.gameObject ?? hit.collider.gameObject;

                if (currentLookObject != targetObj)
                {
                    ClearCurrentInteractable();
                    SetCurrentInteractable(targetObj, interactable);
                }
                return;
            }
        }

        if (currentLookObject != null)
        {
            ClearCurrentInteractable();
        }
    }

    private void UpdateHoldInteraction(IHoldInteractable holdInteractable)
    {
        if (Time.time - lastInteractionTime < interactionCooldown)
        {
            return;
        }

        if (!player.InputHandler.IsInteractPressed)
        {
            holdProgressSeconds = 0f;
            holdTriggered = false;
            RefreshCurrentInteractionPrompt();
            return;
        }

        if (holdTriggered)
        {
            return;
        }

        holdProgressSeconds += Time.deltaTime;
        float requiredSeconds = Mathf.Max(0.1f, holdInteractable.GetHoldDuration(player));
        RefreshCurrentInteractionPrompt();

        if (holdProgressSeconds >= requiredSeconds)
        {
            holdTriggered = true;
            holdProgressSeconds = 0f;
            holdInteractable.OnHoldInteract(player);
            lastInteractionTime = Time.time;
            player.InputHandler.ConsumeInteract();
            RefreshCurrentInteractionPrompt();
        }
    }

    private void SetCurrentInteractable(GameObject obj, IInteractable interactable)
    {
        currentLookObject = obj;
        currentInteractable = interactable;

        // 외곽선 활성화
        currentOutline = obj.GetComponent<Outline>();
        if (currentOutline == null)
        {
            currentOutline = obj.AddComponent<Outline>();
            currentOutline.OutlineMode = Outline.Mode.OutlineAll; // 렌더러 위에 항상 외곽선 표시 (가려져 있어도 표시되게 하여 확실히 뜨게 함)
            currentOutline.OutlineColor = Color.white; // 사진과 동일한 흰색
            currentOutline.OutlineWidth = 5f; // 좀 더 명확하게 두께를 설정
        }
        currentOutline.enabled = true;

        // UI 표시 (임시 이름 사용, 필요 시 IInteractable에 속성 추가해서 사용)
        if (InteractionUI.Instance != null)
        {
            string objName = interactable.GetObjectName();
            string prompt = BuildInteractionPrompt(interactable);
            InteractionUI.Instance.Show(objName, prompt, obj.transform);
        }
    }

    private void RefreshCurrentInteractionPrompt()
    {
        if (currentInteractable == null || currentLookObject == null || InteractionUI.Instance == null)
        {
            return;
        }

        InteractionUI.Instance.RefreshPromptForTarget(
            currentLookObject.transform,
            currentInteractable.GetObjectName(),
            BuildInteractionPrompt(currentInteractable));
    }

    private string BuildInteractionPrompt(IInteractable interactable)
    {
        string keyPrefix = player.InputHandler.GetInteractKey();
        string prompt = interactable.GetInteractPrompt();

        if (interactable is IHoldInteractable holdInteractable)
        {
            float holdSeconds = Mathf.Max(0.1f, holdInteractable.GetHoldDuration(player));
            float displaySeconds = holdSeconds;
            if (player.InputHandler.IsInteractPressed)
            {
                displaySeconds = holdTriggered ? 0f : Mathf.Max(0f, holdSeconds - holdProgressSeconds);
            }

            string holdPrefix = LocalizationSettings.StringDatabase.GetLocalizedString(interactPromptTable, holdPrefixKey);
            return $"{holdPrefix} [{keyPrefix} {displaySeconds:0.0}s] {prompt}";
        }

        return $"[{keyPrefix}] {prompt}";
    }

    private void ClearCurrentInteractable()
    {
        // 1. UI 먼저 무조건 숨김 (오브젝트가 방금 파괴되었더라도 UI는 남아있을 수 있으므로)
        if ((currentLookObject != null || currentInteractable != null) && InteractionUI.Instance != null)
        {
            InteractionUI.Instance.Hide();
        }

        // 2. 오브젝트가 아직 파괴되지 않고 남아있다면 외곽선 꺼줌
        if (currentLookObject != null && currentOutline != null)
        {
            currentOutline.enabled = false;
        }

        // 3. 변수 초기화
        currentLookObject = null;
        currentInteractable = null;
        currentOutline = null;
        holdProgressSeconds = 0f;
        holdTriggered = false;
    }
    
    /// <summary>
    /// 상호작용을 수행하는 공용 메서드 - 시선이 향한 대상과 상호작용
    /// </summary>
    public void PerformInteraction()
    {
        // 쿨다운 체크
        if (Time.time - lastInteractionTime < interactionCooldown)
        {
            return;
        }
        
        lastInteractionTime = Time.time;
        
        if (currentInteractable != null)
        {
            currentInteractable.OnInteract(player);
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        // 상호작용 범위 시각화
        Gizmos.color = Color.yellow; 
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }

    private void ResolveInteractionCamera()
    {
        if (player == null)
        {
            return;
        }

        cameraTransform = player.CameraTransform;
        if (cameraTransform != null)
        {
            return;
        }
        // if (mainCamera != null && mainCamera.isActiveAndEnabled)
            // {
            //     return;
            // }

            // // 1) MainCamera 태그 우선
            // mainCamera = Camera.main;
            // if (mainCamera != null && mainCamera.isActiveAndEnabled)
            // {
            //     return;
            // }

            // 2) 플레이어 하위 카메라(프리팹 구조 변경 대응)
            if (player != null)
            {
                Camera playerCamera = player.GetComponentInChildren<Camera>(true);
                cameraTransform = playerCamera != null ? playerCamera.transform : null;
                if (cameraTransform != null && cameraTransform.gameObject.activeInHierarchy)
                {
                    return;
                }
            }

        // 3) 마지막 폴백: 씬의 활성 카메라 아무거나
        Camera fallbackCamera = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
        cameraTransform = fallbackCamera != null ? fallbackCamera.transform : null;
    }

    public void ConfigureForNetwork(bool isLocalPlayer)
    {
        networkConfigured = true;

        if (!isLocalPlayer)
        {
            ClearCurrentInteractable();
            enabled = false;
            return;
        }

        enabled = true;
        if (player == null)
        {
            player = GetComponentInParent<PlayerController>();
        }

        ResolveInteractionCamera();
    }
}
