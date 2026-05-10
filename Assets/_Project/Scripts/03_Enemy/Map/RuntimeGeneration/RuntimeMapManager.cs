using System.Collections;
using System.Collections.Generic;
using MapMagic.Core;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.Events;

public class RuntimeMapManager : MonoBehaviour
{
    [Header("MapMagic")]
    [SerializeField] private MapMagicObject mapMagic;
    [SerializeField] private bool generateOnStart;
    [SerializeField] private bool randomizeStartSeed = true;
    [SerializeField] private int initialSeed = 12345;
    [SerializeField] private float mapMagicWaitTimeout = 60f;

    [Header("Terrains")]
    [SerializeField] private Terrain[] explicitTerrains;

    [Header("Runtime Objects")]
    [SerializeField] private Transform runtimeRoot;
    [SerializeField] private string runtimeRootName = "Runtime Generated Map";

    [Header("Performance")]
    [SerializeField, Min(0.25f)] private float frameBudgetMs = 2f;
    [SerializeField, Min(1)] private int objectSpawnsPerFrame = 8;
    [SerializeField] private bool useDelayedHeightmapLod = true;

    [Header("Farm Fields")]
    [SerializeField] private RuntimeFieldPreset[] farmFieldPresets;
    [SerializeField, Min(0)] private int farmFieldCount = 6;
    [SerializeField, Min(1)] private int fieldPlacementAttempts = 300;
    [SerializeField, Min(0f)] private float fieldReservationPadding = 4f;
    [SerializeField] private bool spawnFarmFields = true;
    [SerializeField] private bool useFarmFieldSlopeSafetyFilter = true;
    [SerializeField, Range(0f, 90f)] private float maxFarmFieldAverageSlopeDegrees = 18f;
    [SerializeField, Range(0f, 90f)] private float maxFarmFieldPeakSlopeDegrees = 32f;
    [SerializeField, Range(0f, 90f)] private float maxFarmFieldOverallGradeDegrees = 14f;
    [SerializeField, Min(0f)] private float maxFarmFieldHeightDelta = 8f;

    [Header("Terrain Modification")]
    [SerializeField] private bool flattenTerrainForFields = true;
    [SerializeField] private bool clearDetailsUnderFields = true;
    [SerializeField, Min(0f)] private float detailClearPadding = 2f;
    [SerializeField] private bool clearTerrainTreesUnderFields = true;

    private enum TreeSpawnMode
    {
        TerrainTreeInstances,
        GameObjects
    }

    [Header("Tree Scatter")]
    [SerializeField] private bool spawnTrees = true;
    [SerializeField] private TreeSpawnMode treeSpawnMode = TreeSpawnMode.TerrainTreeInstances;
    [SerializeField] private GameObject[] treePrefabs;
    [SerializeField] private bool clearOwnedTerrainTreesBeforeScatter = true;
    [SerializeField, Min(0)] private int targetTreeCount = 300;
    [SerializeField, Min(1)] private int treePlacementAttempts = 6000;
    [SerializeField, Min(0f)] private float minTreeDistance = 6f;
    [SerializeField, Min(0f)] private float treeFieldPadding = 6f;
    [SerializeField] private float minTreeWorldHeight = 0f;
    [SerializeField] private float maxTreeWorldHeight = 220f;
    [SerializeField, Range(0f, 90f)] private float maxTreeSlopeDegrees = 30f;
    [SerializeField] private Vector2 treeScaleRange = new Vector2(0.85f, 1.25f);
    [SerializeField] private bool alignTreesToTerrainNormal;
    [SerializeField] private float treeYOffset;

    [Header("Navigation")]
    [SerializeField] private bool rebuildNavMeshAfterGeneration;
    [SerializeField] private NavMeshSurface[] navMeshSurfaces;

    [Header("Events")]
    public UnityEvent<int> OnGenerationStarted;
    public UnityEvent<int> OnGenerationCompleted;

    [SerializeField, HideInInspector] private int currentSeed;
    [SerializeField, HideInInspector] private List<PlannedFarmField> lastPlannedFields = new List<PlannedFarmField>();

    private readonly RuntimeTerrainReservationMask reservationMask = new RuntimeTerrainReservationMask();
    private Coroutine regenerationCoroutine;
    private bool isRegenerating;

    public int CurrentSeed => currentSeed;
    public bool IsRegenerating => isRegenerating;
    public IReadOnlyList<PlannedFarmField> LastPlannedFields => lastPlannedFields;

    private void Start()
    {
        if (!generateOnStart)
        {
            return;
        }

        int seed = randomizeStartSeed ? Random.Range(int.MinValue, int.MaxValue) : initialSeed;
        Regenerate(seed);
    }

    [ContextMenu("Regenerate With Random Seed")]
    public void RegenerateWithRandomSeed()
    {
        Regenerate(Random.Range(int.MinValue, int.MaxValue));
    }

    [ContextMenu("Regenerate With Initial Seed")]
    public void RegenerateWithInitialSeed()
    {
        Regenerate(initialSeed);
    }

    public void Regenerate(int seed)
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[RuntimeMapCycleManager] Runtime regeneration is only supported in Play Mode.", this);
            return;
        }

        if (regenerationCoroutine != null)
        {
            StopCoroutine(regenerationCoroutine);
        }

        regenerationCoroutine = StartCoroutine(RegenerateRoutine(seed));
    }

    private IEnumerator RegenerateRoutine(int seed)
    {
        isRegenerating = true;
        currentSeed = seed;
        OnGenerationStarted?.Invoke(seed);

        EnsureRuntimeRoot();
        yield return ClearRuntimeObjectsRoutine();

        yield return RegenerateMapMagic(seed);

        Terrain[] terrains = RuntimeTerrainUtility.ResolveTerrains(mapMagic, explicitTerrains);
        if (terrains.Length == 0)
        {
            Debug.LogError("[RuntimeMapCycleManager] No terrain found for runtime generation.", this);
            FinishGeneration(seed);
            yield break;
        }

        lastPlannedFields = PlanFarmFields(terrains, seed);

        reservationMask.Clear();
        for (int i = 0; i < lastPlannedFields.Count; i++)
        {
            reservationMask.AddField(lastPlannedFields[i], fieldReservationPadding);
        }

        if (flattenTerrainForFields)
        {
            yield return ApplyFieldFlatteningRoutine(terrains, lastPlannedFields);
        }

        if (clearDetailsUnderFields)
        {
            yield return ClearDetailsUnderFieldsRoutine(terrains, reservationMask);
        }

        if (clearTerrainTreesUnderFields)
        {
            yield return ClearTerrainTreesUnderFieldsRoutine(terrains, reservationMask);
        }

        if (spawnFarmFields)
        {
            yield return SpawnFarmFieldsRoutine(lastPlannedFields);
        }

        if (spawnTrees)
        {
            yield return ScatterTreesRoutine(terrains, reservationMask, seed);
        }

        if (rebuildNavMeshAfterGeneration)
        {
            yield return RebuildNavMeshRoutine();
        }

        FinishGeneration(seed);
    }

    private IEnumerator RegenerateMapMagic(int seed)
    {
        if (mapMagic == null || mapMagic.graph == null)
        {
            yield break;
        }

        mapMagic.graph.random.Seed = seed;
        mapMagic.Refresh(clearAll: true);

        yield return null;

        float startTime = Time.realtimeSinceStartup;
        while (mapMagic.IsGenerating())
        {
            if (mapMagicWaitTimeout > 0f && Time.realtimeSinceStartup - startTime > mapMagicWaitTimeout)
            {
                Debug.LogWarning("[RuntimeMapCycleManager] Timed out while waiting for MapMagic generation.", this);
                yield break;
            }

            yield return null;
        }

        yield return null;
    }

    private List<PlannedFarmField> PlanFarmFields(Terrain[] terrains, int seed)
    {
        List<PlannedFarmField> fields = new List<PlannedFarmField>();
        RuntimeTerrainReservationMask plannedMask = new RuntimeTerrainReservationMask();
        System.Random random = new System.Random(seed ^ 0x5F3759DF);

        if (farmFieldPresets == null || farmFieldPresets.Length == 0 || farmFieldCount <= 0)
        {
            return fields;
        }

        int maxAttempts = Mathf.Max(fieldPlacementAttempts, farmFieldCount * 200);
        for (int attempt = 0; attempt < maxAttempts && fields.Count < farmFieldCount; attempt++)
        {
            RuntimeFieldPreset preset = PickFarmFieldPreset(random);
            Terrain terrain = RuntimeTerrainUtility.ChooseTerrainByArea(terrains, random);

            if (preset == null || terrain == null || terrain.terrainData == null)
            {
                continue;
            }

            if (!preset.TryGetRuntimeFootprint(out RuntimeFarmFieldFootprintData footprintData))
            {
                continue;
            }

            Rect terrainRect = RuntimeTerrainUtility.GetWorldRect(terrain);
            float scale = RuntimeTerrainUtility.NextFloat(random, preset.randomScaleRange.x, preset.randomScaleRange.y);
            float yaw = GetFarmFieldYaw(preset, footprintData.size, scale, random);

            Vector2 footprint = footprintData.size * scale;
            Vector2 rotatedBounds = GetRotatedBounds(footprint, yaw);
            float marginX = rotatedBounds.x * 0.5f + 1f;
            float marginZ = rotatedBounds.y * 0.5f + 1f;

            if (terrainRect.width <= marginX * 2f || terrainRect.height <= marginZ * 2f)
            {
                continue;
            }

            Vector2 footprintCenter = new Vector2(
                RuntimeTerrainUtility.NextFloat(random, terrainRect.xMin + marginX, terrainRect.xMax - marginX),
                RuntimeTerrainUtility.NextFloat(random, terrainRect.yMin + marginZ, terrainRect.yMax - marginZ));

            if (!TryCreateFieldPlan(terrains, preset, footprintData, footprintCenter, yaw, scale, plannedMask, out PlannedFarmField field))
            {
                continue;
            }

            fields.Add(field);
            plannedMask.AddField(field, fieldReservationPadding);
        }

        if (fields.Count < farmFieldCount)
        {
            Debug.LogWarning($"[RuntimeMapCycleManager] Planned {fields.Count}/{farmFieldCount} farm fields. Increase Field Placement Attempts or relax Farm Field slope safety values if this is too low.", this);
        }

        return fields;
    }

    private RuntimeFieldPreset PickFarmFieldPreset(System.Random random)
    {
        if (farmFieldPresets == null || farmFieldPresets.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < farmFieldPresets.Length; i++)
        {
            RuntimeFieldPreset preset = farmFieldPresets[random.Next(0, farmFieldPresets.Length)];
            if (preset != null)
            {
                return preset;
            }
        }

        return null;
    }

    private bool TryCreateFieldPlan(
        Terrain[] terrains,
        RuntimeFieldPreset preset,
        RuntimeFarmFieldFootprintData footprintData,
        Vector2 footprintCenter,
        float yaw,
        float scale,
        RuntimeTerrainReservationMask plannedMask,
        out PlannedFarmField field)
    {
        field = null;

        float safeScale = Mathf.Max(0.01f, scale);
        Vector2 footprint = footprintData.size * safeScale;
        PlannedFieldFootprintPart[] plannedParts = BuildPlannedFootprintParts(footprintData, footprintCenter, yaw, safeScale);
        int grid = Mathf.Max(2, preset.sampleGrid);
        float minHeight = float.MaxValue;
        float maxHeight = float.MinValue;
        float heightSum = 0f;
        float maxSlope = 0f;
        float slopeSum = 0f;
        int sampleCount = 0;
        float[] heightSamples = new float[Mathf.Max(1, plannedParts.Length) * grid * grid];

        for (int partIndex = 0; partIndex < plannedParts.Length; partIndex++)
        {
            PlannedFieldFootprintPart part = plannedParts[partIndex];
            Vector2 half = part.size * 0.5f;

            for (int z = 0; z < grid; z++)
            {
                float tz = grid == 1 ? 0.5f : z / (float)(grid - 1);
                for (int x = 0; x < grid; x++)
                {
                    float tx = grid == 1 ? 0.5f : x / (float)(grid - 1);
                    Vector2 local = new Vector2(
                        Mathf.Lerp(-half.x, half.x, tx),
                        Mathf.Lerp(-half.y, half.y, tz));
                    Vector2 worldXZ = part.center + RuntimeTerrainUtility.Rotate(local, part.yawDegrees);

                    if (!RuntimeTerrainUtility.TrySample(terrains, worldXZ, out RuntimeTerrainSample sample))
                    {
                        return false;
                    }

                    float height = sample.position.y;
                    minHeight = Mathf.Min(minHeight, height);
                    maxHeight = Mathf.Max(maxHeight, height);
                    heightSum += height;
                    heightSamples[sampleCount] = height;
                    maxSlope = Mathf.Max(maxSlope, sample.slopeDegrees);
                    slopeSum += sample.slopeDegrees;
                    sampleCount++;
                }
            }
        }

        if (sampleCount == 0)
        {
            return false;
        }

        float averageHeight = heightSum / sampleCount;
        float targetHeight = CalculateMedianHeight(heightSamples, sampleCount);
        float averageSlope = slopeSum / sampleCount;
        float heightDelta = maxHeight - minHeight;
        if (averageHeight < preset.minWorldHeight || averageHeight > preset.maxWorldHeight)
        {
            return false;
        }

        if (useFarmFieldSlopeSafetyFilter &&
            (averageSlope > maxFarmFieldAverageSlopeDegrees ||
             maxSlope > maxFarmFieldPeakSlopeDegrees ||
             heightDelta > GetAllowedFarmFieldHeightDelta(footprint)))
        {
            return false;
        }

        if (preset.useHeightDeltaFilter && heightDelta > preset.maxHeightDelta)
        {
            return false;
        }

        if (preset.useSlopeFilter && maxSlope > preset.maxSlopeDegrees)
        {
            return false;
        }

        Rect rect = GetUnionRect(plannedParts);
        Rect paddedRect = RuntimeTerrainReservationMask.Pad(rect, fieldReservationPadding);

        if (plannedMask.Overlaps(paddedRect))
        {
            return false;
        }

        Rect occupiedRect = rect;
        Vector2 scaledCenterOffset = footprintData.centerOffset * safeScale;
        Vector2 pivotXZ = footprintCenter - RuntimeTerrainUtility.Rotate(scaledCenterOffset, yaw);
        float pivotY = targetHeight - footprintData.groundLocalY * safeScale;
        Vector3 position = new Vector3(pivotXZ.x, pivotY, pivotXZ.y);
        field = new PlannedFarmField(
            preset,
            position,
            Quaternion.Euler(0f, yaw, 0f),
            footprint,
            footprintCenter,
            plannedParts,
            occupiedRect,
            targetHeight,
            yaw,
            scale);

        return true;
    }

    private static PlannedFieldFootprintPart[] BuildPlannedFootprintParts(
        RuntimeFarmFieldFootprintData footprintData,
        Vector2 footprintCenter,
        float yaw,
        float scale)
    {
        RuntimeFieldFootprintPartData[] sourceParts = footprintData.parts;
        if (sourceParts == null || sourceParts.Length == 0)
        {
            sourceParts = new[]
            {
                new RuntimeFieldFootprintPartData(footprintData.size, footprintData.centerOffset, 0f, footprintData.groundLocalY)
            };
        }

        PlannedFieldFootprintPart[] plannedParts = new PlannedFieldFootprintPart[sourceParts.Length];
        for (int i = 0; i < sourceParts.Length; i++)
        {
            RuntimeFieldFootprintPartData part = sourceParts[i];
            Vector2 localFromAggregateCenter = (part.centerOffset - footprintData.centerOffset) * scale;
            Vector2 worldCenter = footprintCenter + RuntimeTerrainUtility.Rotate(localFromAggregateCenter, yaw);
            plannedParts[i] = new PlannedFieldFootprintPart(
                worldCenter,
                part.size * scale,
                yaw + part.yawDegrees);
        }

        return plannedParts;
    }

    private static Rect GetUnionRect(PlannedFieldFootprintPart[] parts)
    {
        if (parts == null || parts.Length == 0)
        {
            return new Rect();
        }

        Rect union = RuntimeTerrainUtility.GetRotatedWorldRect(parts[0].center, parts[0].size, parts[0].yawDegrees);
        for (int i = 1; i < parts.Length; i++)
        {
            Rect rect = RuntimeTerrainUtility.GetRotatedWorldRect(parts[i].center, parts[i].size, parts[i].yawDegrees);
            float xMin = Mathf.Min(union.xMin, rect.xMin);
            float yMin = Mathf.Min(union.yMin, rect.yMin);
            float xMax = Mathf.Max(union.xMax, rect.xMax);
            float yMax = Mathf.Max(union.yMax, rect.yMax);
            union = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        return union;
    }

    private IEnumerator ApplyFieldFlatteningRoutine(Terrain[] terrains, IReadOnlyList<PlannedFarmField> fields)
    {
        if (fields == null || fields.Count == 0)
        {
            yield break;
        }

        float frameStart = Time.realtimeSinceStartup;

        for (int terrainIndex = 0; terrainIndex < terrains.Length; terrainIndex++)
        {
            Terrain terrain = terrains[terrainIndex];
            if (terrain == null || terrain.terrainData == null)
            {
                continue;
            }

            TerrainData data = terrain.terrainData;
            int resolution = data.heightmapResolution;
            Vector3 terrainPosition = terrain.transform.position;
            Vector3 size = data.size;
            Rect terrainRect = RuntimeTerrainUtility.GetWorldRect(terrain);
            bool terrainChanged = false;

            for (int fieldIndex = 0; fieldIndex < fields.Count; fieldIndex++)
            {
                PlannedFarmField field = fields[fieldIndex];
                if (field == null || field.preset == null)
                {
                    continue;
                }

                float blendWidth = field.preset.flattenBlendWidth;
                Rect affectedRect = RuntimeTerrainReservationMask.Pad(field.worldRect, blendWidth + 1f);
                if (!terrainRect.Overlaps(affectedRect))
                {
                    continue;
                }

                int xMin = WorldToGridIndex(affectedRect.xMin, terrainPosition.x, size.x, resolution, Mathf.FloorToInt);
                int xMax = WorldToGridIndex(affectedRect.xMax, terrainPosition.x, size.x, resolution, Mathf.CeilToInt);
                int zMin = WorldToGridIndex(affectedRect.yMin, terrainPosition.z, size.z, resolution, Mathf.FloorToInt);
                int zMax = WorldToGridIndex(affectedRect.yMax, terrainPosition.z, size.z, resolution, Mathf.CeilToInt);
                int width = xMax - xMin + 1;
                int height = zMax - zMin + 1;

                if (width <= 0 || height <= 0)
                {
                    continue;
                }

                float[,] heights = data.GetHeights(xMin, zMin, width, height);
                float targetHeight = Mathf.InverseLerp(terrainPosition.y, terrainPosition.y + size.y, field.targetWorldHeight);
                bool patchChanged = false;

                for (int z = 0; z < height; z++)
                {
                    float worldZ = terrainPosition.z + (zMin + z) / (float)(resolution - 1) * size.z;
                    for (int x = 0; x < width; x++)
                    {
                        float worldX = terrainPosition.x + (xMin + x) / (float)(resolution - 1) * size.x;
                        float weight = CalculateFieldFlattenWeight(field, new Vector2(worldX, worldZ));

                        if (weight <= 0f)
                        {
                            continue;
                        }

                        heights[z, x] = Mathf.Lerp(heights[z, x], targetHeight, weight);
                        patchChanged = true;
                    }

                    if (IsOverFrameBudget(frameStart))
                    {
                        yield return null;
                        frameStart = Time.realtimeSinceStartup;
                    }
                }

                if (patchChanged)
                {
                    if (useDelayedHeightmapLod)
                    {
                        data.SetHeightsDelayLOD(xMin, zMin, heights);
                    }
                    else
                    {
                        data.SetHeights(xMin, zMin, heights);
                    }

                    terrainChanged = true;
                }
            }

            if (terrainChanged)
            {
                if (useDelayedHeightmapLod)
                {
                    data.SyncHeightmap();
                }
                terrain.Flush();
                yield return null;
                frameStart = Time.realtimeSinceStartup;
            }
        }
    }

    private IEnumerator ClearDetailsUnderFieldsRoutine(Terrain[] terrains, RuntimeTerrainReservationMask mask)
    {
        if (mask == null || mask.Areas.Count == 0)
        {
            yield break;
        }

        float frameStart = Time.realtimeSinceStartup;

        for (int terrainIndex = 0; terrainIndex < terrains.Length; terrainIndex++)
        {
            Terrain terrain = terrains[terrainIndex];
            if (terrain == null || terrain.terrainData == null)
            {
                continue;
            }

            TerrainData data = terrain.terrainData;
            int detailResolution = data.detailResolution;
            int layerCount = data.detailPrototypes != null ? data.detailPrototypes.Length : 0;

            if (detailResolution <= 0 || layerCount == 0)
            {
                continue;
            }

            Vector3 terrainPosition = terrain.transform.position;
            Vector3 size = data.size;
            Rect terrainRect = RuntimeTerrainUtility.GetWorldRect(terrain);

            for (int layer = 0; layer < layerCount; layer++)
            {
                for (int areaIndex = 0; areaIndex < mask.Areas.Count; areaIndex++)
                {
                    Rect area = RuntimeTerrainReservationMask.Pad(mask.Areas[areaIndex].rect, detailClearPadding);
                    if (!terrainRect.Overlaps(area))
                    {
                        continue;
                    }

                    int xMin = WorldToDetailIndex(area.xMin, terrainPosition.x, size.x, detailResolution, Mathf.FloorToInt);
                    int xMax = WorldToDetailIndex(area.xMax, terrainPosition.x, size.x, detailResolution, Mathf.CeilToInt);
                    int zMin = WorldToDetailIndex(area.yMin, terrainPosition.z, size.z, detailResolution, Mathf.FloorToInt);
                    int zMax = WorldToDetailIndex(area.yMax, terrainPosition.z, size.z, detailResolution, Mathf.CeilToInt);
                    int width = xMax - xMin + 1;
                    int height = zMax - zMin + 1;

                    if (width <= 0 || height <= 0)
                    {
                        continue;
                    }

                    int[,] details = data.GetDetailLayer(xMin, zMin, width, height, layer);
                    bool changed = false;

                    for (int z = 0; z < height; z++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            if (details[z, x] == 0)
                            {
                                continue;
                            }

                            details[z, x] = 0;
                            changed = true;
                        }
                    }

                    if (changed)
                    {
                        data.SetDetailLayer(xMin, zMin, layer, details);
                    }

                    if (IsOverFrameBudget(frameStart))
                    {
                        yield return null;
                        frameStart = Time.realtimeSinceStartup;
                    }
                }
            }
        }
    }

    private IEnumerator ClearTerrainTreesUnderFieldsRoutine(Terrain[] terrains, RuntimeTerrainReservationMask mask)
    {
        if (mask == null || mask.Areas.Count == 0)
        {
            yield break;
        }

        float frameStart = Time.realtimeSinceStartup;

        for (int terrainIndex = 0; terrainIndex < terrains.Length; terrainIndex++)
        {
            Terrain terrain = terrains[terrainIndex];
            if (terrain == null || terrain.terrainData == null)
            {
                continue;
            }

            TerrainData data = terrain.terrainData;
            TreeInstance[] treeInstances = data.treeInstances;
            if (treeInstances == null || treeInstances.Length == 0)
            {
                continue;
            }

            Vector3 terrainPosition = terrain.transform.position;
            Vector3 size = data.size;
            List<TreeInstance> keptTrees = new List<TreeInstance>(treeInstances.Length);

            for (int i = 0; i < treeInstances.Length; i++)
            {
                TreeInstance tree = treeInstances[i];
                Vector2 worldXZ = new Vector2(
                    terrainPosition.x + tree.position.x * size.x,
                    terrainPosition.z + tree.position.z * size.z);

                if (!mask.Contains(worldXZ, treeFieldPadding))
                {
                    keptTrees.Add(tree);
                }

                if ((i & 127) == 0 && IsOverFrameBudget(frameStart))
                {
                    yield return null;
                    frameStart = Time.realtimeSinceStartup;
                }
            }

            if (keptTrees.Count != treeInstances.Length)
            {
                data.SetTreeInstances(keptTrees.ToArray(), true);
                terrain.Flush();
                yield return null;
                frameStart = Time.realtimeSinceStartup;
            }
        }
    }

    private IEnumerator SpawnFarmFieldsRoutine(IReadOnlyList<PlannedFarmField> fields)
    {
        if (fields == null || fields.Count == 0)
        {
            yield break;
        }

        Transform parent = CreateRuntimeGroup("Farm Fields");
        int spawnedThisFrame = 0;

        for (int i = 0; i < fields.Count; i++)
        {
            PlannedFarmField field = fields[i];
            if (field == null || field.preset == null || field.preset.prefab == null)
            {
                continue;
            }

            GameObject instance = Instantiate(field.preset.prefab, field.position, field.rotation, parent);
            instance.transform.localScale = instance.transform.localScale * field.scale;
            instance.name = $"{field.preset.prefab.name}_Field_{i:00}";

            spawnedThisFrame++;
            if (spawnedThisFrame >= objectSpawnsPerFrame)
            {
                spawnedThisFrame = 0;
                yield return null;
            }
        }
    }

    private IEnumerator ScatterTreesRoutine(Terrain[] terrains, RuntimeTerrainReservationMask mask, int seed)
    {
        if (treePrefabs == null || treePrefabs.Length == 0 || targetTreeCount <= 0)
        {
            yield break;
        }

        Transform parent = treeSpawnMode == TreeSpawnMode.GameObjects ? CreateRuntimeGroup("Trees") : null;
        System.Random random = new System.Random(seed ^ 0x7331AA55);
        RuntimeSpatialHash2D spatialHash = minTreeDistance > 0f
            ? new RuntimeSpatialHash2D(minTreeDistance)
            : null;
        Dictionary<Terrain, List<TreeInstance>> terrainTreeAdds = treeSpawnMode == TreeSpawnMode.TerrainTreeInstances
            ? new Dictionary<Terrain, List<TreeInstance>>()
            : null;
        int spawnedCount = 0;
        int spawnedThisFrame = 0;
        float frameStart = Time.realtimeSinceStartup;

        if (treeSpawnMode == TreeSpawnMode.TerrainTreeInstances && clearOwnedTerrainTreesBeforeScatter)
        {
            yield return ClearOwnedTerrainTreesRoutine(terrains);
            frameStart = Time.realtimeSinceStartup;
        }

        for (int attempt = 0; attempt < treePlacementAttempts && spawnedCount < targetTreeCount; attempt++)
        {
            Terrain terrain = RuntimeTerrainUtility.ChooseTerrainByArea(terrains, random);
            if (terrain == null || terrain.terrainData == null)
            {
                continue;
            }

            Rect terrainRect = RuntimeTerrainUtility.GetWorldRect(terrain);
            Vector2 worldXZ = new Vector2(
                RuntimeTerrainUtility.NextFloat(random, terrainRect.xMin, terrainRect.xMax),
                RuntimeTerrainUtility.NextFloat(random, terrainRect.yMin, terrainRect.yMax));

            if (mask != null && mask.Contains(worldXZ, treeFieldPadding))
            {
                continue;
            }

            if (!RuntimeTerrainUtility.TrySample(terrains, worldXZ, out RuntimeTerrainSample sample))
            {
                continue;
            }

            if (sample.position.y < minTreeWorldHeight || sample.position.y > maxTreeWorldHeight)
            {
                continue;
            }

            if (sample.slopeDegrees > maxTreeSlopeDegrees)
            {
                continue;
            }

            if (spatialHash != null && spatialHash.HasPointWithinDistance(worldXZ, minTreeDistance))
            {
                continue;
            }

            GameObject prefab = PickTreePrefab(random);
            if (prefab == null)
            {
                continue;
            }

            spatialHash?.Add(worldXZ);
            spawnedCount++;
            Quaternion yaw = Quaternion.Euler(0f, RuntimeTerrainUtility.NextFloat(random, 0f, 360f), 0f);
            float scale = RuntimeTerrainUtility.NextFloat(random, treeScaleRange.x, treeScaleRange.y);

            if (treeSpawnMode == TreeSpawnMode.TerrainTreeInstances)
            {
                AddPlannedTerrainTree(terrainTreeAdds, terrain, prefab, sample.position, yaw.eulerAngles.y, scale);
            }
            else
            {
                Vector3 position = sample.position + Vector3.up * treeYOffset;
                Quaternion rotation = alignTreesToTerrainNormal
                    ? Quaternion.FromToRotation(Vector3.up, sample.normal) * yaw
                    : yaw;

                GameObject tree = Instantiate(prefab, position, rotation, parent);
                tree.transform.localScale = tree.transform.localScale * scale;
                tree.name = $"{prefab.name}_Tree_{spawnedCount:0000}";
            }

            spawnedThisFrame++;
            if (spawnedThisFrame >= objectSpawnsPerFrame || IsOverFrameBudget(frameStart))
            {
                spawnedThisFrame = 0;
                yield return null;
                frameStart = Time.realtimeSinceStartup;
            }
        }

        if (spawnedCount < targetTreeCount)
        {
            Debug.LogWarning($"[RuntimeMapCycleManager] Spawned {spawnedCount}/{targetTreeCount} trees. Increase attempts or relax filters if this is too low.", this);
        }

        if (treeSpawnMode == TreeSpawnMode.TerrainTreeInstances)
        {
            yield return ApplyTerrainTreeInstancesRoutine(terrainTreeAdds);
        }
    }

    private GameObject PickTreePrefab(System.Random random)
    {
        if (treePrefabs == null || treePrefabs.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < treePrefabs.Length; i++)
        {
            GameObject prefab = treePrefabs[random.Next(0, treePrefabs.Length)];
            if (prefab != null)
            {
                return prefab;
            }
        }

        return null;
    }

    private void AddPlannedTerrainTree(
        Dictionary<Terrain, List<TreeInstance>> terrainTreeAdds,
        Terrain terrain,
        GameObject prefab,
        Vector3 worldPosition,
        float yawDegrees,
        float scale)
    {
        if (terrainTreeAdds == null || terrain == null || terrain.terrainData == null || prefab == null)
        {
            return;
        }

        TerrainData data = terrain.terrainData;
        int prototypeIndex = EnsureTerrainTreePrototype(data, prefab);
        if (prototypeIndex < 0)
        {
            return;
        }

        Vector3 terrainPosition = terrain.transform.position;
        Vector3 size = data.size;
        Vector3 normalizedPosition = new Vector3(
            Mathf.InverseLerp(terrainPosition.x, terrainPosition.x + size.x, worldPosition.x),
            Mathf.InverseLerp(terrainPosition.y, terrainPosition.y + size.y, worldPosition.y + treeYOffset),
            Mathf.InverseLerp(terrainPosition.z, terrainPosition.z + size.z, worldPosition.z));

        if (!terrainTreeAdds.TryGetValue(terrain, out List<TreeInstance> trees))
        {
            trees = new List<TreeInstance>();
            terrainTreeAdds.Add(terrain, trees);
        }

        trees.Add(new TreeInstance
        {
            prototypeIndex = prototypeIndex,
            position = normalizedPosition,
            widthScale = scale,
            heightScale = scale,
            rotation = yawDegrees * Mathf.Deg2Rad,
            color = Color.white,
            lightmapColor = Color.white
        });
    }

    private int EnsureTerrainTreePrototype(TerrainData data, GameObject prefab)
    {
        TreePrototype[] prototypes = data.treePrototypes;
        for (int i = 0; i < prototypes.Length; i++)
        {
            if (prototypes[i] != null && prototypes[i].prefab == prefab)
            {
                return i;
            }
        }

        List<TreePrototype> newPrototypes = new List<TreePrototype>(prototypes);
        newPrototypes.Add(new TreePrototype
        {
            prefab = prefab,
            bendFactor = 0.2f
        });

        data.treePrototypes = newPrototypes.ToArray();
        return newPrototypes.Count - 1;
    }

    private IEnumerator ApplyTerrainTreeInstancesRoutine(Dictionary<Terrain, List<TreeInstance>> terrainTreeAdds)
    {
        if (terrainTreeAdds == null || terrainTreeAdds.Count == 0)
        {
            yield break;
        }

        foreach (KeyValuePair<Terrain, List<TreeInstance>> pair in terrainTreeAdds)
        {
            Terrain terrain = pair.Key;
            if (terrain == null || terrain.terrainData == null || pair.Value == null || pair.Value.Count == 0)
            {
                continue;
            }

            TerrainData data = terrain.terrainData;
            List<TreeInstance> trees = new List<TreeInstance>(data.treeInstances);
            trees.AddRange(pair.Value);
            data.SetTreeInstances(trees.ToArray(), true);
            terrain.Flush();
            yield return null;
        }
    }

    private IEnumerator ClearOwnedTerrainTreesRoutine(Terrain[] terrains)
    {
        if (terrains == null || treePrefabs == null || treePrefabs.Length == 0)
        {
            yield break;
        }

        float frameStart = Time.realtimeSinceStartup;

        for (int terrainIndex = 0; terrainIndex < terrains.Length; terrainIndex++)
        {
            Terrain terrain = terrains[terrainIndex];
            if (terrain == null || terrain.terrainData == null)
            {
                continue;
            }

            TerrainData data = terrain.terrainData;
            TreeInstance[] trees = data.treeInstances;
            TreePrototype[] prototypes = data.treePrototypes;
            if (trees == null || trees.Length == 0 || prototypes == null || prototypes.Length == 0)
            {
                continue;
            }

            HashSet<int> ownedPrototypeIndexes = new HashSet<int>();
            for (int i = 0; i < prototypes.Length; i++)
            {
                GameObject prototypePrefab = prototypes[i]?.prefab;
                if (prototypePrefab != null && IsConfiguredTreePrefab(prototypePrefab))
                {
                    ownedPrototypeIndexes.Add(i);
                }
            }

            if (ownedPrototypeIndexes.Count == 0)
            {
                continue;
            }

            List<TreeInstance> keptTrees = new List<TreeInstance>(trees.Length);
            for (int i = 0; i < trees.Length; i++)
            {
                if (!ownedPrototypeIndexes.Contains(trees[i].prototypeIndex))
                {
                    keptTrees.Add(trees[i]);
                }

                if ((i & 255) == 0 && IsOverFrameBudget(frameStart))
                {
                    yield return null;
                    frameStart = Time.realtimeSinceStartup;
                }
            }

            if (keptTrees.Count != trees.Length)
            {
                data.SetTreeInstances(keptTrees.ToArray(), true);
                terrain.Flush();
                yield return null;
                frameStart = Time.realtimeSinceStartup;
            }
        }
    }

    private bool IsConfiguredTreePrefab(GameObject prefab)
    {
        for (int i = 0; i < treePrefabs.Length; i++)
        {
            if (treePrefabs[i] == prefab)
            {
                return true;
            }
        }

        return false;
    }

    private IEnumerator RebuildNavMeshRoutine()
    {
        if (navMeshSurfaces == null || navMeshSurfaces.Length == 0)
        {
            navMeshSurfaces = FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None);
        }

        for (int i = 0; i < navMeshSurfaces.Length; i++)
        {
            if (navMeshSurfaces[i] == null)
            {
                continue;
            }

            navMeshSurfaces[i].BuildNavMesh();
            yield return null;
        }
    }

    private void EnsureRuntimeRoot()
    {
        if (runtimeRoot != null)
        {
            return;
        }

        Transform existing = transform.Find(runtimeRootName);
        if (existing != null)
        {
            runtimeRoot = existing;
            return;
        }

        GameObject root = new GameObject(runtimeRootName);
        runtimeRoot = root.transform;
        runtimeRoot.SetParent(transform, false);
    }

    private IEnumerator ClearRuntimeObjectsRoutine()
    {
        EnsureRuntimeRoot();

        int destroyedThisFrame = 0;
        for (int i = runtimeRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = runtimeRoot.GetChild(i);
            Destroy(child.gameObject);

            destroyedThisFrame++;
            if (destroyedThisFrame >= objectSpawnsPerFrame)
            {
                destroyedThisFrame = 0;
                yield return null;
            }
        }
    }

    private Transform CreateRuntimeGroup(string groupName)
    {
        EnsureRuntimeRoot();

        GameObject group = new GameObject(groupName);
        group.transform.SetParent(runtimeRoot, false);
        return group.transform;
    }

    private void FinishGeneration(int seed)
    {
        isRegenerating = false;
        regenerationCoroutine = null;
        OnGenerationCompleted?.Invoke(seed);
        Debug.Log($"[RuntimeMapCycleManager] Runtime map generation completed. Seed={seed}, Fields={lastPlannedFields.Count}", this);
    }

    private static Vector2 GetRotatedBounds(Vector2 size, float yawDegrees)
    {
        float radians = yawDegrees * Mathf.Deg2Rad;
        float sin = Mathf.Abs(Mathf.Sin(radians));
        float cos = Mathf.Abs(Mathf.Cos(radians));
        return new Vector2(size.x * cos + size.y * sin, size.x * sin + size.y * cos);
    }

    private static float GetFarmFieldYaw(RuntimeFieldPreset preset, Vector2 footprintSize, float scale, System.Random random)
    {
        if (preset == null)
        {
            return 0f;
        }

        float longestSide = Mathf.Max(footprintSize.x, footprintSize.y) * Mathf.Max(0.01f, scale);
        bool shouldLockYaw = !preset.randomYaw ||
            (preset.lockYawWhenLarge && longestSide >= preset.largeFootprintYawLockThreshold);

        if (shouldLockYaw)
        {
            return preset.fixedYawDegrees;
        }

        return RuntimeTerrainUtility.NextFloat(random, preset.yawRange.x, preset.yawRange.y);
    }

    private float GetAllowedFarmFieldHeightDelta(Vector2 footprint)
    {
        float diagonal = Mathf.Max(1f, footprint.magnitude);
        float gradeAllowedDelta = Mathf.Tan(maxFarmFieldOverallGradeDegrees * Mathf.Deg2Rad) * diagonal;
        return Mathf.Max(maxFarmFieldHeightDelta, gradeAllowedDelta);
    }

    private static float CalculateMedianHeight(float[] samples, int count)
    {
        if (samples == null || count <= 0)
        {
            return 0f;
        }

        System.Array.Sort(samples, 0, count);
        int middle = count / 2;
        if ((count & 1) == 1)
        {
            return samples[middle];
        }

        return (samples[middle - 1] + samples[middle]) * 0.5f;
    }

    private static float CalculateFieldFlattenWeight(PlannedFarmField field, Vector2 worldXZ)
    {
        float blendWidth = field.preset != null ? field.preset.flattenBlendWidth : 0f;

        if (field.footprintParts == null || field.footprintParts.Length == 0)
        {
            Vector2 local = RuntimeTerrainUtility.Rotate(worldXZ - field.footprintCenter, -field.yawDegrees);
            Vector2 half = field.footprintSize * 0.5f;
            return CalculateRectFlattenWeight(local, half, blendWidth);
        }

        float weight = 0f;
        for (int i = 0; i < field.footprintParts.Length; i++)
        {
            PlannedFieldFootprintPart part = field.footprintParts[i];
            Vector2 local = RuntimeTerrainUtility.Rotate(worldXZ - part.center, -part.yawDegrees);
            Vector2 half = part.size * 0.5f;
            weight = Mathf.Max(weight, CalculateRectFlattenWeight(local, half, blendWidth));
        }

        return weight;
    }

    private static float CalculateRectFlattenWeight(Vector2 local, Vector2 half, float blendWidth)
    {
        float outsideX = Mathf.Max(Mathf.Abs(local.x) - half.x, 0f);
        float outsideZ = Mathf.Max(Mathf.Abs(local.y) - half.y, 0f);
        float outsideDistance = Mathf.Sqrt(outsideX * outsideX + outsideZ * outsideZ);

        if (outsideDistance <= 0f)
        {
            return 1f;
        }

        if (blendWidth <= 0f || outsideDistance > blendWidth)
        {
            return 0f;
        }

        float t = Mathf.Clamp01(outsideDistance / blendWidth);
        return 1f - Mathf.SmoothStep(0f, 1f, t);
    }

    private bool IsOverFrameBudget(float frameStart)
    {
        return frameBudgetMs > 0f && (Time.realtimeSinceStartup - frameStart) * 1000f >= frameBudgetMs;
    }

    private static int WorldToGridIndex(
        float worldValue,
        float terrainStart,
        float terrainSize,
        int resolution,
        System.Func<float, int> round)
    {
        float normalized = Mathf.InverseLerp(terrainStart, terrainStart + terrainSize, worldValue);
        int index = round(normalized * (resolution - 1));
        return Mathf.Clamp(index, 0, resolution - 1);
    }

    private static int WorldToDetailIndex(
        float worldValue,
        float terrainStart,
        float terrainSize,
        int resolution,
        System.Func<float, int> round)
    {
        float normalized = Mathf.InverseLerp(terrainStart, terrainStart + terrainSize, worldValue);
        int index = round(normalized * resolution);
        return Mathf.Clamp(index, 0, resolution - 1);
    }

    private void OnValidate()
    {
        frameBudgetMs = Mathf.Max(0.25f, frameBudgetMs);
        objectSpawnsPerFrame = Mathf.Max(1, objectSpawnsPerFrame);
        farmFieldCount = Mathf.Max(0, farmFieldCount);
        fieldPlacementAttempts = Mathf.Max(1, fieldPlacementAttempts);
        fieldReservationPadding = Mathf.Max(0f, fieldReservationPadding);
        maxFarmFieldAverageSlopeDegrees = Mathf.Clamp(maxFarmFieldAverageSlopeDegrees, 0f, 90f);
        maxFarmFieldPeakSlopeDegrees = Mathf.Clamp(maxFarmFieldPeakSlopeDegrees, maxFarmFieldAverageSlopeDegrees, 90f);
        maxFarmFieldOverallGradeDegrees = Mathf.Clamp(maxFarmFieldOverallGradeDegrees, 0f, maxFarmFieldPeakSlopeDegrees);
        maxFarmFieldHeightDelta = Mathf.Max(0f, maxFarmFieldHeightDelta);
        detailClearPadding = Mathf.Max(0f, detailClearPadding);
        targetTreeCount = Mathf.Max(0, targetTreeCount);
        treePlacementAttempts = Mathf.Max(1, treePlacementAttempts);
        minTreeDistance = Mathf.Max(0f, minTreeDistance);
        treeFieldPadding = Mathf.Max(0f, treeFieldPadding);
        maxTreeWorldHeight = Mathf.Max(minTreeWorldHeight, maxTreeWorldHeight);
        maxTreeSlopeDegrees = Mathf.Clamp(maxTreeSlopeDegrees, 0f, 90f);

        if (treeScaleRange.x <= 0f)
        {
            treeScaleRange.x = 1f;
        }

        if (treeScaleRange.y <= 0f)
        {
            treeScaleRange.y = treeScaleRange.x;
        }

        if (treeScaleRange.y < treeScaleRange.x)
        {
            treeScaleRange.y = treeScaleRange.x;
        }
    }
}
