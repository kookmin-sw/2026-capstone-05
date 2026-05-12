using UnityEngine;

[DisallowMultipleComponent]
public sealed class RuntimeFieldAccessPointAuthoring : MonoBehaviour
{
    [SerializeField, Min(0f)] private float allowedVerticalDifference = 0.75f;
    [SerializeField, Range(0f, 90f)] private float maxSlopeDegrees = 35f;

    public float AllowedVerticalDifference => allowedVerticalDifference;
    public float MaxSlopeDegrees => maxSlopeDegrees;

    private void OnValidate()
    {
        allowedVerticalDifference = Mathf.Max(0f, allowedVerticalDifference);
        maxSlopeDegrees = Mathf.Clamp(maxSlopeDegrees, 0f, 90f);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.1f, 1f, 0.35f, 0.9f);
        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.5f);
    }
}
