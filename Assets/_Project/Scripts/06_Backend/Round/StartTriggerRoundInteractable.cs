using Fusion;
using UnityEngine;

public class StartTriggerRoundInteractable : NetworkBehaviour, IInteractable, IHoldInteractable
{
    [Header("Round Trigger")]
    [SerializeField] private BackendRoundManager roundManager;
    [SerializeField] private float holdSeconds = 2f;
    [SerializeField] private float toggleDebounceSeconds = 0.75f;

    [Header("Interaction Assist")]
    [SerializeField] private bool autoCreateInteractionCollider = true;
    [SerializeField] private float assistColliderRadius = 3.5f;
    [SerializeField] private float maxInteractDistance = 4f;

    [Header("Focus Outline")]
    [SerializeField] private bool autoConfigureOutline = true;
    [SerializeField] private Outline.Mode outlineMode = Outline.Mode.OutlineAll;
    [SerializeField] private Color outlineColor = Color.white;
    [SerializeField] private float outlineWidth = 5f;

    private SphereCollider assistCollider;
    private Outline cachedOutline;
    private float lastToggleRequestTime = float.NegativeInfinity;

    public bool CanInteract(PlayerController player)
    {
        // roundManager가 늦게 바인딩되더라도 안내 UI는 먼저 표시되도록 허용한다.
        // 실제 라운드 토글은 OnHoldInteract에서 null 체크로 안전하게 처리한다.
        if (player == null || player.InputHandler == null)
        {
            return false;
        }

        return Vector3.Distance(player.transform.position, transform.position) <= Mathf.Max(assistColliderRadius, maxInteractDistance);
    }

    public void OnInteract(PlayerController player)
    {
        // 홀드 상호작용 전용: 즉시 상호작용은 사용하지 않음
    }

    public float GetHoldDuration(PlayerController player)
    {
        return holdSeconds;
    }

    public void OnHoldInteract(PlayerController player)
    {
        if (roundManager == null)
        {
            return;
        }

        if (Time.time - lastToggleRequestTime < toggleDebounceSeconds)
        {
            return;
        }

        PlayerRef requester = PlayerRef.None;
        NetworkObject playerNetworkObject = player != null ? player.GetComponent<NetworkObject>() : null;
        if (playerNetworkObject != null)
        {
            requester = playerNetworkObject.InputAuthority;
        }

        bool shouldStartRound = !roundManager.IsRoundRunning;
        roundManager.RpcRequestSetRoundState(requester, shouldStartRound);
        lastToggleRequestTime = Time.time;
    }

    public string GetInteractPrompt()
    {
        if (roundManager == null)
        {
            return "[Hold E] 준비 안됨";
        }

        return roundManager.IsRoundRunning
            ? "[Hold E] 게임 종료"
            : "[Hold E] 라운드 시작";
    }

    public string GetObjectName()
    {
        return "Start Trigger";
    }

    private void Awake()
    {
        if (roundManager == null)
        {
            roundManager = FindFirstObjectByType<BackendRoundManager>();
        }

        if (autoCreateInteractionCollider)
        {
            EnsureAssistCollider();
        }

        if (autoConfigureOutline)
        {
            EnsureOutlineComponent();
        }
    }

    private void OnValidate()
    {
        assistColliderRadius = Mathf.Max(0.5f, assistColliderRadius);
        maxInteractDistance = Mathf.Max(assistColliderRadius, maxInteractDistance);
        toggleDebounceSeconds = Mathf.Max(0.1f, toggleDebounceSeconds);

        if (assistCollider != null)
        {
            assistCollider.radius = assistColliderRadius;
        }

        outlineWidth = Mathf.Max(0f, outlineWidth);
        ApplyOutlineStyle();
    }

    private void EnsureAssistCollider()
    {
        assistCollider = GetComponent<SphereCollider>();
        if (assistCollider == null)
        {
            assistCollider = gameObject.AddComponent<SphereCollider>();
        }

        assistCollider.isTrigger = true;
        assistCollider.radius = assistColliderRadius;
    }

    private void EnsureOutlineComponent()
    {
        cachedOutline = GetComponent<Outline>();
        if (cachedOutline == null)
        {
            cachedOutline = gameObject.AddComponent<Outline>();
        }

        ApplyOutlineStyle();
        cachedOutline.enabled = false;
    }

    private void ApplyOutlineStyle()
    {
        if (cachedOutline == null)
        {
            return;
        }

        cachedOutline.OutlineMode = outlineMode;
        cachedOutline.OutlineColor = outlineColor;
        cachedOutline.OutlineWidth = outlineWidth;
    }
}
