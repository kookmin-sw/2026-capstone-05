using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class RuntimeFieldFootprintAuthoring : MonoBehaviour
{
    [SerializeField] private Collider footprintCollider;

    public bool TryGetCollider(out Collider result)
    {
        result = footprintCollider != null ? footprintCollider : GetComponent<Collider>();
        return result != null;
    }

    private void OnValidate()
    {
        if (footprintCollider == null)
        {
            footprintCollider = GetComponent<Collider>();
        }

        if (footprintCollider != null)
        {
            footprintCollider.isTrigger = true;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!TryGetCollider(out Collider collider))
        {
            return;
        }

        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.35f);
        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.DrawCube(collider.bounds.center, collider.bounds.size);
        Gizmos.color = new Color(0.2f, 0.9f, 1f, 1f);
        Gizmos.DrawWireCube(collider.bounds.center, collider.bounds.size);
    }
}
