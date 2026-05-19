using Fusion;
using UnityEngine;
using UnityEngine.Localization.Settings;

public class StartTriggerRoundInteractable : MonoBehaviour, IInteractable, IHoldInteractable
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

    private readonly string objectNameTable = "ObjectNames";
    private readonly string objectNameKey = "StartRound";
    private readonly string interactPromptTable = "InteractPrompts";
    private readonly string notReadyPromptKey = "RoundNotReady";
    private readonly string readyPromptKey = "RoundStart";
    private readonly string inProgressPromptKey = "RoundInProgress";

    public bool CanInteract(PlayerController player)
    {
        if (player == null || player.InputHandler == null)
        {
            return false;
        }

        return Vector3.Distance(player.transform.position, transform.position) <= Mathf.Max(assistColliderRadius, maxInteractDistance);
    }

    public void OnInteract(PlayerController player)
    {
    }

    public float GetHoldDuration(PlayerController player)
    {
        return holdSeconds;
    }

    public void OnHoldInteract(PlayerController player)
    {
        BackendRoundManager activeRoundManager = ResolveRoundManager();
        if (activeRoundManager == null)
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

        if (!activeRoundManager.IsRoundRunning)
        {
            if (activeRoundManager.Object != null && activeRoundManager.Object.IsValid) {
                activeRoundManager.RpcRequestSetRoundState(requester, true);
            } else if (PlayerNetworkSetup.IsOfflineTestMode) {
                // Offline fallback - we can't call RPCs, so we just log or handle it locally if needed
                Debug.Log("[StartTriggerRoundInteractable] Offline mode: Pretending to start round locally.");
                // In offline mode, BackendRoundManager doesn't have a Runner to start a TickTimer.
                // We could implement a local fallback in BackendRoundManager, but for now we just skip the RPC.
                activeRoundManager.StartRoundOfflineFallback();
            }
        }

        lastToggleRequestTime = Time.time;
    }

    public string GetInteractPrompt()
    {
        BackendRoundManager activeRoundManager = ResolveRoundManager();
        if (activeRoundManager == null)
        {
            return LocalizationSettings.StringDatabase.GetLocalizedString(interactPromptTable, notReadyPromptKey);
        }

        return activeRoundManager.IsRoundRunning
            ? LocalizationSettings.StringDatabase.GetLocalizedString(interactPromptTable, inProgressPromptKey)
            : LocalizationSettings.StringDatabase.GetLocalizedString(interactPromptTable, readyPromptKey);
    }

    public string GetObjectName()
    {
        return LocalizationSettings.StringDatabase.GetLocalizedString(objectNameTable, objectNameKey);
    }

    private void Awake()
    {
        ResolveRoundManager();

        if (autoCreateInteractionCollider)
        {
            EnsureAssistCollider();
        }

        if (autoConfigureOutline)
        {
            EnsureOutlineComponent();
        }
    }

    private BackendRoundManager ResolveRoundManager()
    {
        if (BackendRoundManager.Instance != null)
        {
            roundManager = BackendRoundManager.Instance;
            return roundManager;
        }

        if (roundManager == null)
        {
            roundManager = FindFirstObjectByType<BackendRoundManager>();
        }

        return roundManager;
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
