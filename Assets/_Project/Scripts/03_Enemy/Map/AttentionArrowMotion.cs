using UnityEngine;

[DisallowMultipleComponent]
public class AttentionArrowMotion : MonoBehaviour
{
    private const int LegacyReverseDirectionVisibilityModeValue = 3;

    public enum MotionAxis
    {
        Vertical,
        Horizontal
    }

    public enum VisibilityMode
    {
        AlwaysVisible = 0,
        RoundStartUntilOutsideBunker = 1,
        VisibleUntilRoundStarts = 2
    }

    public enum DirectionMode
    {
        KeepDirection = 0,
        ReverseAfterQuest3 = 1
    }

    [Header("Motion")]
    public MotionAxis motionAxis = MotionAxis.Vertical;
    public bool useLocalAxis;

    [Min(0f)]
    public float amplitude = 0.25f;
    [Min(0f)]
    public float speed = 2f;
    public float startOffset;

    [Header("Visibility")]
    public VisibilityMode visibilityMode = VisibilityMode.AlwaysVisible;
    [Tooltip("Leave empty to toggle this arrow's child renderers.")]
    [SerializeField] private Transform visualRoot;

    [Header("Direction")]
    [SerializeField] private DirectionMode directionMode = DirectionMode.KeepDirection;
    [Tooltip("Leave empty to rotate the GameObject that has this script.")]
    [SerializeField] private Transform rotationRoot;
    [SerializeField] private Vector3 quest3CompletedRotationOffset = new Vector3(0f, 180f, 0f);
    [SerializeField, HideInInspector] private bool destroyWhenRoundStarts;

    private Vector3 startPosition;
    private Vector3 startLocalEulerAngles;
    private Renderer[] visualRenderers;
    private bool[] rendererDefaultVisibility;
    private bool wasRoundRunning;
    private int observedRoundNumber = -1;
    private bool isDirectionReversed;
    private bool hasCachedStartRotation;
    private bool visualsAreVisible = true;

    private void Awake()
    {
        startPosition = transform.position;
        MigrateLegacyDestroyMode();
        CaptureRoundState();
        CacheVisualRenderers();
        CacheDirectionRotation(force: true);
        ResetVisibilityState();
    }

    private void OnEnable()
    {
        startPosition = transform.position;
        MigrateLegacyDestroyMode();
        DemoRoundMissionHUD.Quest4ActiveChanged -= HandleQuest4ActiveChanged;
        DemoRoundMissionHUD.Quest4ActiveChanged += HandleQuest4ActiveChanged;
        CaptureRoundState();
        if (!hasCachedStartRotation)
        {
            CacheDirectionRotation(force: true);
        }

        SetDirectionReversed(false, force: true);
        ApplyQuest4DirectionState();
        ResetVisibilityState();
    }

    private void OnDisable()
    {
        DemoRoundMissionHUD.Quest4ActiveChanged -= HandleQuest4ActiveChanged;
    }

    private void Update()
    {
        Vector3 axis = GetMotionAxis();
        float offset = Mathf.Sin((Time.time + startOffset) * speed) * amplitude;

        transform.position = startPosition + axis * offset;

        RefreshRoundState();
        UpdateVisibility();
        UpdateDirection();
    }

    private void OnValidate()
    {
        amplitude = Mathf.Max(0f, amplitude);
        speed = Mathf.Max(0f, speed);
        MigrateLegacyDestroyMode();
    }

    private Vector3 GetMotionAxis()
    {
        if (motionAxis == MotionAxis.Horizontal)
        {
            return useLocalAxis ? transform.right : Vector3.right;
        }

        return useLocalAxis ? transform.up : Vector3.up;
    }

    private void ResetVisibilityState()
    {
        if (visualRenderers == null)
        {
            CacheVisualRenderers();
        }

        UpdateVisibility();
    }

    private void UpdateVisibility()
    {
        bool shouldShow = visibilityMode switch
        {
            VisibilityMode.RoundStartUntilOutsideBunker => IsRoundRunning() && !IsLocalPlayerOutsideBunker(),
            VisibilityMode.VisibleUntilRoundStarts => !IsRoundRunning(),
            _ => true
        };

        SetVisualsVisible(shouldShow);
    }

    private void UpdateDirection()
    {
        bool shouldReverse = directionMode == DirectionMode.ReverseAfterQuest3 && IsLocalPlayerQuest3Completed();
        SetDirectionReversed(shouldReverse);
    }

    private void CaptureRoundState()
    {
        BackendRoundManager roundManager = BackendRoundManager.Instance;
        wasRoundRunning = roundManager != null && roundManager.IsRoundRunning;
        observedRoundNumber = roundManager != null ? roundManager.CurrentRoundNumber : -1;
    }

    private void RefreshRoundState()
    {
        BackendRoundManager roundManager = BackendRoundManager.Instance;
        bool isRoundRunning = roundManager != null && roundManager.IsRoundRunning;
        int roundNumber = roundManager != null ? roundManager.CurrentRoundNumber : -1;

        if (isRoundRunning == wasRoundRunning && roundNumber == observedRoundNumber)
        {
            return;
        }

        wasRoundRunning = isRoundRunning;
        observedRoundNumber = roundNumber;
        ResetVisibilityState();
        UpdateDirection();
    }

    private void MigrateLegacyDestroyMode()
    {
        if ((int)visibilityMode == LegacyReverseDirectionVisibilityModeValue)
        {
            directionMode = DirectionMode.ReverseAfterQuest3;
            visibilityMode = VisibilityMode.AlwaysVisible;
        }

        if (!destroyWhenRoundStarts)
        {
            return;
        }

        visibilityMode = VisibilityMode.VisibleUntilRoundStarts;
        destroyWhenRoundStarts = false;
    }

    private void CacheDirectionRotation(bool force)
    {
        if (hasCachedStartRotation && !force)
        {
            return;
        }

        startLocalEulerAngles = DirectionTransform.localEulerAngles;
        hasCachedStartRotation = true;
        isDirectionReversed = false;
    }

    private void CacheVisualRenderers()
    {
        Transform rendererRoot = visualRoot != null ? visualRoot : transform;
        visualRenderers = rendererRoot.GetComponentsInChildren<Renderer>(true);
        rendererDefaultVisibility = new bool[visualRenderers.Length];

        for (int i = 0; i < visualRenderers.Length; i++)
        {
            rendererDefaultVisibility[i] = visualRenderers[i] != null && visualRenderers[i].enabled;
        }
    }

    private void SetVisualsVisible(bool visible)
    {
        if (visualRenderers == null || visualsAreVisible == visible)
        {
            return;
        }

        visualsAreVisible = visible;
        for (int i = 0; i < visualRenderers.Length; i++)
        {
            Renderer renderer = visualRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            renderer.enabled = visible && rendererDefaultVisibility[i];
        }
    }

    private void SetDirectionReversed(bool reversed, bool force = false)
    {
        if (!force && isDirectionReversed == reversed)
        {
            return;
        }

        isDirectionReversed = reversed;
        DirectionTransform.localEulerAngles = reversed
            ? startLocalEulerAngles + quest3CompletedRotationOffset
            : startLocalEulerAngles;
    }

    private void HandleQuest4ActiveChanged(bool isQuest4Active)
    {
        if (directionMode != DirectionMode.ReverseAfterQuest3)
        {
            return;
        }

        SetDirectionReversed(isQuest4Active, force: true);
    }

    private void ApplyQuest4DirectionState()
    {
        if (directionMode != DirectionMode.ReverseAfterQuest3)
        {
            return;
        }

        SetDirectionReversed(IsLocalPlayerQuest3Completed(), force: true);
    }

    private Transform DirectionTransform => rotationRoot != null ? rotationRoot : transform;

    private static bool IsLocalPlayerOutsideBunker()
    {
        return LocalPlayerReferenceResolver.TryGetLocalCondition(out PlayerCondition condition)
            && condition != null
            && !condition.IsInBunker;
    }

    private static bool IsRoundRunning()
    {
        return BackendRoundManager.Instance != null && BackendRoundManager.Instance.IsRoundRunning;
    }

    private static bool IsLocalPlayerQuest3Completed()
    {
        if (DemoRoundMissionHUD.IsQuest4ActiveForLocalPlayer)
        {
            return true;
        }

        return BackendRoundManager.Instance != null && BackendRoundManager.Instance.IsLocalPlayerQuest3Completed();
    }
}
