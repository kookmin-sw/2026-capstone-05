using UnityEngine;

[DisallowMultipleComponent]
public class AttentionArrowMotion : MonoBehaviour
{
    public enum MotionAxis
    {
        Vertical,
        Horizontal
    }

    public enum VisibilityMode
    {
        AlwaysVisible,
        RoundStartUntilOutsideBunker,
        OutsideBunkerTimed
    }

    [Header("Motion")]
    public MotionAxis motionAxis = MotionAxis.Vertical;
    public bool useLocalAxis;

    [Min(0f)]
    public float amplitude = 0.25f;
    [Min(0f)]
    public float speed = 2f;
    public float startOffset;

    [Header("Round Start")]
    public bool destroyWhenRoundStarts;

    [Header("Visibility")]
    public VisibilityMode visibilityMode = VisibilityMode.AlwaysVisible;
    [Tooltip("Leave empty to toggle this arrow's child renderers.")]
    [SerializeField] private Transform visualRoot;
    [Min(0f)]
    [SerializeField] private float outsideBunkerDurationSeconds = 180f;

    private Vector3 startPosition;
    private Renderer[] visualRenderers;
    private bool[] rendererDefaultVisibility;
    private bool wasRoundRunning;
    private bool outsideBunkerTimerStarted;
    private float outsideBunkerVisibleUntil;
    private bool visualsAreVisible = true;

    private void Awake()
    {
        startPosition = transform.position;
        wasRoundRunning = IsRoundRunning();
        CacheVisualRenderers();
        ResetVisibilityState();
    }

    private void OnEnable()
    {
        startPosition = transform.position;
        wasRoundRunning = IsRoundRunning();
        ResetVisibilityState();
    }

    private void Update()
    {
        Vector3 axis = GetMotionAxis();
        float offset = Mathf.Sin((Time.time + startOffset) * speed) * amplitude;

        transform.position = startPosition + axis * offset;

        DestroyIfRoundJustStarted();
        UpdateVisibility();
    }

    private void OnValidate()
    {
        amplitude = Mathf.Max(0f, amplitude);
        speed = Mathf.Max(0f, speed);
        outsideBunkerDurationSeconds = Mathf.Max(0f, outsideBunkerDurationSeconds);
    }

    private Vector3 GetMotionAxis()
    {
        if (motionAxis == MotionAxis.Horizontal)
        {
            return useLocalAxis ? transform.right : Vector3.right;
        }

        return useLocalAxis ? transform.up : Vector3.up;
    }

    private void DestroyIfRoundJustStarted()
    {
        if (!destroyWhenRoundStarts)
        {
            return;
        }

        bool isRoundRunning = IsRoundRunning();
        if (isRoundRunning && !wasRoundRunning)
        {
            Destroy(gameObject);
            return;
        }

        wasRoundRunning = isRoundRunning;
    }

    private void ResetVisibilityState()
    {
        if (visualRenderers == null)
        {
            CacheVisualRenderers();
        }

        outsideBunkerTimerStarted = false;
        outsideBunkerVisibleUntil = 0f;
        UpdateVisibility();
    }

    private void UpdateVisibility()
    {
        bool shouldShow = visibilityMode switch
        {
            VisibilityMode.RoundStartUntilOutsideBunker => IsRoundRunning() && !IsLocalPlayerOutsideBunker(),
            VisibilityMode.OutsideBunkerTimed => ShouldShowOutsideBunkerTimedArrow(),
            _ => true
        };

        SetVisualsVisible(shouldShow);
    }

    private bool ShouldShowOutsideBunkerTimedArrow()
    {
        if (!outsideBunkerTimerStarted && IsLocalPlayerOutsideBunker())
        {
            outsideBunkerTimerStarted = true;
            outsideBunkerVisibleUntil = Time.time + outsideBunkerDurationSeconds;
        }

        return outsideBunkerTimerStarted && Time.time < outsideBunkerVisibleUntil;
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
}
