using UnityEngine;

[DisallowMultipleComponent]
public class AttentionArrowMotion : MonoBehaviour
{
    public enum MotionAxis
    {
        Vertical,
        Horizontal
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

    private Vector3 startPosition;
    private bool wasRoundRunning;

    private void Awake()
    {
        startPosition = transform.position;
        wasRoundRunning = IsRoundRunning();
    }

    private void OnEnable()
    {
        startPosition = transform.position;
        wasRoundRunning = IsRoundRunning();
    }

    private void Update()
    {
        Vector3 axis = GetMotionAxis();
        float offset = Mathf.Sin((Time.time + startOffset) * speed) * amplitude;

        transform.position = startPosition + axis * offset;

        DestroyIfRoundJustStarted();
    }

    private void OnValidate()
    {
        amplitude = Mathf.Max(0f, amplitude);
        speed = Mathf.Max(0f, speed);
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

    private static bool IsRoundRunning()
    {
        return BackendRoundManager.Instance != null && BackendRoundManager.Instance.IsRoundRunning;
    }
}
