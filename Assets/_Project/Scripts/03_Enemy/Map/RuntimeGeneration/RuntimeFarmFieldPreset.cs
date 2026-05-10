using UnityEngine;

[CreateAssetMenu(menuName = "_Project/Map/Runtime Farm Field Preset")]
public class RuntimeFarmFieldPreset : ScriptableObject
{
    private const float FlatTriggerThickness = 1f;
    private const float FlatTriggerRelativeThickness = 0.2f;
    private const float FlatTriggerGroundBand = 0.5f;

    [Header("Prefab")]
    public GameObject prefab;

    [Header("Flattening")]
    [Min(0f)] public float flattenBlendWidth = 8f;

    [Header("Variation")]
    public bool randomYaw = true;
    public bool lockYawWhenLarge = true;
    [Min(1f)] public float largeFootprintYawLockThreshold = 50f;
    public float fixedYawDegrees;
    public Vector2 yawRange = new Vector2(0f, 360f);
    public Vector2 randomScaleRange = Vector2.one;

    [HideInInspector, Min(0f)] public float footprintPadding = 2f;

    [HideInInspector] public float minWorldHeight = 0f;
    [HideInInspector] public float maxWorldHeight = 180f;
    [HideInInspector] public bool useSlopeFilter = false;
    [HideInInspector, Range(0f, 90f)] public float maxSlopeDegrees = 12f;
    [HideInInspector] public bool useHeightDeltaFilter = false;
    [HideInInspector, Min(0f)] public float maxHeightDelta = 2f;
    [HideInInspector, Range(2, 9)] public int sampleGrid = 5;

    public bool TryGetRuntimeFootprint(out RuntimeFarmFieldFootprintData footprintData)
    {
        return TryCalculatePrefabFootprint(prefab, footprintPadding, out footprintData);
    }

    private void OnValidate()
    {
        footprintPadding = Mathf.Max(0f, footprintPadding);
        maxWorldHeight = Mathf.Max(minWorldHeight, maxWorldHeight);
        maxHeightDelta = Mathf.Max(0f, maxHeightDelta);
        flattenBlendWidth = Mathf.Max(0f, flattenBlendWidth);
        sampleGrid = Mathf.Clamp(sampleGrid, 2, 9);
        largeFootprintYawLockThreshold = Mathf.Max(1f, largeFootprintYawLockThreshold);
        fixedYawDegrees = Mathf.Repeat(fixedYawDegrees, 360f);

        if (randomScaleRange.x <= 0f)
        {
            randomScaleRange.x = 1f;
        }

        if (randomScaleRange.y <= 0f)
        {
            randomScaleRange.y = randomScaleRange.x;
        }

        if (randomScaleRange.y < randomScaleRange.x)
        {
            randomScaleRange.y = randomScaleRange.x;
        }
    }

    public static bool TryCalculatePrefabFootprint(
        GameObject sourcePrefab,
        float padding,
        out RuntimeFarmFieldFootprintData footprintData)
    {
        footprintData = default;

        if (sourcePrefab == null)
        {
            return false;
        }

        if (TryCalculateNamedFootprintBounds(sourcePrefab, out Bounds markedBounds))
        {
            footprintData = CreateFootprintData(markedBounds, 0f, markedBounds.max.y);
            return IsValidFootprint(footprintData);
        }

        if (TryCalculateBestTriggerFootprintBounds(sourcePrefab, out Bounds triggerFootprintBounds))
        {
            footprintData = CreateFootprintData(triggerFootprintBounds, 0f, triggerFootprintBounds.max.y);
            return IsValidFootprint(footprintData);
        }

        if (!TryCalculateAggregateLocalBoundsFromRenderers(sourcePrefab, out Bounds localBounds) &&
            !TryCalculateAggregateLocalBoundsFromColliders(sourcePrefab, out localBounds))
        {
            return false;
        }

        footprintData = CreateFootprintData(
            localBounds,
            padding,
            CalculateGroundLocalY(sourcePrefab, localBounds.min.y));
        return IsValidFootprint(footprintData);
    }

    private static RuntimeFarmFieldFootprintData CreateFootprintData(Bounds bounds, float padding, float groundLocalY)
    {
        return new RuntimeFarmFieldFootprintData(
            new Vector2(bounds.size.x + padding * 2f, bounds.size.z + padding * 2f),
            new Vector2(bounds.center.x, bounds.center.z),
            groundLocalY);
    }

    private static bool IsValidFootprint(RuntimeFarmFieldFootprintData footprintData)
    {
        return footprintData.size.x > 0f && footprintData.size.y > 0f;
    }

    private static bool TryCalculateNamedFootprintBounds(GameObject sourcePrefab, out Bounds bounds)
    {
        bounds = new Bounds();
        bool hasBounds = false;
        Collider[] colliders = sourcePrefab.GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.isTrigger || !HasFootprintName(collider.transform, sourcePrefab.transform))
            {
                continue;
            }

            Bounds localBounds = CalculateLocalBoundsInRootSpace(sourcePrefab.transform, collider.bounds);
            EncapsulateBounds(localBounds, ref bounds, ref hasBounds);
        }

        return hasBounds;
    }

    private static bool HasFootprintName(Transform current, Transform root)
    {
        while (current != null)
        {
            string objectName = current.name.ToLowerInvariant();
            if (objectName.Contains("footprint") || objectName.Contains("flatten"))
            {
                return true;
            }

            if (current == root)
            {
                break;
            }

            current = current.parent;
        }

        return false;
    }

    private static bool TryCalculateBestTriggerFootprintBounds(GameObject sourcePrefab, out Bounds bounds)
    {
        bounds = new Bounds();
        Collider[] colliders = sourcePrefab.GetComponentsInChildren<Collider>(true);
        bool hasFlatTrigger = false;
        float floorTopY = float.MaxValue;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.isTrigger)
            {
                continue;
            }

            Bounds localBounds = CalculateLocalBoundsInRootSpace(sourcePrefab.transform, collider.bounds);
            if (!LooksLikeFlatFootprint(localBounds))
            {
                continue;
            }

            hasFlatTrigger = true;
            floorTopY = Mathf.Min(floorTopY, localBounds.max.y);
        }

        if (hasFlatTrigger)
        {
            bool hasBounds = false;
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null || !collider.isTrigger)
                {
                    continue;
                }

                Bounds localBounds = CalculateLocalBoundsInRootSpace(sourcePrefab.transform, collider.bounds);
                if (!LooksLikeFlatFootprint(localBounds) || Mathf.Abs(localBounds.max.y - floorTopY) > FlatTriggerGroundBand)
                {
                    continue;
                }

                EncapsulateBounds(localBounds, ref bounds, ref hasBounds);
            }

            return hasBounds;
        }

        bool hasBest = false;
        float bestArea = 0f;
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.isTrigger)
            {
                continue;
            }

            Bounds localBounds = CalculateLocalBoundsInRootSpace(sourcePrefab.transform, collider.bounds);
            float area = Mathf.Max(0f, localBounds.size.x) * Mathf.Max(0f, localBounds.size.z);
            if (area <= bestArea)
            {
                continue;
            }

            bounds = localBounds;
            bestArea = area;
            hasBest = true;
        }

        return hasBest;
    }

    private static bool LooksLikeFlatFootprint(Bounds bounds)
    {
        float horizontal = Mathf.Max(bounds.size.x, bounds.size.z);
        if (horizontal <= 0f)
        {
            return false;
        }

        float allowedThickness = Mathf.Max(FlatTriggerThickness, horizontal * FlatTriggerRelativeThickness);
        return bounds.size.y <= allowedThickness;
    }

    private static float CalculateGroundLocalY(GameObject sourcePrefab, float fallback)
    {
        if (TryCalculateAggregateLocalBoundsFromRenderers(sourcePrefab, out Bounds rendererBounds))
        {
            return rendererBounds.min.y;
        }

        if (TryCalculateAggregateLocalBoundsFromColliders(sourcePrefab, out Bounds colliderBounds))
        {
            return colliderBounds.min.y;
        }

        return fallback;
    }

    private static bool TryCalculateAggregateLocalBoundsFromRenderers(GameObject sourcePrefab, out Bounds bounds)
    {
        bounds = new Bounds();
        bool hasBounds = false;
        Renderer[] renderers = sourcePrefab.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            EncapsulateBounds(CalculateLocalBoundsInRootSpace(sourcePrefab.transform, renderers[i].bounds), ref bounds, ref hasBounds);
        }

        return hasBounds;
    }

    private static bool TryCalculateAggregateLocalBoundsFromColliders(GameObject sourcePrefab, out Bounds bounds)
    {
        bounds = new Bounds();
        bool hasBounds = false;
        Collider[] colliders = sourcePrefab.GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < colliders.Length; i++)
        {
            EncapsulateBounds(CalculateLocalBoundsInRootSpace(sourcePrefab.transform, colliders[i].bounds), ref bounds, ref hasBounds);
        }

        return hasBounds;
    }

    private static void EncapsulateBounds(Bounds source, ref Bounds aggregate, ref bool hasAggregate)
    {
        if (!hasAggregate)
        {
            aggregate = source;
            hasAggregate = true;
            return;
        }

        aggregate.Encapsulate(source);
    }

    private static Bounds CalculateLocalBoundsInRootSpace(
        Transform root,
        Bounds worldBounds)
    {
        Matrix4x4 worldToRoot = root.worldToLocalMatrix;
        Vector3 center = worldBounds.center;
        Vector3 extents = worldBounds.extents;
        Bounds localBounds = new Bounds();
        bool hasBounds = false;

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 corner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                    Vector3 localCorner = worldToRoot.MultiplyPoint3x4(corner);

                    if (!hasBounds)
                    {
                        localBounds = new Bounds(localCorner, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        localBounds.Encapsulate(localCorner);
                    }
                }
            }
        }

        return localBounds;
    }
}
