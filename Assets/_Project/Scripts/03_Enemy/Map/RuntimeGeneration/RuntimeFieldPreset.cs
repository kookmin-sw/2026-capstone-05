using UnityEngine;

public class RuntimeFieldPreset : ScriptableObject
{
    private static readonly string[] FootprintObjectNames =
    {
        "RuntimeFieldFootprint",
        "FieldFootprint",
        "FlattenFootprint"
    };

    [Header("Prefab")]
    public GameObject prefab;

    [Header("Director")]
    public RuntimeFieldCategory category = RuntimeFieldCategory.House;
    [Range(0, 3)] public int difficultyTier;
    [HideInInspector, Min(0f)] public float directorWeight = 1f;
    [HideInInspector]
    public Vector2 preferredDistanceRange01 = new Vector2(0f, 1f);
    [HideInInspector, Min(0f)] public float maxFlattenCostOverride;

    [Header("Flattening")]
    [Min(0f)] public float flattenBlendWidth = 8f;

    [Header("Variation")]
    public bool randomYaw = true;
    public float fixedYawDegrees;
    public Vector2 randomScaleRange = Vector2.one;
    [HideInInspector] public bool lockYawWhenLarge = true;
    [HideInInspector, Min(1f)] public float largeFootprintYawLockThreshold = 50f;
    [HideInInspector] public Vector2 yawRange = new Vector2(0f, 360f);

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
        directorWeight = Mathf.Max(0f, directorWeight);
        difficultyTier = Mathf.Clamp(difficultyTier, 0, 3);
        preferredDistanceRange01.x = Mathf.Clamp01(preferredDistanceRange01.x);
        preferredDistanceRange01.y = Mathf.Clamp01(preferredDistanceRange01.y);
        if (preferredDistanceRange01.y < preferredDistanceRange01.x)
        {
            preferredDistanceRange01.y = preferredDistanceRange01.x;
        }

        maxFlattenCostOverride = Mathf.Max(0f, maxFlattenCostOverride);
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

        if (TryCalculateAuthoredFootprint(sourcePrefab, out RuntimeFarmFieldFootprintData authoredFootprint))
        {
            footprintData = authoredFootprint;
            return IsValidFootprint(footprintData);
        }

        if (TryCalculateNamedFootprintBounds(sourcePrefab, out Bounds namedBounds))
        {
            footprintData = CreateFootprintData(namedBounds, 0f, namedBounds.max.y);
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
        RuntimeFieldFootprintPartData part = new RuntimeFieldFootprintPartData(
            new Vector2(bounds.size.x + padding * 2f, bounds.size.z + padding * 2f),
            new Vector2(bounds.center.x, bounds.center.z),
            0f,
            groundLocalY);

        return new RuntimeFarmFieldFootprintData(
            new Vector2(bounds.size.x + padding * 2f, bounds.size.z + padding * 2f),
            new Vector2(bounds.center.x, bounds.center.z),
            groundLocalY,
            new[] { part });
    }

    private static bool IsValidFootprint(RuntimeFarmFieldFootprintData footprintData)
    {
        return footprintData.size.x > 0f && footprintData.size.y > 0f;
    }

    private static bool TryCalculateAuthoredFootprint(GameObject sourcePrefab, out RuntimeFarmFieldFootprintData footprintData)
    {
        footprintData = default;
        bool hasBounds = false;
        Bounds aggregateBounds = new Bounds();
        RuntimeFieldFootprintAuthoring[] footprints = sourcePrefab.GetComponentsInChildren<RuntimeFieldFootprintAuthoring>(true);
        RuntimeFieldFootprintPartData[] parts = new RuntimeFieldFootprintPartData[footprints.Length];
        int partCount = 0;

        for (int i = 0; i < footprints.Length; i++)
        {
            RuntimeFieldFootprintAuthoring footprint = footprints[i];
            if (footprint == null || !footprint.TryGetCollider(out Collider footprintCollider))
            {
                continue;
            }

            RuntimeFieldFootprintPartData part = CalculateFootprintPartInRootSpace(sourcePrefab.transform, footprintCollider);
            if (part.size.x <= 0f || part.size.y <= 0f)
            {
                continue;
            }

            Bounds localBounds = CalculateColliderBoundsInRootSpace(sourcePrefab.transform, footprintCollider);
            EncapsulateBounds(localBounds, ref aggregateBounds, ref hasBounds);
            parts[partCount] = part;
            partCount++;
        }

        if (!hasBounds || partCount == 0)
        {
            return false;
        }

        System.Array.Resize(ref parts, partCount);
        footprintData = new RuntimeFarmFieldFootprintData(
            new Vector2(aggregateBounds.size.x, aggregateBounds.size.z),
            new Vector2(aggregateBounds.center.x, aggregateBounds.center.z),
            aggregateBounds.max.y,
            parts);
        return true;
    }

    private static bool TryCalculateNamedFootprintBounds(GameObject sourcePrefab, out Bounds bounds)
    {
        bounds = new Bounds();
        bool hasBounds = false;

        for (int i = 0; i < FootprintObjectNames.Length; i++)
        {
            Transform footprintTransform = FindDeepChildByExactName(sourcePrefab.transform, FootprintObjectNames[i]);
            if (footprintTransform == null)
            {
                continue;
            }

            Collider footprintCollider = footprintTransform.GetComponent<Collider>();
            if (footprintCollider == null)
            {
                continue;
            }

            Bounds localBounds = CalculateColliderBoundsInRootSpace(sourcePrefab.transform, footprintCollider);
            EncapsulateBounds(localBounds, ref bounds, ref hasBounds);
        }

        return hasBounds;
    }

    private static Transform FindDeepChildByExactName(Transform root, string targetName)
    {
        if (root == null)
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == targetName)
            {
                return child;
            }

            Transform nested = FindDeepChildByExactName(child, targetName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
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
            EncapsulateBounds(CalculateColliderBoundsInRootSpace(sourcePrefab.transform, colliders[i]), ref bounds, ref hasBounds);
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

    private static Bounds CalculateColliderBoundsInRootSpace(Transform root, Collider collider)
    {
        if (collider is BoxCollider box)
        {
            return CalculateLocalBoxBoundsInRootSpace(root, box.transform, box.center, box.size);
        }

        if (collider is SphereCollider sphere)
        {
            Vector3 size = Vector3.one * sphere.radius * 2f;
            return CalculateLocalBoxBoundsInRootSpace(root, sphere.transform, sphere.center, size);
        }

        if (collider is CapsuleCollider capsule)
        {
            Vector3 size = Vector3.one * capsule.radius * 2f;
            size[capsule.direction] = capsule.height;
            return CalculateLocalBoxBoundsInRootSpace(root, capsule.transform, capsule.center, size);
        }

        if (collider is MeshCollider meshCollider && meshCollider.sharedMesh != null)
        {
            Bounds meshBounds = meshCollider.sharedMesh.bounds;
            return CalculateLocalBoxBoundsInRootSpace(root, meshCollider.transform, meshBounds.center, meshBounds.size);
        }

        return CalculateLocalBoundsInRootSpace(root, collider.bounds);
    }

    private static RuntimeFieldFootprintPartData CalculateFootprintPartInRootSpace(Transform root, Collider collider)
    {
        Bounds bounds = CalculateColliderBoundsInRootSpace(root, collider);

        if (collider is BoxCollider box)
        {
            Matrix4x4 localToRoot = root.worldToLocalMatrix * box.transform.localToWorldMatrix;
            Vector3 center = localToRoot.MultiplyPoint3x4(box.center);
            Vector3 right = localToRoot.MultiplyVector(Vector3.right * box.size.x);
            Vector3 forward = localToRoot.MultiplyVector(Vector3.forward * box.size.z);
            Vector2 rightXZ = new Vector2(right.x, right.z);
            Vector2 forwardXZ = new Vector2(forward.x, forward.z);
            float yaw = rightXZ.sqrMagnitude > 0.0001f
                ? Mathf.Atan2(-rightXZ.y, rightXZ.x) * Mathf.Rad2Deg
                : 0f;

            return new RuntimeFieldFootprintPartData(
                new Vector2(rightXZ.magnitude, forwardXZ.magnitude),
                new Vector2(center.x, center.z),
                yaw,
                bounds.max.y);
        }

        return new RuntimeFieldFootprintPartData(
            new Vector2(bounds.size.x, bounds.size.z),
            new Vector2(bounds.center.x, bounds.center.z),
            0f,
            bounds.max.y);
    }

    private static Bounds CalculateLocalBoxBoundsInRootSpace(
        Transform root,
        Transform boxTransform,
        Vector3 localCenter,
        Vector3 localSize)
    {
        Matrix4x4 localToRoot = root.worldToLocalMatrix * boxTransform.localToWorldMatrix;
        Vector3 extents = localSize * 0.5f;
        Bounds bounds = new Bounds();
        bool hasBounds = false;

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 corner = localCenter + Vector3.Scale(extents, new Vector3(x, y, z));
                    Vector3 rootCorner = localToRoot.MultiplyPoint3x4(corner);

                    if (!hasBounds)
                    {
                        bounds = new Bounds(rootCorner, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(rootCorner);
                    }
                }
            }
        }

        return bounds;
    }
}
