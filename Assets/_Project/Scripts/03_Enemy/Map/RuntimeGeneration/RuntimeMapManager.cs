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
    [SerializeField] private bool spawnFarmFields = true;

    [Header("World Director")]
    [SerializeField] private bool useFieldDirector = true;
    [SerializeField] private Vector3 playerSpawnXZ = Vector3.zero;
    [SerializeField, Min(0f)] private float spawnFlatRadius = 18f;
    [SerializeField, Min(0f)] private float spawnBlendWidth = 18f;
    [SerializeField, Min(0f)] private float spawnFieldExclusionPadding = 28f;
    [SerializeField] private RuntimeDirectorTuningPreset directorTuningPreset = RuntimeDirectorTuningPreset.Forgiving;
    [SerializeField] private RuntimeFieldDistributionStyle fieldDistributionStyle = RuntimeFieldDistributionStyle.Mixed;
    [SerializeField, Range(0f, 1f)] private float fieldSpacing = 0.55f;
    [SerializeField, Range(0f, 1f)] private float factoryPresence = 0.35f;
    [SerializeField, Range(0f, 1f)] private float rareLocationPresence = 0.08f;
    [SerializeField, Range(0f, 1f)] private float naturalProtection = 0.45f;
    [SerializeField, Range(0f, 1f)] private float placementForgiveness = 0.75f;

    [Header("Arctic Terrain Shape")]
    [SerializeField] private bool ensureArcticMountainRelief = true;
    [SerializeField, Range(0f, 1f)] private float arcticMountainPresence = 0.45f;

    [Header("Terrain Modification")]
    [SerializeField] private bool flattenTerrainForFields = true;
    [SerializeField, Range(0f, 1f)] private float terrainBlendIrregularity = 0.45f;
    [SerializeField] private bool clearDetailsUnderFields = true;
    [SerializeField] private bool clearTerrainTreesUnderFields = true;

    [HideInInspector, SerializeField, Min(1)] private int fieldPlacementAttempts = 300;
    [HideInInspector, SerializeField, Min(0f)] private float fieldReservationPadding = 4f;
    [HideInInspector, SerializeField] private bool useFarmFieldSlopeSafetyFilter = true;
    [HideInInspector, SerializeField, Range(0f, 90f)] private float maxFarmFieldAverageSlopeDegrees = 18f;
    [HideInInspector, SerializeField, Range(0f, 90f)] private float maxFarmFieldPeakSlopeDegrees = 32f;
    [HideInInspector, SerializeField, Range(0f, 90f)] private float maxFarmFieldOverallGradeDegrees = 14f;
    [HideInInspector, SerializeField, Min(0f)] private float maxFarmFieldHeightDelta = 8f;
    [HideInInspector, SerializeField, Min(1)] private int fieldSectorColumns = 4;
    [HideInInspector, SerializeField, Min(1)] private int fieldSectorRows = 4;
    [HideInInspector, SerializeField, Min(1)] private int fieldCandidatesPerSector = 3;
    [HideInInspector, SerializeField, Min(0f)] private float minFieldCenterDistance = 45f;
    [HideInInspector, SerializeField, Min(0f)] private float nearFieldDistance = 180f;
    [HideInInspector, SerializeField, Min(0f)] private float midFieldDistance = 420f;
    [HideInInspector, SerializeField, Range(0f, 1f)] private float distanceBandExceptionWeight = 0.12f;
    [HideInInspector, SerializeField, Range(0f, 1f)] private float houseTargetRatio = 0.6f;
    [HideInInspector, SerializeField, Range(0f, 1f)] private float factoryTargetRatio = 0.25f;
    [HideInInspector, SerializeField, Range(0f, 1f)] private float storageTargetRatio = 0.1f;
    [HideInInspector, SerializeField, Range(0f, 1f)] private float ruinsTargetRatio = 0.05f;
    [HideInInspector, SerializeField, Range(0f, 1f)] private float landmarkTargetRatio;
    [HideInInspector, SerializeField, Range(0f, 1f)] private float specialTargetRatio;
    [HideInInspector, SerializeField, Min(0f)] private float categoryBudgetCorrectionStrength = 1.25f;
    [HideInInspector, SerializeField, Min(0f)] private float maxFarmFieldFlattenCost = 3f;
    [HideInInspector, SerializeField, Min(0f)] private float maxFarmFieldSingleSampleFlattenCost = 8f;
    [HideInInspector, SerializeField, Range(0f, 2f)] private float fieldClusterAffinity = 0.35f;
    [HideInInspector, SerializeField, Min(0f)] private float fieldClusterRadius = 140f;
    [HideInInspector, SerializeField, Range(0f, 1f)] private float settlementLayoutStrength = 0.65f;
    [HideInInspector, SerializeField, Min(0f)] private float settlementLotSpacing = 34f;
    [HideInInspector, SerializeField, Min(0f)] private float settlementStreetHalfWidth = 22f;
    [HideInInspector, SerializeField, Min(0f)] private float detailClearPadding = 2f;
    [HideInInspector, SerializeField, Min(0.0001f)] private float flattenBlendNoiseScale = 0.075f;
    [HideInInspector, SerializeField, Min(0f)] private float minimumArcticReliefRange = 24f;
    [HideInInspector, SerializeField, Min(0f)] private float arcticReliefHeight = 18f;
    [HideInInspector, SerializeField, Range(0f, 1f)] private float arcticReliefNoiseThreshold = 0.68f;
    [HideInInspector, SerializeField, Min(0.0001f)] private float arcticReliefNoiseScale = 0.0025f;
    [HideInInspector, SerializeField, Min(0f)] private float arcticReliefStartSafeRadius = 120f;
    [HideInInspector, SerializeField, Min(0f)] private float arcticReliefFieldPadding = 36f;
    [HideInInspector, SerializeField] private Vector3 directorFallbackStartPosition = new Vector3(15f, 0f, 15f);

    private enum TreeSpawnMode
    {
        TerrainTreeInstances,
        GameObjects
    }

    [Header("Natural Scatter")]
    [SerializeField, InspectorName("Spawn Natural Scatter")] private bool spawnTrees = true;
    [SerializeField, InspectorName("Spawn Mode")] private TreeSpawnMode treeSpawnMode = TreeSpawnMode.TerrainTreeInstances;
    [SerializeField] private RuntimeNaturalScatterPreset[] naturalScatterPresets;
    [SerializeField, Min(0), InspectorName("Target Natural Scatter Count")] private int targetTreeCount = 300;
    [SerializeField, Range(0f, 1f)] private float forestPatchiness = 0.65f;
    [SerializeField, Range(0f, 1f)] private float fieldEdgeClearance = 0.65f;

    [HideInInspector, SerializeField] private bool clearOwnedTerrainTreesBeforeScatter = true;
    [HideInInspector, SerializeField, Min(1)] private int treePlacementAttempts = 6000;
    [HideInInspector, SerializeField, Min(0f)] private float minTreeDistance = 6f;
    [HideInInspector, SerializeField, Min(0f)] private float treeFieldPadding = 6f;
    [HideInInspector, SerializeField] private float minTreeWorldHeight = 0f;
    [HideInInspector, SerializeField] private float maxTreeWorldHeight = 220f;
    [HideInInspector, SerializeField, Range(0f, 90f)] private float maxTreeSlopeDegrees = 30f;
    [HideInInspector, SerializeField] private Vector2 treeScaleRange = new Vector2(0.85f, 1.25f);
    [HideInInspector, SerializeField] private bool alignTreesToTerrainNormal;
    [HideInInspector, SerializeField] private float treeYOffset;
    [HideInInspector, SerializeField] private bool useNaturalScatterNoise = true;
    [HideInInspector, SerializeField, Min(0.0001f)] private float naturalScatterNoiseScale = 0.0125f;
    [HideInInspector, SerializeField, Range(0f, 1f)] private float naturalScatterNoiseThreshold = 0.3f;
    [HideInInspector, SerializeField, Range(0f, 1f)] private float naturalScatterNoiseStrength = 0.75f;
    [HideInInspector, SerializeField, Min(0.0001f)] private float naturalScatterDetailNoiseScale = 0.045f;
    [HideInInspector, SerializeField, Min(0.0001f)] private float naturalScatterCorridorNoiseScale = 0.0035f;
    [HideInInspector, SerializeField, Range(0f, 1f)] private float naturalScatterCorridorStrength = 0.35f;
    [HideInInspector, SerializeField, Range(0f, 1f)] private float naturalScatterMinimumDensity = 0.08f;
    [HideInInspector, SerializeField, Range(0f, 1f)] private float rockClusterChance = 0.35f;
    [HideInInspector, SerializeField, Min(1)] private int rockClusterMaxCount = 4;
    [HideInInspector, SerializeField, Min(0f)] private float rockClusterRadius = 8f;

    [Header("Navigation")]
    [SerializeField] private bool rebuildNavMeshAfterGeneration;
    [SerializeField] private NavMeshSurface[] navMeshSurfaces;

    [Header("Generation Debug")]
    [SerializeField] private bool drawGenerationGizmos = true;
    [SerializeField] private bool drawRejectedFieldCandidates;
    [SerializeField, Min(0)] private int maxDebugCandidateGizmos = 200;
    [SerializeField, Min(1)] private int seedValidationCount = 100;

    [Header("Events")]
    public UnityEvent<int> OnGenerationStarted;
    public UnityEvent<int> OnGenerationCompleted;

    [SerializeField, HideInInspector] private int currentSeed;
    [SerializeField, HideInInspector] private List<PlannedFarmField> lastPlannedFields = new List<PlannedFarmField>();
    [SerializeField, HideInInspector] private List<RuntimeFieldPlacementDebugPoint> lastFieldPlacementDebug = new List<RuntimeFieldPlacementDebugPoint>();
    [SerializeField, HideInInspector] private RuntimeSpawnSafeZone lastSpawnSafeZone;

    private struct FieldCandidate
    {
        public Vector2 point;
        public float distanceFromStart;
        public float normalizedDistanceFromStart;

        public FieldCandidate(Vector2 point, float distanceFromStart, float normalizedDistanceFromStart)
        {
            this.point = point;
            this.distanceFromStart = distanceFromStart;
            this.normalizedDistanceFromStart = normalizedDistanceFromStart;
        }
    }

    private struct NaturalScatterChoice
    {
        public GameObject[] prefabs;
        public RuntimeNaturalScatterKind kind;
        public bool spawnAsTerrainTreeInstance;
        public bool alignToTerrainNormal;
        public Vector2 scaleRange;
        public float randomTiltDegrees;
        public float clusterChance;
        public int maxClusterCount;
        public float clusterRadius;
    }

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

    [ContextMenu("Validate 100 Director Seeds On Current Terrain")]
    public void ValidateDirectorSeedsOnCurrentTerrain()
    {
        Terrain[] terrains = RuntimeTerrainUtility.ResolveTerrains(mapMagic, explicitTerrains);
        if (terrains.Length == 0)
        {
            Debug.LogWarning("[RuntimeMapCycleManager] Cannot validate director seeds because no terrain is available.", this);
            return;
        }

        ApplyDesignerTuning(terrains);
        int validationCount = Mathf.Max(1, seedValidationCount);
        int completeSeeds = 0;
        int totalFields = 0;
        int[] categoryCounts = new int[System.Enum.GetValues(typeof(RuntimeFieldCategory)).Length];
        float totalFlattenCost = 0f;
        int flattenCostSamples = 0;

        for (int i = 0; i < validationCount; i++)
        {
            int seed = initialSeed + i;
            List<PlannedFarmField> fields = PlanFarmFields(terrains, seed, reportWarning: false, collectDebug: false);
            totalFields += fields.Count;

            if (fields.Count >= farmFieldCount)
            {
                completeSeeds++;
            }

            for (int fieldIndex = 0; fieldIndex < fields.Count; fieldIndex++)
            {
                PlannedFarmField field = fields[fieldIndex];
                if (field == null)
                {
                    continue;
                }

                int categoryIndex = (int)field.category;
                if (categoryIndex >= 0 && categoryIndex < categoryCounts.Length)
                {
                    categoryCounts[categoryIndex]++;
                }

                totalFlattenCost += field.flattenCost;
                flattenCostSamples++;
            }
        }

        float averageFields = totalFields / (float)validationCount;
        float averageFlattenCost = flattenCostSamples > 0 ? totalFlattenCost / flattenCostSamples : 0f;
        Debug.Log(
            "[RuntimeMapCycleManager] Director seed validation " +
            $"Seeds={validationCount}, Complete={completeSeeds}/{validationCount}, " +
            $"AverageFields={averageFields:0.00}/{farmFieldCount}, " +
            $"AverageFlattenCost={averageFlattenCost:0.00}, " +
            $"House={categoryCounts[(int)RuntimeFieldCategory.House]}, " +
            $"Factory={categoryCounts[(int)RuntimeFieldCategory.Factory]}, " +
            $"Storage={categoryCounts[(int)RuntimeFieldCategory.Storage]}, " +
            $"Ruins={categoryCounts[(int)RuntimeFieldCategory.Ruins]}, " +
            $"Landmark={categoryCounts[(int)RuntimeFieldCategory.Landmark]}, " +
            $"Special={categoryCounts[(int)RuntimeFieldCategory.Special]}",
            this);
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

        ApplyDesignerTuning(terrains);
        lastSpawnSafeZone = CreateSpawnSafeZone(terrains);
        lastPlannedFields = PlanFarmFields(terrains, seed);

        reservationMask.Clear();
        RuntimeTerrainReservationMask naturalScatterBlockMask = new RuntimeTerrainReservationMask();
        AddSpawnSafeZoneReservations(reservationMask, naturalScatterBlockMask);
        for (int i = 0; i < lastPlannedFields.Count; i++)
        {
            reservationMask.AddField(lastPlannedFields[i], fieldReservationPadding);
            naturalScatterBlockMask.AddField(lastPlannedFields[i], 0f);
        }

        if (ensureArcticMountainRelief)
        {
            yield return EnsureArcticMountainReliefRoutine(terrains, reservationMask, seed);
        }

        if (lastSpawnSafeZone != null)
        {
            yield return ApplySpawnSafeZoneFlatteningRoutine(terrains, lastSpawnSafeZone);
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
            yield return ClearTerrainTreesUnderFieldsRoutine(terrains, naturalScatterBlockMask);
        }

        if (spawnFarmFields)
        {
            yield return SpawnFarmFieldsRoutine(lastPlannedFields);
        }

        if (spawnTrees)
        {
            yield return ScatterTreesRoutine(terrains, naturalScatterBlockMask, seed);
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

    private void ApplyDesignerTuning(Terrain[] terrains)
    {
        float forgiveness = Mathf.Clamp01(placementForgiveness);
        float protection = Mathf.Clamp01(naturalProtection);
        float spacing = Mathf.Clamp01(fieldSpacing);
        float mapDistance = CalculateMaxDirectorDistance(terrains);

        switch (directorTuningPreset)
        {
            case RuntimeDirectorTuningPreset.Forgiving:
                forgiveness = Mathf.Max(forgiveness, 0.85f);
                protection = Mathf.Min(protection, 0.45f);
                break;
            case RuntimeDirectorTuningPreset.Scenic:
                protection = Mathf.Max(protection, 0.75f);
                forgiveness = Mathf.Min(forgiveness, 0.55f);
                break;
            case RuntimeDirectorTuningPreset.Dense:
                forgiveness = Mathf.Max(forgiveness, 0.75f);
                spacing = Mathf.Min(spacing, 0.35f);
                break;
        }

        fieldPlacementAttempts = Mathf.Max(300, Mathf.RoundToInt(farmFieldCount * Mathf.Lerp(180f, 420f, forgiveness)));
        fieldSectorColumns = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(Mathf.Max(1, farmFieldCount)) * 1.4f), 3, 8);
        fieldSectorRows = fieldSectorColumns;
        fieldCandidatesPerSector = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(3f, 8f, forgiveness)), 3, 8);
        minFieldCenterDistance = Mathf.Lerp(32f, 110f, spacing);
        fieldReservationPadding = Mathf.Lerp(4f, 14f, spacing);
        nearFieldDistance = mapDistance * 0.24f;
        midFieldDistance = mapDistance * 0.58f;
        distanceBandExceptionWeight = Mathf.Lerp(0.06f, 0.22f, forgiveness);
        categoryBudgetCorrectionStrength = Mathf.Lerp(0.8f, 1.8f, forgiveness);
        fieldClusterAffinity = Mathf.Lerp(0.15f, 0.55f, 1f - spacing);
        fieldClusterRadius = Mathf.Lerp(90f, 190f, 1f - spacing);
        settlementLotSpacing = Mathf.Lerp(28f, 44f, spacing);
        settlementStreetHalfWidth = Mathf.Lerp(16f, 28f, spacing);

        maxFarmFieldAverageSlopeDegrees = Mathf.Lerp(28f, 14f, protection) + Mathf.Lerp(0f, 6f, forgiveness);
        maxFarmFieldPeakSlopeDegrees = Mathf.Lerp(48f, 26f, protection) + Mathf.Lerp(0f, 8f, forgiveness);
        maxFarmFieldOverallGradeDegrees = Mathf.Lerp(22f, 10f, protection) + Mathf.Lerp(0f, 5f, forgiveness);
        maxFarmFieldHeightDelta = Mathf.Lerp(18f, 5f, protection) + Mathf.Lerp(0f, 5f, forgiveness);
        maxFarmFieldFlattenCost = Mathf.Lerp(8f, 1.8f, protection) + Mathf.Lerp(0f, 2.5f, forgiveness);
        maxFarmFieldSingleSampleFlattenCost = Mathf.Lerp(18f, 5f, protection) + Mathf.Lerp(0f, 4f, forgiveness);
        detailClearPadding = Mathf.Lerp(1f, 4f, protection);
        flattenBlendNoiseScale = Mathf.Lerp(0.045f, 0.11f, terrainBlendIrregularity);
        minimumArcticReliefRange = Mathf.Lerp(14f, 44f, arcticMountainPresence);
        arcticReliefHeight = Mathf.Lerp(8f, 34f, arcticMountainPresence) * Mathf.Lerp(1.2f, 0.85f, protection);
        arcticReliefNoiseThreshold = Mathf.Lerp(0.78f, 0.56f, arcticMountainPresence);
        arcticReliefNoiseScale = Mathf.Lerp(0.0016f, 0.0042f, arcticMountainPresence);
        arcticReliefStartSafeRadius = Mathf.Max(80f, nearFieldDistance * 0.45f);
        arcticReliefFieldPadding = Mathf.Lerp(26f, 54f, spacing);

        ApplyCategoryMix();

        treePlacementAttempts = Mathf.Max(1000, Mathf.RoundToInt(targetTreeCount * Mathf.Lerp(12f, 28f, Mathf.Max(forgiveness, forestPatchiness))));
        minTreeDistance = Mathf.Lerp(4f, 9f, 1f - forestPatchiness);
        treeFieldPadding = Mathf.Lerp(1.25f, 6f, Mathf.Clamp01(fieldEdgeClearance));
        maxTreeSlopeDegrees = Mathf.Lerp(38f, 22f, protection);
        useNaturalScatterNoise = true;
        naturalScatterNoiseScale = Mathf.Lerp(0.018f, 0.006f, forestPatchiness);
        naturalScatterNoiseThreshold = Mathf.Lerp(0.18f, 0.45f, forestPatchiness);
        naturalScatterNoiseStrength = Mathf.Lerp(0.35f, 0.9f, forestPatchiness);
        naturalScatterDetailNoiseScale = Mathf.Lerp(0.052f, 0.026f, forestPatchiness);
        naturalScatterCorridorNoiseScale = Mathf.Lerp(0.0048f, 0.0024f, forestPatchiness);
        naturalScatterCorridorStrength = Mathf.Lerp(0.12f, 0.48f, forestPatchiness);
        naturalScatterMinimumDensity = Mathf.Lerp(0.18f, 0.04f, forestPatchiness);
        rockClusterChance = Mathf.Lerp(0.2f, 0.55f, forestPatchiness);
        rockClusterMaxCount = Mathf.RoundToInt(Mathf.Lerp(2f, 5f, forestPatchiness));
        rockClusterRadius = Mathf.Lerp(5f, 12f, forestPatchiness);
    }

    private void ApplyCategoryMix()
    {
        float factory = Mathf.Lerp(0.08f, 0.34f, Mathf.Clamp01(factoryPresence));
        float rare = Mathf.Lerp(0f, 0.16f, Mathf.Clamp01(rareLocationPresence));
        float storage = 0.12f;
        float ruins = 0.06f;
        float landmark = rare * 0.45f;
        float special = rare * 0.55f;

        switch (fieldDistributionStyle)
        {
            case RuntimeFieldDistributionStyle.Settlements:
                factory *= 0.55f;
                storage += 0.06f;
                ruins *= 0.75f;
                settlementLayoutStrength = 0.9f;
                break;
            case RuntimeFieldDistributionStyle.Industrial:
                factory = Mathf.Max(factory, 0.32f);
                storage += 0.08f;
                ruins += 0.03f;
                settlementLayoutStrength = 0.35f;
                break;
            case RuntimeFieldDistributionStyle.Ruined:
                factory *= 0.75f;
                ruins = Mathf.Max(ruins, 0.22f);
                special += 0.03f;
                settlementLayoutStrength = 0.45f;
                break;
            default:
                settlementLayoutStrength = 0.65f;
                break;
        }

        float house = Mathf.Max(0.05f, 1f - factory - storage - ruins - landmark - special);
        houseTargetRatio = house;
        factoryTargetRatio = factory;
        storageTargetRatio = storage;
        ruinsTargetRatio = ruins;
        landmarkTargetRatio = landmark;
        specialTargetRatio = special;
    }

    private RuntimeSpawnSafeZone CreateSpawnSafeZone(Terrain[] terrains)
    {
        Vector2 spawnXZ = new Vector2(playerSpawnXZ.x, playerSpawnXZ.z);
        float targetHeight = playerSpawnXZ.y;

        if (RuntimeTerrainUtility.TrySample(terrains, spawnXZ, out RuntimeTerrainSample sample))
        {
            targetHeight = sample.position.y;
        }

        return new RuntimeSpawnSafeZone(
            new Vector3(spawnXZ.x, targetHeight, spawnXZ.y),
            spawnFlatRadius,
            spawnBlendWidth,
            targetHeight);
    }

    private void AddSpawnSafeZoneReservations(
        RuntimeTerrainReservationMask fieldMask,
        RuntimeTerrainReservationMask naturalMask)
    {
        if (lastSpawnSafeZone == null)
        {
            return;
        }

        fieldMask?.Add(lastSpawnSafeZone.GetReservationRect(spawnFieldExclusionPadding));
        naturalMask?.Add(lastSpawnSafeZone.GetReservationRect(0f));
    }

    private List<PlannedFarmField> PlanFarmFields(
        Terrain[] terrains,
        int seed,
        bool reportWarning = true,
        bool collectDebug = true)
    {
        List<PlannedFarmField> fields = new List<PlannedFarmField>();
        RuntimeTerrainReservationMask plannedMask = new RuntimeTerrainReservationMask();
        System.Random random = new System.Random(seed ^ 0x5F3759DF);

        if (collectDebug)
        {
            lastFieldPlacementDebug.Clear();
        }

        if (farmFieldPresets == null || farmFieldPresets.Length == 0 || farmFieldCount <= 0)
        {
            return fields;
        }

        if (lastSpawnSafeZone != null)
        {
            plannedMask.Add(lastSpawnSafeZone.GetReservationRect(spawnFieldExclusionPadding));
        }

        if (useFieldDirector)
        {
            PlanSettlementClusters(terrains, random, fields, plannedMask, collectDebug);
        }

        int maxAttempts = Mathf.Max(fieldPlacementAttempts, farmFieldCount * 200);
        List<FieldCandidate> candidates = useFieldDirector
            ? GenerateDirectorFieldCandidates(terrains, random, maxAttempts)
            : GenerateRandomFieldCandidates(terrains, random, maxAttempts);

        for (int attempt = 0; attempt < candidates.Count && fields.Count < farmFieldCount; attempt++)
        {
            FieldCandidate candidate = candidates[attempt];
            RuntimeFieldPreset preset = PickFarmFieldPresetForCandidate(random, candidate, fields, out float presetScore);

            if (preset == null)
            {
                AddFieldPlacementDebug(collectDebug, candidate.point, RuntimeFieldCategory.Special, RuntimeFieldPlacementDebugStatus.RejectedPreset, 0f);
                continue;
            }

            if (!preset.TryGetRuntimeFootprint(out RuntimeFarmFieldFootprintData footprintData))
            {
                AddFieldPlacementDebug(collectDebug, candidate.point, preset.category, RuntimeFieldPlacementDebugStatus.RejectedPreset, presetScore);
                continue;
            }

            float scale = RuntimeTerrainUtility.NextFloat(random, preset.randomScaleRange.x, preset.randomScaleRange.y);
            float yaw = GetFarmFieldYaw(preset, footprintData.size, scale, random);

            if (!TryCreateFieldPlan(
                    terrains,
                    preset,
                    footprintData,
                    candidate.point,
                    yaw,
                    scale,
                    candidate.distanceFromStart,
                    candidate.normalizedDistanceFromStart,
                    plannedMask,
                    out PlannedFarmField field,
                    out RuntimeFieldPlacementDebugStatus rejectionStatus))
            {
                AddFieldPlacementDebug(collectDebug, candidate.point, preset.category, rejectionStatus, presetScore);
                continue;
            }

            fields.Add(field);
            plannedMask.AddField(field, fieldReservationPadding);
            AddFieldPlacementDebug(collectDebug, field.footprintCenter, field.category, RuntimeFieldPlacementDebugStatus.Placed, presetScore);
        }

        if (useFieldDirector && fields.Count < farmFieldCount)
        {
            FillMissingFarmFieldsRelaxed(terrains, random, fields, plannedMask, collectDebug);
        }

        if (reportWarning && fields.Count < farmFieldCount)
        {
            Debug.LogWarning($"[RuntimeMapCycleManager] Planned {fields.Count}/{farmFieldCount} farm fields. Increase Field Placement Attempts or relax Farm Field slope safety values if this is too low.", this);
        }

        return fields;
    }

    private void FillMissingFarmFieldsRelaxed(
        Terrain[] terrains,
        System.Random random,
        List<PlannedFarmField> fields,
        RuntimeTerrainReservationMask plannedMask,
        bool collectDebug)
    {
        float originalAverageSlope = maxFarmFieldAverageSlopeDegrees;
        float originalPeakSlope = maxFarmFieldPeakSlopeDegrees;
        float originalHeightDelta = maxFarmFieldHeightDelta;
        float originalFlattenCost = maxFarmFieldFlattenCost;
        float originalSingleFlattenCost = maxFarmFieldSingleSampleFlattenCost;

        maxFarmFieldAverageSlopeDegrees = Mathf.Min(90f, maxFarmFieldAverageSlopeDegrees + 8f);
        maxFarmFieldPeakSlopeDegrees = Mathf.Min(90f, maxFarmFieldPeakSlopeDegrees + 12f);
        maxFarmFieldHeightDelta += 8f;
        maxFarmFieldFlattenCost *= 1.75f;
        maxFarmFieldSingleSampleFlattenCost *= 1.5f;

        int relaxedAttempts = Mathf.Max(fieldPlacementAttempts, farmFieldCount * 300);
        List<FieldCandidate> fallbackCandidates = GenerateRandomFieldCandidates(terrains, random, relaxedAttempts);
        for (int attempt = 0; attempt < fallbackCandidates.Count && fields.Count < farmFieldCount; attempt++)
        {
            FieldCandidate candidate = fallbackCandidates[attempt];
            RuntimeFieldPreset preset = PickFarmFieldPresetForCandidate(random, candidate, fields, out float presetScore);
            if (preset == null || !preset.TryGetRuntimeFootprint(out RuntimeFarmFieldFootprintData footprintData))
            {
                continue;
            }

            float scale = RuntimeTerrainUtility.NextFloat(random, preset.randomScaleRange.x, preset.randomScaleRange.y);
            float yaw = GetFarmFieldYaw(preset, footprintData.size, scale, random);
            if (!TryCreateFieldPlan(
                    terrains,
                    preset,
                    footprintData,
                    candidate.point,
                    yaw,
                    scale,
                    candidate.distanceFromStart,
                    candidate.normalizedDistanceFromStart,
                    plannedMask,
                    out PlannedFarmField field,
                    out RuntimeFieldPlacementDebugStatus rejectionStatus))
            {
                AddFieldPlacementDebug(collectDebug, candidate.point, preset.category, rejectionStatus, presetScore);
                continue;
            }

            fields.Add(field);
            plannedMask.AddField(field, fieldReservationPadding);
            AddFieldPlacementDebug(collectDebug, field.footprintCenter, field.category, RuntimeFieldPlacementDebugStatus.Placed, presetScore);
        }

        maxFarmFieldAverageSlopeDegrees = originalAverageSlope;
        maxFarmFieldPeakSlopeDegrees = originalPeakSlope;
        maxFarmFieldHeightDelta = originalHeightDelta;
        maxFarmFieldFlattenCost = originalFlattenCost;
        maxFarmFieldSingleSampleFlattenCost = originalSingleFlattenCost;
    }

    private void PlanSettlementClusters(
        Terrain[] terrains,
        System.Random random,
        List<PlannedFarmField> fields,
        RuntimeTerrainReservationMask plannedMask,
        bool collectDebug)
    {
        if (settlementLayoutStrength <= 0f || CountPresetsByCategory(RuntimeFieldCategory.House) < 1)
        {
            return;
        }

        int desiredClusterCount = Mathf.Clamp(
            Mathf.RoundToInt(farmFieldCount * settlementLayoutStrength / 5f),
            0,
            Mathf.Max(0, farmFieldCount / 2));
        if (desiredClusterCount <= 0)
        {
            return;
        }

        int clusterAttempts = Mathf.Max(desiredClusterCount * 10, 12);
        List<FieldCandidate> clusterCandidates = GenerateDirectorFieldCandidates(terrains, random, clusterAttempts * 4);
        int completedClusters = 0;

        for (int i = 0; i < clusterCandidates.Count && completedClusters < desiredClusterCount && fields.Count < farmFieldCount; i++)
        {
            int remainingSlots = farmFieldCount - fields.Count;
            if (remainingSlots < 2)
            {
                break;
            }

            int targetLots = Mathf.Min(remainingSlots, random.Next(3, 5));
            if (TryPlanSettlementCluster(
                    terrains,
                    random,
                    clusterCandidates[i],
                    targetLots,
                    fields,
                    plannedMask,
                    collectDebug,
                    out List<PlannedFarmField> clusterFields))
            {
                for (int fieldIndex = 0; fieldIndex < clusterFields.Count; fieldIndex++)
                {
                    PlannedFarmField field = clusterFields[fieldIndex];
                    fields.Add(field);
                    plannedMask.AddField(field, fieldReservationPadding);
                    AddFieldPlacementDebug(collectDebug, field.footprintCenter, field.category, RuntimeFieldPlacementDebugStatus.Placed, 1f);
                }

                completedClusters++;
            }
        }
    }

    private bool TryPlanSettlementCluster(
        Terrain[] terrains,
        System.Random random,
        FieldCandidate centerCandidate,
        int targetLots,
        IReadOnlyList<PlannedFarmField> existingFields,
        RuntimeTerrainReservationMask existingMask,
        bool collectDebug,
        out List<PlannedFarmField> clusterFields)
    {
        clusterFields = new List<PlannedFarmField>(targetLots);
        RuntimeTerrainReservationMask clusterMask = CloneReservationMask(existingMask);
        float baseYaw = RuntimeTerrainUtility.NextFloat(random, 0f, 360f);
        int firstRowCount = Mathf.CeilToInt(targetLots * 0.5f);
        int secondRowCount = targetLots - firstRowCount;
        float maxDirectorDistance = CalculateMaxDirectorDistance(terrains);

        for (int lotIndex = 0; lotIndex < targetLots; lotIndex++)
        {
            bool firstRow = lotIndex < firstRowCount;
            int rowIndex = firstRow ? lotIndex : lotIndex - firstRowCount;
            int rowCount = Mathf.Max(1, firstRow ? firstRowCount : secondRowCount);
            float along = (rowIndex - (rowCount - 1) * 0.5f) * settlementLotSpacing;
            along += RuntimeTerrainUtility.NextFloat(random, -settlementLotSpacing * 0.12f, settlementLotSpacing * 0.12f);
            float side = firstRow ? settlementStreetHalfWidth : -settlementStreetHalfWidth;
            side += RuntimeTerrainUtility.NextFloat(random, -2.5f, 2.5f);

            Vector2 localOffset = new Vector2(side, along);
            Vector2 lotPoint = centerCandidate.point + RuntimeTerrainUtility.Rotate(localOffset, baseYaw);
            FieldCandidate lotCandidate = CreateFieldCandidate(lotPoint, maxDirectorDistance);
            RuntimeFieldPreset preset = PickPresetForCategory(RuntimeFieldCategory.House, lotCandidate, existingFields, random);
            if (preset == null || !preset.TryGetRuntimeFootprint(out RuntimeFarmFieldFootprintData footprintData))
            {
                continue;
            }

            float scale = RuntimeTerrainUtility.NextFloat(random, preset.randomScaleRange.x, preset.randomScaleRange.y);
            float rowFacingYaw = firstRow ? baseYaw + 270f : baseYaw + 90f;
            float yaw = preset.randomYaw
                ? rowFacingYaw + RuntimeTerrainUtility.NextFloat(random, -7f, 7f)
                : preset.fixedYawDegrees;

            if (!TryCreateFieldPlan(
                    terrains,
                    preset,
                    footprintData,
                    lotCandidate.point,
                    yaw,
                    scale,
                    lotCandidate.distanceFromStart,
                    lotCandidate.normalizedDistanceFromStart,
                    clusterMask,
                    out PlannedFarmField field,
                    out RuntimeFieldPlacementDebugStatus rejectionStatus))
            {
                AddFieldPlacementDebug(collectDebug, lotPoint, RuntimeFieldCategory.House, rejectionStatus, 1f);
                continue;
            }

            clusterFields.Add(field);
            clusterMask.AddField(field, fieldReservationPadding);
        }

        return clusterFields.Count >= 2;
    }

    private RuntimeFieldPreset PickPresetForCategory(
        RuntimeFieldCategory category,
        FieldCandidate candidate,
        IReadOnlyList<PlannedFarmField> fields,
        System.Random random)
    {
        float totalWeight = 0f;
        for (int i = 0; i < farmFieldPresets.Length; i++)
        {
            RuntimeFieldPreset preset = farmFieldPresets[i];
            if (preset == null || preset.category != category)
            {
                continue;
            }

            totalWeight += CalculateFieldPresetDirectorWeight(preset, candidate, fields);
        }

        if (totalWeight <= 0f)
        {
            return null;
        }

        float pick = RuntimeTerrainUtility.NextFloat(random, 0f, totalWeight);
        float accumulated = 0f;
        for (int i = 0; i < farmFieldPresets.Length; i++)
        {
            RuntimeFieldPreset preset = farmFieldPresets[i];
            if (preset == null || preset.category != category)
            {
                continue;
            }

            accumulated += CalculateFieldPresetDirectorWeight(preset, candidate, fields);
            if (pick <= accumulated)
            {
                return preset;
            }
        }

        return null;
    }

    private int CountPresetsByCategory(RuntimeFieldCategory category)
    {
        if (farmFieldPresets == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < farmFieldPresets.Length; i++)
        {
            if (farmFieldPresets[i] != null && farmFieldPresets[i].category == category)
            {
                count++;
            }
        }

        return count;
    }

    private FieldCandidate CreateFieldCandidate(Vector2 point, float maxDirectorDistance)
    {
        Vector3 startPosition = ResolveDirectorStartPosition();
        float distance = Vector2.Distance(point, new Vector2(startPosition.x, startPosition.z));
        float normalizedDistance = maxDirectorDistance > 0f ? Mathf.Clamp01(distance / maxDirectorDistance) : 0f;
        return new FieldCandidate(point, distance, normalizedDistance);
    }

    private static RuntimeTerrainReservationMask CloneReservationMask(RuntimeTerrainReservationMask source)
    {
        RuntimeTerrainReservationMask clone = new RuntimeTerrainReservationMask();
        if (source == null)
        {
            return clone;
        }

        for (int i = 0; i < source.Areas.Count; i++)
        {
            clone.Add(source.Areas[i].rect);
        }

        return clone;
    }

    private List<FieldCandidate> GenerateDirectorFieldCandidates(Terrain[] terrains, System.Random random, int maxCandidates)
    {
        List<FieldCandidate> candidates = new List<FieldCandidate>(maxCandidates);
        RuntimeSpatialHash2D spatialHash = minFieldCenterDistance > 0f
            ? new RuntimeSpatialHash2D(minFieldCenterDistance)
            : null;
        float maxDirectorDistance = CalculateMaxDirectorDistance(terrains);

        if (terrains != null)
        {
            for (int terrainIndex = 0; terrainIndex < terrains.Length && candidates.Count < maxCandidates; terrainIndex++)
            {
                Terrain terrain = terrains[terrainIndex];
                if (terrain == null || terrain.terrainData == null)
                {
                    continue;
                }

                Rect terrainRect = RuntimeTerrainUtility.GetWorldRect(terrain);
                float sectorWidth = terrainRect.width / Mathf.Max(1, fieldSectorColumns);
                float sectorHeight = terrainRect.height / Mathf.Max(1, fieldSectorRows);

                for (int z = 0; z < fieldSectorRows && candidates.Count < maxCandidates; z++)
                {
                    for (int x = 0; x < fieldSectorColumns && candidates.Count < maxCandidates; x++)
                    {
                        Rect sector = new Rect(
                            terrainRect.xMin + x * sectorWidth,
                            terrainRect.yMin + z * sectorHeight,
                            sectorWidth,
                            sectorHeight);

                        for (int i = 0; i < fieldCandidatesPerSector && candidates.Count < maxCandidates; i++)
                        {
                            Vector2 point = new Vector2(
                                RuntimeTerrainUtility.NextFloat(random, sector.xMin, sector.xMax),
                                RuntimeTerrainUtility.NextFloat(random, sector.yMin, sector.yMax));

                            TryAddFieldCandidate(candidates, spatialHash, point, maxDirectorDistance);
                        }
                    }
                }
            }
        }

        int fillAttempts = maxCandidates * 4;
        for (int i = 0; i < fillAttempts && candidates.Count < maxCandidates; i++)
        {
            Terrain terrain = RuntimeTerrainUtility.ChooseTerrainByArea(terrains, random);
            if (terrain == null || terrain.terrainData == null)
            {
                break;
            }

            Rect terrainRect = RuntimeTerrainUtility.GetWorldRect(terrain);
            Vector2 point = new Vector2(
                RuntimeTerrainUtility.NextFloat(random, terrainRect.xMin, terrainRect.xMax),
                RuntimeTerrainUtility.NextFloat(random, terrainRect.yMin, terrainRect.yMax));

            TryAddFieldCandidate(candidates, spatialHash, point, maxDirectorDistance);
        }

        Shuffle(candidates, random);
        return candidates;
    }

    private List<FieldCandidate> GenerateRandomFieldCandidates(Terrain[] terrains, System.Random random, int maxCandidates)
    {
        List<FieldCandidate> candidates = new List<FieldCandidate>(maxCandidates);
        float maxDirectorDistance = CalculateMaxDirectorDistance(terrains);

        for (int i = 0; i < maxCandidates; i++)
        {
            Terrain terrain = RuntimeTerrainUtility.ChooseTerrainByArea(terrains, random);
            if (terrain == null || terrain.terrainData == null)
            {
                break;
            }

            Rect terrainRect = RuntimeTerrainUtility.GetWorldRect(terrain);
            Vector2 point = new Vector2(
                RuntimeTerrainUtility.NextFloat(random, terrainRect.xMin, terrainRect.xMax),
                RuntimeTerrainUtility.NextFloat(random, terrainRect.yMin, terrainRect.yMax));

            TryAddFieldCandidate(candidates, null, point, maxDirectorDistance);
        }

        return candidates;
    }

    private void TryAddFieldCandidate(
        List<FieldCandidate> candidates,
        RuntimeSpatialHash2D spatialHash,
        Vector2 point,
        float maxDirectorDistance)
    {
        if (spatialHash != null && spatialHash.HasPointWithinDistance(point, minFieldCenterDistance))
        {
            return;
        }

        Vector3 startPosition = ResolveDirectorStartPosition();
        float distance = Vector2.Distance(point, new Vector2(startPosition.x, startPosition.z));
        float normalizedDistance = maxDirectorDistance > 0f ? Mathf.Clamp01(distance / maxDirectorDistance) : 0f;
        candidates.Add(new FieldCandidate(point, distance, normalizedDistance));
        spatialHash?.Add(point);
    }

    private RuntimeFieldPreset PickFarmFieldPresetForCandidate(
        System.Random random,
        FieldCandidate candidate,
        IReadOnlyList<PlannedFarmField> fields,
        out float pickedScore)
    {
        pickedScore = 0f;

        if (farmFieldPresets == null || farmFieldPresets.Length == 0)
        {
            return null;
        }

        float totalWeight = 0f;
        for (int i = 0; i < farmFieldPresets.Length; i++)
        {
            totalWeight += CalculateFieldPresetDirectorWeight(farmFieldPresets[i], candidate, fields);
        }

        if (totalWeight <= 0f)
        {
            return null;
        }

        float pick = RuntimeTerrainUtility.NextFloat(random, 0f, totalWeight);
        float accumulated = 0f;
        RuntimeFieldPreset fallback = null;
        float fallbackScore = 0f;

        for (int i = 0; i < farmFieldPresets.Length; i++)
        {
            RuntimeFieldPreset preset = farmFieldPresets[i];
            float weight = CalculateFieldPresetDirectorWeight(preset, candidate, fields);
            if (weight <= 0f)
            {
                continue;
            }

            fallback = preset;
            fallbackScore = weight;
            accumulated += weight;
            if (pick <= accumulated)
            {
                pickedScore = weight;
                return preset;
            }
        }

        pickedScore = fallbackScore;
        return fallback;
    }

    private float CalculateFieldPresetDirectorWeight(
        RuntimeFieldPreset preset,
        FieldCandidate candidate,
        IReadOnlyList<PlannedFarmField> fields)
    {
        if (preset == null || preset.prefab == null || preset.directorWeight <= 0f)
        {
            return 0f;
        }

        float weight = preset.directorWeight;
        weight *= GetCategoryBudgetMultiplier(preset.category, fields);
        weight *= GetDistanceDifficultyMultiplier(preset, candidate);
        weight *= GetClusterMultiplier(preset.category, candidate.point, fields);
        return Mathf.Max(0f, weight);
    }

    private float GetCategoryBudgetMultiplier(RuntimeFieldCategory category, IReadOnlyList<PlannedFarmField> fields)
    {
        float targetRatio = GetNormalizedCategoryTargetRatio(category);
        if (targetRatio <= 0f)
        {
            return 0f;
        }

        int placedCount = CountFieldsByCategory(fields, category);
        int totalPlaced = fields != null ? fields.Count : 0;
        float expectedCount = Mathf.Max(0.001f, (totalPlaced + 1) * targetRatio);
        float shortageRatio = (expectedCount - placedCount) / expectedCount;
        return Mathf.Clamp(1f + shortageRatio * categoryBudgetCorrectionStrength, 0.05f, 4f);
    }

    private float GetDistanceDifficultyMultiplier(RuntimeFieldPreset preset, FieldCandidate candidate)
    {
        int targetTier = GetTargetDifficultyTier(candidate.distanceFromStart);
        int tierDifference = Mathf.Abs(preset.difficultyTier - targetTier);
        float tierMultiplier = tierDifference == 0
            ? 1f
            : tierDifference == 1
                ? Mathf.Max(0.02f, distanceBandExceptionWeight)
                : Mathf.Max(0.01f, distanceBandExceptionWeight * 0.25f);

        Vector2 preferredRange = preset.preferredDistanceRange01;
        float rangeMultiplier = candidate.normalizedDistanceFromStart >= preferredRange.x &&
            candidate.normalizedDistanceFromStart <= preferredRange.y
                ? 1f
                : Mathf.Max(0.01f, distanceBandExceptionWeight);

        return tierMultiplier * rangeMultiplier;
    }

    private float GetClusterMultiplier(
        RuntimeFieldCategory category,
        Vector2 point,
        IReadOnlyList<PlannedFarmField> fields)
    {
        if (fieldClusterAffinity <= 0f || fieldClusterRadius <= 0f || fields == null)
        {
            return 1f;
        }

        int nearbySameCategoryCount = 0;
        float radiusSqr = fieldClusterRadius * fieldClusterRadius;
        for (int i = 0; i < fields.Count; i++)
        {
            PlannedFarmField field = fields[i];
            if (field == null || field.category != category)
            {
                continue;
            }

            if ((field.footprintCenter - point).sqrMagnitude <= radiusSqr)
            {
                nearbySameCategoryCount++;
            }
        }

        return Mathf.Clamp(1f + nearbySameCategoryCount * fieldClusterAffinity, 1f, 3f);
    }

    private float GetNormalizedCategoryTargetRatio(RuntimeFieldCategory category)
    {
        float total = houseTargetRatio + factoryTargetRatio + storageTargetRatio +
            ruinsTargetRatio + landmarkTargetRatio + specialTargetRatio;

        if (total <= 0f)
        {
            return 1f / 6f;
        }

        return GetRawCategoryTargetRatio(category) / total;
    }

    private float GetRawCategoryTargetRatio(RuntimeFieldCategory category)
    {
        switch (category)
        {
            case RuntimeFieldCategory.House:
                return houseTargetRatio;
            case RuntimeFieldCategory.Factory:
                return factoryTargetRatio;
            case RuntimeFieldCategory.Storage:
                return storageTargetRatio;
            case RuntimeFieldCategory.Ruins:
                return ruinsTargetRatio;
            case RuntimeFieldCategory.Landmark:
                return landmarkTargetRatio;
            case RuntimeFieldCategory.Special:
                return specialTargetRatio;
            default:
                return 0f;
        }
    }

    private static int CountFieldsByCategory(IReadOnlyList<PlannedFarmField> fields, RuntimeFieldCategory category)
    {
        if (fields == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < fields.Count; i++)
        {
            if (fields[i] != null && fields[i].category == category)
            {
                count++;
            }
        }

        return count;
    }

    private int GetTargetDifficultyTier(float distanceFromStart)
    {
        if (distanceFromStart <= nearFieldDistance)
        {
            return 0;
        }

        if (distanceFromStart <= midFieldDistance)
        {
            return 1;
        }

        return 2;
    }

    private float CalculateMaxDirectorDistance(Terrain[] terrains)
    {
        if (terrains == null || terrains.Length == 0)
        {
            return 720f;
        }

        Vector3 startPosition = ResolveDirectorStartPosition();
        Vector2 start = new Vector2(startPosition.x, startPosition.z);
        float maxDistance = 1f;

        for (int i = 0; i < terrains.Length; i++)
        {
            Terrain terrain = terrains[i];
            if (terrain == null || terrain.terrainData == null)
            {
                continue;
            }

            Rect rect = RuntimeTerrainUtility.GetWorldRect(terrain);
            maxDistance = Mathf.Max(maxDistance, Vector2.Distance(start, new Vector2(rect.xMin, rect.yMin)));
            maxDistance = Mathf.Max(maxDistance, Vector2.Distance(start, new Vector2(rect.xMin, rect.yMax)));
            maxDistance = Mathf.Max(maxDistance, Vector2.Distance(start, new Vector2(rect.xMax, rect.yMin)));
            maxDistance = Mathf.Max(maxDistance, Vector2.Distance(start, new Vector2(rect.xMax, rect.yMax)));
        }

        return maxDistance;
    }

    private Vector3 ResolveDirectorStartPosition()
    {
        if (lastSpawnSafeZone != null)
        {
            return lastSpawnSafeZone.center;
        }

        PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        PlayerController fallback = null;

        for (int i = 0; i < players.Length; i++)
        {
            PlayerController player = players[i];
            if (player == null)
            {
                continue;
            }

            fallback ??= player;
            if (player.IsLocalPlayer)
            {
                return player.transform.position;
            }
        }

        return fallback != null ? fallback.transform.position : directorFallbackStartPosition;
    }

    private void AddFieldPlacementDebug(
        bool collectDebug,
        Vector2 point,
        RuntimeFieldCategory category,
        RuntimeFieldPlacementDebugStatus status,
        float score)
    {
        if (!collectDebug || maxDebugCandidateGizmos <= 0)
        {
            return;
        }

        if (status != RuntimeFieldPlacementDebugStatus.Placed && !drawRejectedFieldCandidates)
        {
            return;
        }

        if (lastFieldPlacementDebug.Count >= maxDebugCandidateGizmos)
        {
            lastFieldPlacementDebug.RemoveAt(0);
        }

        lastFieldPlacementDebug.Add(new RuntimeFieldPlacementDebugPoint(point, category, status, score));
    }

    private static void Shuffle<T>(List<T> values, System.Random random)
    {
        if (values == null)
        {
            return;
        }

        for (int i = values.Count - 1; i > 0; i--)
        {
            int swapIndex = random.Next(0, i + 1);
            T value = values[i];
            values[i] = values[swapIndex];
            values[swapIndex] = value;
        }
    }

    private bool TryCreateFieldPlan(
        Terrain[] terrains,
        RuntimeFieldPreset preset,
        RuntimeFarmFieldFootprintData footprintData,
        Vector2 footprintCenter,
        float yaw,
        float scale,
        float distanceFromStart,
        float normalizedDistanceFromStart,
        RuntimeTerrainReservationMask plannedMask,
        out PlannedFarmField field,
        out RuntimeFieldPlacementDebugStatus rejectionStatus)
    {
        field = null;
        rejectionStatus = RuntimeFieldPlacementDebugStatus.RejectedTerrain;

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
                        rejectionStatus = RuntimeFieldPlacementDebugStatus.RejectedTerrain;
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
            rejectionStatus = RuntimeFieldPlacementDebugStatus.RejectedTerrain;
            return false;
        }

        float averageHeight = heightSum / sampleCount;
        float targetHeight = CalculateMedianHeight(heightSamples, sampleCount);
        AssignPartTargetHeights(plannedParts, targetHeight, footprintData.groundLocalY, safeScale);
        float averageSlope = slopeSum / sampleCount;
        float heightDelta = maxHeight - minHeight;
        float flattenCost = 0f;
        float peakFlattenCost = 0f;
        for (int i = 0; i < sampleCount; i++)
        {
            float sampleFlattenCost = Mathf.Abs(heightSamples[i] - targetHeight);
            flattenCost += sampleFlattenCost;
            peakFlattenCost = Mathf.Max(peakFlattenCost, sampleFlattenCost);
        }

        flattenCost /= sampleCount;

        if (averageHeight < preset.minWorldHeight || averageHeight > preset.maxWorldHeight)
        {
            rejectionStatus = RuntimeFieldPlacementDebugStatus.RejectedTerrain;
            return false;
        }

        if (useFarmFieldSlopeSafetyFilter &&
            (averageSlope > maxFarmFieldAverageSlopeDegrees ||
             maxSlope > maxFarmFieldPeakSlopeDegrees ||
             heightDelta > GetAllowedFarmFieldHeightDelta(footprint)))
        {
            rejectionStatus = RuntimeFieldPlacementDebugStatus.RejectedTerrain;
            return false;
        }

        if (preset.useHeightDeltaFilter && heightDelta > preset.maxHeightDelta)
        {
            rejectionStatus = RuntimeFieldPlacementDebugStatus.RejectedTerrain;
            return false;
        }

        if (preset.useSlopeFilter && maxSlope > preset.maxSlopeDegrees)
        {
            rejectionStatus = RuntimeFieldPlacementDebugStatus.RejectedTerrain;
            return false;
        }

        float allowedFlattenCost = preset.maxFlattenCostOverride > 0f
            ? preset.maxFlattenCostOverride
            : maxFarmFieldFlattenCost;
        if (allowedFlattenCost > 0f && flattenCost > allowedFlattenCost)
        {
            rejectionStatus = RuntimeFieldPlacementDebugStatus.RejectedNaturalFit;
            return false;
        }

        if (maxFarmFieldSingleSampleFlattenCost > 0f && peakFlattenCost > maxFarmFieldSingleSampleFlattenCost)
        {
            rejectionStatus = RuntimeFieldPlacementDebugStatus.RejectedNaturalFit;
            return false;
        }

        Rect rect = GetUnionRect(plannedParts);
        Rect paddedRect = RuntimeTerrainReservationMask.Pad(rect, fieldReservationPadding);

        if (plannedMask.Overlaps(paddedRect))
        {
            rejectionStatus = RuntimeFieldPlacementDebugStatus.RejectedReservation;
            return false;
        }

        Rect occupiedRect = rect;
        Vector2 scaledCenterOffset = footprintData.centerOffset * safeScale;
        Vector2 pivotXZ = footprintCenter - RuntimeTerrainUtility.Rotate(scaledCenterOffset, yaw);
        float pivotY = targetHeight - footprintData.groundLocalY * safeScale;
        Vector3 position = new Vector3(pivotXZ.x, pivotY, pivotXZ.y);
        field = new PlannedFarmField(
            preset,
            preset.category,
            position,
            Quaternion.Euler(0f, yaw, 0f),
            footprint,
            footprintCenter,
            plannedParts,
            occupiedRect,
            targetHeight,
            yaw,
            scale,
            preset.difficultyTier,
            distanceFromStart,
            normalizedDistanceFromStart,
            flattenCost);

        if (!PassesAccessPointChecks(terrains, field))
        {
            rejectionStatus = RuntimeFieldPlacementDebugStatus.RejectedAccess;
            field = null;
            return false;
        }

        return true;
    }

    private bool PassesAccessPointChecks(Terrain[] terrains, PlannedFarmField field)
    {
        if (field == null || field.preset == null || field.preset.prefab == null)
        {
            return true;
        }

        RuntimeFieldAccessPointAuthoring[] accessPoints =
            field.preset.prefab.GetComponentsInChildren<RuntimeFieldAccessPointAuthoring>(true);
        if (accessPoints == null || accessPoints.Length == 0)
        {
            return true;
        }

        Transform prefabRoot = field.preset.prefab.transform;
        Vector2 pivotXZ = new Vector2(field.position.x, field.position.z);

        for (int i = 0; i < accessPoints.Length; i++)
        {
            RuntimeFieldAccessPointAuthoring accessPoint = accessPoints[i];
            if (accessPoint == null)
            {
                continue;
            }

            Vector3 localPoint = prefabRoot.InverseTransformPoint(accessPoint.transform.position) * field.scale;
            Vector2 worldXZ = pivotXZ + RuntimeTerrainUtility.Rotate(new Vector2(localPoint.x, localPoint.z), field.yawDegrees);

            if (!RuntimeTerrainUtility.TrySample(terrains, worldXZ, out RuntimeTerrainSample sample))
            {
                return false;
            }

            float weight = CalculateFieldFlattenWeight(field, worldXZ, out float targetWorldHeight);
            float expectedGroundY = field.position.y + localPoint.y;
            float terrainYAfterFlatten = Mathf.Lerp(sample.position.y, targetWorldHeight, weight);

            if (Mathf.Abs(terrainYAfterFlatten - expectedGroundY) > accessPoint.AllowedVerticalDifference)
            {
                return false;
            }

            if (weight < 0.95f && sample.slopeDegrees > accessPoint.MaxSlopeDegrees)
            {
                return false;
            }
        }

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
                yaw + part.yawDegrees,
                part.groundLocalY * scale);
        }

        return plannedParts;
    }

    private static void AssignPartTargetHeights(
        PlannedFieldFootprintPart[] parts,
        float fieldTargetHeight,
        float fieldGroundLocalY,
        float scale)
    {
        if (parts == null)
        {
            return;
        }

        float scaledFieldGroundLocalY = fieldGroundLocalY * scale;
        for (int i = 0; i < parts.Length; i++)
        {
            PlannedFieldFootprintPart part = parts[i];
            part.targetWorldHeight = fieldTargetHeight + part.groundLocalY - scaledFieldGroundLocalY;
            parts[i] = part;
        }
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

    private IEnumerator EnsureArcticMountainReliefRoutine(
        Terrain[] terrains,
        RuntimeTerrainReservationMask protectedMask,
        int seed)
    {
        if (terrains == null || terrains.Length == 0 || arcticMountainPresence <= 0f)
        {
            yield break;
        }

        if (!TryAnalyzeTerrainHeightRange(terrains, out float heightRange) ||
            heightRange >= minimumArcticReliefRange)
        {
            yield break;
        }

        float missingRelief01 = Mathf.Clamp01((minimumArcticReliefRange - heightRange) / Mathf.Max(1f, minimumArcticReliefRange));
        float amplitude = arcticReliefHeight * Mathf.Lerp(0.45f, 1f, missingRelief01);
        if (amplitude <= 0.01f)
        {
            yield break;
        }

        float frameStart = Time.realtimeSinceStartup;
        Vector3 startPosition = ResolveDirectorStartPosition();
        Vector2 startXZ = new Vector2(startPosition.x, startPosition.z);

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
            float[,] heights = data.GetHeights(0, 0, resolution, resolution);
            bool changed = false;

            for (int z = 0; z < resolution; z++)
            {
                float worldZ = terrainPosition.z + z / (float)(resolution - 1) * size.z;
                for (int x = 0; x < resolution; x++)
                {
                    float worldX = terrainPosition.x + x / (float)(resolution - 1) * size.x;
                    Vector2 worldXZ = new Vector2(worldX, worldZ);

                    if (protectedMask != null && protectedMask.Contains(worldXZ, arcticReliefFieldPadding))
                    {
                        continue;
                    }

                    float startDistance = Vector2.Distance(worldXZ, startXZ);
                    float startMask = Mathf.SmoothStep(
                        0f,
                        1f,
                        Mathf.InverseLerp(arcticReliefStartSafeRadius, arcticReliefStartSafeRadius * 1.8f, startDistance));
                    if (startMask <= 0f)
                    {
                        continue;
                    }

                    float mountainNoise = CalculateArcticMountainNoise(worldXZ, seed);
                    float mountainMask = Mathf.SmoothStep(
                        0f,
                        1f,
                        Mathf.InverseLerp(arcticReliefNoiseThreshold, 1f, mountainNoise));

                    if (mountainMask <= 0f)
                    {
                        continue;
                    }

                    float addWorldHeight = amplitude * mountainMask * startMask;
                    heights[z, x] = Mathf.Clamp01(heights[z, x] + addWorldHeight / Mathf.Max(1f, size.y));
                    changed = true;
                }

                if (IsOverFrameBudget(frameStart))
                {
                    yield return null;
                    frameStart = Time.realtimeSinceStartup;
                }
            }

            if (changed)
            {
                if (useDelayedHeightmapLod)
                {
                    data.SetHeightsDelayLOD(0, 0, heights);
                    data.SyncHeightmap();
                }
                else
                {
                    data.SetHeights(0, 0, heights);
                }

                terrain.Flush();
                yield return null;
                frameStart = Time.realtimeSinceStartup;
            }
        }
    }

    private static bool TryAnalyzeTerrainHeightRange(Terrain[] terrains, out float heightRange)
    {
        heightRange = 0f;
        if (terrains == null || terrains.Length == 0)
        {
            return false;
        }

        float minHeight = float.MaxValue;
        float maxHeight = float.MinValue;
        int sampleCount = 0;

        for (int terrainIndex = 0; terrainIndex < terrains.Length; terrainIndex++)
        {
            Terrain terrain = terrains[terrainIndex];
            if (terrain == null || terrain.terrainData == null)
            {
                continue;
            }

            TerrainData data = terrain.terrainData;
            int resolution = data.heightmapResolution;
            int step = Mathf.Max(1, resolution / 32);
            Vector3 terrainPosition = terrain.transform.position;
            Vector3 size = data.size;

            for (int z = 0; z < resolution; z += step)
            {
                float normalizedZ = z / (float)(resolution - 1);
                for (int x = 0; x < resolution; x += step)
                {
                    float normalizedX = x / (float)(resolution - 1);
                    float height = data.GetInterpolatedHeight(normalizedX, normalizedZ) + terrainPosition.y;
                    minHeight = Mathf.Min(minHeight, height);
                    maxHeight = Mathf.Max(maxHeight, height);
                    sampleCount++;
                }
            }
        }

        if (sampleCount == 0)
        {
            return false;
        }

        heightRange = maxHeight - minHeight;
        return true;
    }

    private float CalculateArcticMountainNoise(Vector2 worldXZ, int seed)
    {
        float seedOffset = (seed & 0xFFFF) * 0.0137f;
        float x = (worldXZ.x + seedOffset) * arcticReliefNoiseScale;
        float y = (worldXZ.y - seedOffset) * arcticReliefNoiseScale;
        float broad = Mathf.PerlinNoise(x, y);
        float ridgeSource = Mathf.PerlinNoise(x * 2.1f + 19.3f, y * 2.1f - 31.7f);
        float ridge = 1f - Mathf.Abs(ridgeSource * 2f - 1f);
        return Mathf.Clamp01(broad * 0.55f + ridge * 0.45f);
    }

    private IEnumerator ApplySpawnSafeZoneFlatteningRoutine(Terrain[] terrains, RuntimeSpawnSafeZone safeZone)
    {
        if (safeZone == null || terrains == null || terrains.Length == 0)
        {
            yield break;
        }

        float affectedRadius = safeZone.flatRadius + safeZone.blendWidth;
        if (affectedRadius <= 0f)
        {
            yield break;
        }

        Rect affectedRect = safeZone.GetReservationRect(0f);
        float frameStart = Time.realtimeSinceStartup;
        Vector2 safeCenter = new Vector2(safeZone.center.x, safeZone.center.z);

        for (int terrainIndex = 0; terrainIndex < terrains.Length; terrainIndex++)
        {
            Terrain terrain = terrains[terrainIndex];
            if (terrain == null || terrain.terrainData == null)
            {
                continue;
            }

            Rect terrainRect = RuntimeTerrainUtility.GetWorldRect(terrain);
            if (!terrainRect.Overlaps(affectedRect))
            {
                continue;
            }

            TerrainData data = terrain.terrainData;
            int resolution = data.heightmapResolution;
            Vector3 terrainPosition = terrain.transform.position;
            Vector3 size = data.size;
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
            bool changed = false;

            for (int z = 0; z < height; z++)
            {
                float worldZ = terrainPosition.z + (zMin + z) / (float)(resolution - 1) * size.z;
                for (int x = 0; x < width; x++)
                {
                    float worldX = terrainPosition.x + (xMin + x) / (float)(resolution - 1) * size.x;
                    float distance = Vector2.Distance(new Vector2(worldX, worldZ), safeCenter);
                    float weight = CalculateSpawnSafeZoneFlattenWeight(distance, safeZone.flatRadius, safeZone.blendWidth);

                    if (weight <= 0f)
                    {
                        continue;
                    }

                    float targetHeight = Mathf.InverseLerp(terrainPosition.y, terrainPosition.y + size.y, safeZone.targetWorldHeight);
                    heights[z, x] = Mathf.Lerp(heights[z, x], targetHeight, weight);
                    changed = true;
                }

                if (IsOverFrameBudget(frameStart))
                {
                    yield return null;
                    frameStart = Time.realtimeSinceStartup;
                }
            }

            if (changed)
            {
                if (useDelayedHeightmapLod)
                {
                    data.SetHeightsDelayLOD(xMin, zMin, heights);
                    data.SyncHeightmap();
                }
                else
                {
                    data.SetHeights(xMin, zMin, heights);
                }

                terrain.Flush();
                yield return null;
                frameStart = Time.realtimeSinceStartup;
            }
        }
    }

    private static float CalculateSpawnSafeZoneFlattenWeight(float distance, float flatRadius, float blendWidth)
    {
        if (distance <= flatRadius)
        {
            return 1f;
        }

        if (blendWidth <= 0f || distance >= flatRadius + blendWidth)
        {
            return 0f;
        }

        float t = Mathf.Clamp01((distance - flatRadius) / blendWidth);
        return 1f - Mathf.SmoothStep(0f, 1f, t);
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
                bool patchChanged = false;

                for (int z = 0; z < height; z++)
                {
                    float worldZ = terrainPosition.z + (zMin + z) / (float)(resolution - 1) * size.z;
                    for (int x = 0; x < width; x++)
                    {
                        float worldX = terrainPosition.x + (xMin + x) / (float)(resolution - 1) * size.x;
                        float weight = CalculateFieldFlattenWeight(field, new Vector2(worldX, worldZ), out float targetWorldHeight);

                        if (weight <= 0f)
                        {
                            continue;
                        }

                        float targetHeight = Mathf.InverseLerp(terrainPosition.y, terrainPosition.y + size.y, targetWorldHeight);
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
        if (!HasAnyNaturalScatterPrefab() || targetTreeCount <= 0)
        {
            yield break;
        }

        Transform parent = RequiresNaturalScatterObjectParent() ? CreateRuntimeGroup("Natural Scatter") : null;
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
            if (!TryPickNaturalScatterChoice(random, out NaturalScatterChoice choice))
            {
                break;
            }

            Terrain terrain = RuntimeTerrainUtility.ChooseTerrainByArea(terrains, random);
            if (terrain == null || terrain.terrainData == null)
            {
                continue;
            }

            Rect terrainRect = RuntimeTerrainUtility.GetWorldRect(terrain);
            Vector2 worldXZ = new Vector2(
                RuntimeTerrainUtility.NextFloat(random, terrainRect.xMin, terrainRect.xMax),
                RuntimeTerrainUtility.NextFloat(random, terrainRect.yMin, terrainRect.yMax));

            if (!TryGetNaturalScatterDensity(worldXZ, seed, random, out float scatterDensity))
            {
                continue;
            }

            int spawnedBefore = spawnedCount;
            float densityAdjustedMinDistance = GetDensityAdjustedNaturalScatterDistance(scatterDensity);
            if (!TryPlaceNaturalScatter(
                    terrains,
                    mask,
                    spatialHash,
                    terrainTreeAdds,
                    parent,
                    choice,
                    worldXZ,
                    densityAdjustedMinDistance,
                    random,
                    ref spawnedCount))
            {
                continue;
            }

            int clusterCount = GetNaturalScatterClusterCount(choice, random);
            float clusterMinDistance = Mathf.Min(densityAdjustedMinDistance, Mathf.Max(1f, choice.clusterRadius * 0.35f));
            for (int clusterIndex = 1; clusterIndex < clusterCount && spawnedCount < targetTreeCount; clusterIndex++)
            {
                float angle = RuntimeTerrainUtility.NextFloat(random, 0f, Mathf.PI * 2f);
                float radius = Mathf.Sqrt(RuntimeTerrainUtility.NextFloat(random, 0f, 1f)) * choice.clusterRadius;
                Vector2 clusterPoint = worldXZ + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

                TryPlaceNaturalScatter(
                    terrains,
                    mask,
                    spatialHash,
                    terrainTreeAdds,
                    parent,
                    choice,
                    clusterPoint,
                    clusterMinDistance,
                    random,
                    ref spawnedCount);
            }

            spawnedThisFrame += spawnedCount - spawnedBefore;
            if (spawnedThisFrame >= objectSpawnsPerFrame || IsOverFrameBudget(frameStart))
            {
                spawnedThisFrame = 0;
                yield return null;
                frameStart = Time.realtimeSinceStartup;
            }
        }

        if (spawnedCount < targetTreeCount)
        {
            Debug.LogWarning($"[RuntimeMapCycleManager] Spawned {spawnedCount}/{targetTreeCount} natural scatter objects. Increase attempts or relax filters if this is too low.", this);
        }

        if (treeSpawnMode == TreeSpawnMode.TerrainTreeInstances)
        {
            yield return ApplyTerrainTreeInstancesRoutine(terrainTreeAdds);
        }
    }

    private bool TryPlaceNaturalScatter(
        Terrain[] terrains,
        RuntimeTerrainReservationMask mask,
        RuntimeSpatialHash2D spatialHash,
        Dictionary<Terrain, List<TreeInstance>> terrainTreeAdds,
        Transform parent,
        NaturalScatterChoice choice,
        Vector2 worldXZ,
        float minimumDistance,
        System.Random random,
        ref int spawnedCount)
    {
        GameObject prefab = PickNaturalScatterPrefab(choice, random);
        if (prefab == null)
        {
            return false;
        }

        if (mask != null && mask.Contains(worldXZ, treeFieldPadding))
        {
            return false;
        }

        if (!RuntimeTerrainUtility.TrySample(terrains, worldXZ, out RuntimeTerrainSample sample))
        {
            return false;
        }

        if (sample.position.y < minTreeWorldHeight || sample.position.y > maxTreeWorldHeight)
        {
            return false;
        }

        if (sample.slopeDegrees > maxTreeSlopeDegrees)
        {
            return false;
        }

        if (spatialHash != null && spatialHash.HasPointWithinDistance(worldXZ, minimumDistance))
        {
            return false;
        }

        spatialHash?.Add(worldXZ);
        spawnedCount++;

        Quaternion yaw = Quaternion.Euler(0f, RuntimeTerrainUtility.NextFloat(random, 0f, 360f), 0f);
        Vector2 scaleRange = ValidateScaleRange(choice.scaleRange);
        float scale = RuntimeTerrainUtility.NextFloat(random, scaleRange.x, scaleRange.y);

        if (choice.spawnAsTerrainTreeInstance)
        {
            AddPlannedTerrainTree(terrainTreeAdds, sample.terrain, prefab, sample.position, yaw.eulerAngles.y, scale);
            return true;
        }

        Vector3 position = sample.position + Vector3.up * treeYOffset;
        Quaternion tilt = GetNaturalScatterTilt(choice, random);
        Quaternion rotation = choice.alignToTerrainNormal
            ? Quaternion.FromToRotation(Vector3.up, sample.normal) * yaw * tilt
            : yaw * tilt;

        GameObject instance = Instantiate(prefab, position, rotation, parent);
        instance.transform.localScale = instance.transform.localScale * scale;
        instance.name = $"{prefab.name}_{choice.kind}_{spawnedCount:0000}";
        return true;
    }

    private bool TryPickNaturalScatterChoice(System.Random random, out NaturalScatterChoice choice)
    {
        choice = default;
        float totalWeight = 0f;

        if (naturalScatterPresets != null)
        {
            for (int i = 0; i < naturalScatterPresets.Length; i++)
            {
                RuntimeNaturalScatterPreset preset = naturalScatterPresets[i];
                if (!IsNaturalScatterPresetUsable(preset))
                {
                    continue;
                }

                totalWeight += preset.weight;
            }
        }

        if (totalWeight > 0f)
        {
            float pick = RuntimeTerrainUtility.NextFloat(random, 0f, totalWeight);
            float accumulated = 0f;

            for (int i = 0; i < naturalScatterPresets.Length; i++)
            {
                RuntimeNaturalScatterPreset preset = naturalScatterPresets[i];
                if (!IsNaturalScatterPresetUsable(preset))
                {
                    continue;
                }

                accumulated += preset.weight;
                if (pick <= accumulated)
                {
                    choice = CreateNaturalScatterChoice(preset);
                    return true;
                }
            }
        }

        return false;
    }

    private NaturalScatterChoice CreateNaturalScatterChoice(RuntimeNaturalScatterPreset preset)
    {
        float clusterChance = preset.clusterChance;
        int maxClusterCount = Mathf.Max(1, preset.maxClusterCount);
        float clusterRadius = Mathf.Max(0f, preset.clusterRadius);

        if (preset.kind == RuntimeNaturalScatterKind.Rock)
        {
            clusterChance = clusterChance > 0f ? clusterChance : rockClusterChance;
            maxClusterCount = maxClusterCount > 1 ? maxClusterCount : rockClusterMaxCount;
            clusterRadius = clusterRadius > 0f ? clusterRadius : rockClusterRadius;
        }

        return new NaturalScatterChoice
        {
            prefabs = BuildNaturalScatterPrefabList(preset),
            kind = preset.kind,
            spawnAsTerrainTreeInstance = CanSpawnPresetAsTerrainTreeInstance(preset),
            alignToTerrainNormal = alignTreesToTerrainNormal || preset.alignToTerrainNormal,
            scaleRange = preset.useGlobalScaleRange ? treeScaleRange : preset.scaleRange,
            randomTiltDegrees = GetNaturalScatterTiltDegrees(preset),
            clusterChance = clusterChance,
            maxClusterCount = maxClusterCount,
            clusterRadius = clusterRadius
        };
    }

    private bool IsNaturalScatterPresetUsable(RuntimeNaturalScatterPreset preset)
    {
        return preset != null && preset.weight > 0f && HasAnyNaturalScatterPrefab(preset);
    }

    private bool HasAnyNaturalScatterPrefab()
    {
        if (naturalScatterPresets != null)
        {
            for (int i = 0; i < naturalScatterPresets.Length; i++)
            {
                if (IsNaturalScatterPresetUsable(naturalScatterPresets[i]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool RequiresNaturalScatterObjectParent()
    {
        if (treeSpawnMode == TreeSpawnMode.GameObjects)
        {
            return true;
        }

        if (naturalScatterPresets == null)
        {
            return false;
        }

        for (int i = 0; i < naturalScatterPresets.Length; i++)
        {
            RuntimeNaturalScatterPreset preset = naturalScatterPresets[i];
            if (IsNaturalScatterPresetUsable(preset) && !CanSpawnPresetAsTerrainTreeInstance(preset))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasAnyNaturalScatterPrefab(RuntimeNaturalScatterPreset preset)
    {
        if (preset == null)
        {
            return false;
        }

        if (preset.prefab != null)
        {
            return true;
        }

        if (preset.prefabVariants == null)
        {
            return false;
        }

        for (int i = 0; i < preset.prefabVariants.Length; i++)
        {
            if (preset.prefabVariants[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private static GameObject[] BuildNaturalScatterPrefabList(RuntimeNaturalScatterPreset preset)
    {
        if (preset == null)
        {
            return System.Array.Empty<GameObject>();
        }

        List<GameObject> prefabs = new List<GameObject>();
        if (preset.prefab != null)
        {
            prefabs.Add(preset.prefab);
        }

        if (preset.prefabVariants != null)
        {
            for (int i = 0; i < preset.prefabVariants.Length; i++)
            {
                GameObject variant = preset.prefabVariants[i];
                if (variant != null && !prefabs.Contains(variant))
                {
                    prefabs.Add(variant);
                }
            }
        }

        return prefabs.ToArray();
    }

    private GameObject PickNaturalScatterPrefab(NaturalScatterChoice choice, System.Random random)
    {
        if (choice.prefabs == null || choice.prefabs.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < choice.prefabs.Length; i++)
        {
            GameObject prefab = choice.prefabs[random.Next(0, choice.prefabs.Length)];
            if (prefab != null)
            {
                return prefab;
            }
        }

        return null;
    }

    private bool CanSpawnPresetAsTerrainTreeInstance(RuntimeNaturalScatterPreset preset)
    {
        return treeSpawnMode == TreeSpawnMode.TerrainTreeInstances
            && preset != null
            && preset.allowTerrainTreeInstance
            && !preset.alignToTerrainNormal
            && preset.kind != RuntimeNaturalScatterKind.Rock;
    }

    private static float GetNaturalScatterTiltDegrees(RuntimeNaturalScatterPreset preset)
    {
        if (preset == null)
        {
            return 0f;
        }

        if (preset.randomTiltDegrees > 0f)
        {
            return preset.randomTiltDegrees;
        }

        return preset.kind == RuntimeNaturalScatterKind.Rock ? 10f : 0f;
    }

    private static Quaternion GetNaturalScatterTilt(NaturalScatterChoice choice, System.Random random)
    {
        if (choice.randomTiltDegrees <= 0f)
        {
            return Quaternion.identity;
        }

        float pitch = RuntimeTerrainUtility.NextFloat(random, -choice.randomTiltDegrees, choice.randomTiltDegrees);
        float roll = RuntimeTerrainUtility.NextFloat(random, -choice.randomTiltDegrees, choice.randomTiltDegrees);
        return Quaternion.Euler(pitch, 0f, roll);
    }

    private bool TryGetNaturalScatterDensity(Vector2 worldXZ, int seed, System.Random random, out float density)
    {
        density = 1f;
        if (!useNaturalScatterNoise)
        {
            return true;
        }

        float seedOffset = (seed & 0xFFFF) * 0.0173f;
        float forestPatch = GetFractalNoise(
            (worldXZ.x + seedOffset) * naturalScatterNoiseScale,
            (worldXZ.y - seedOffset) * naturalScatterNoiseScale);

        float localVariation = Mathf.PerlinNoise(
            (worldXZ.x - seedOffset * 1.7f) * naturalScatterDetailNoiseScale,
            (worldXZ.y + seedOffset * 1.3f) * naturalScatterDetailNoiseScale);

        float corridorNoise = Mathf.PerlinNoise(
            (worldXZ.x + seedOffset * 2.1f) * naturalScatterCorridorNoiseScale,
            (worldXZ.y - seedOffset * 2.4f) * naturalScatterCorridorNoiseScale);

        float patchDensity = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(naturalScatterNoiseThreshold, 1f, forestPatch));
        float detailMultiplier = Mathf.Lerp(0.72f, 1.18f, localVariation);
        float corridorCenter = 1f - Mathf.Clamp01(Mathf.Abs(corridorNoise - 0.5f) / 0.18f);
        float corridorReduction = Mathf.Lerp(1f, 1f - corridorCenter * 0.82f, naturalScatterCorridorStrength);

        density = Mathf.Clamp01(patchDensity * detailMultiplier * corridorReduction);
        float acceptChance = Mathf.Lerp(naturalScatterMinimumDensity, 1f, density);
        acceptChance = Mathf.Lerp(1f, acceptChance, naturalScatterNoiseStrength);
        return random.NextDouble() <= acceptChance;
    }

    private float GetDensityAdjustedNaturalScatterDistance(float density)
    {
        if (minTreeDistance <= 0f)
        {
            return 0f;
        }

        return Mathf.Lerp(minTreeDistance * 1.35f, minTreeDistance * 0.55f, Mathf.Clamp01(density));
    }

    private static float GetFractalNoise(float x, float y)
    {
        float baseNoise = Mathf.PerlinNoise(x, y);
        float broadNoise = Mathf.PerlinNoise(x * 0.43f + 37.1f, y * 0.43f - 19.7f);
        float fineNoise = Mathf.PerlinNoise(x * 2.15f - 11.3f, y * 2.15f + 5.9f);
        return Mathf.Clamp01(baseNoise * 0.58f + broadNoise * 0.28f + fineNoise * 0.14f);
    }

    private int GetNaturalScatterClusterCount(NaturalScatterChoice choice, System.Random random)
    {
        if (choice.maxClusterCount <= 1 || choice.clusterChance <= 0f || choice.clusterRadius <= 0f)
        {
            return 1;
        }

        if (random.NextDouble() > choice.clusterChance)
        {
            return 1;
        }

        return random.Next(2, choice.maxClusterCount + 1);
    }

    private static Vector2 ValidateScaleRange(Vector2 scaleRange)
    {
        if (scaleRange.x <= 0f)
        {
            scaleRange.x = 1f;
        }

        if (scaleRange.y <= 0f)
        {
            scaleRange.y = scaleRange.x;
        }

        if (scaleRange.y < scaleRange.x)
        {
            scaleRange.y = scaleRange.x;
        }

        return scaleRange;
    }

    private bool IsConfiguredNaturalScatterPrefab(GameObject prefab, bool terrainTreeOnly)
    {
        if (prefab == null)
        {
            return false;
        }

        if (naturalScatterPresets != null)
        {
            for (int i = 0; i < naturalScatterPresets.Length; i++)
            {
                RuntimeNaturalScatterPreset preset = naturalScatterPresets[i];
                if (preset == null)
                {
                    continue;
                }

                if (terrainTreeOnly && !CanSpawnPresetAsTerrainTreeInstance(preset))
                {
                    continue;
                }

                if (preset.prefab == prefab)
                {
                    return true;
                }

                if (preset.prefabVariants == null)
                {
                    continue;
                }

                for (int variantIndex = 0; variantIndex < preset.prefabVariants.Length; variantIndex++)
                {
                    if (preset.prefabVariants[variantIndex] == prefab)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
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
        if (terrains == null || !HasAnyNaturalScatterPrefab())
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
        return IsConfiguredNaturalScatterPrefab(prefab, terrainTreeOnly: true);
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

    private float CalculateFieldFlattenWeight(PlannedFarmField field, Vector2 worldXZ, out float targetWorldHeight)
    {
        float blendWidth = field.preset != null ? field.preset.flattenBlendWidth : 0f;
        targetWorldHeight = field.targetWorldHeight;

        if (field.footprintParts == null || field.footprintParts.Length == 0)
        {
            Vector2 local = RuntimeTerrainUtility.Rotate(worldXZ - field.footprintCenter, -field.yawDegrees);
            Vector2 half = field.footprintSize * 0.5f;
            return CalculateRectFlattenWeight(local, half, blendWidth, terrainBlendIrregularity, flattenBlendNoiseScale);
        }

        float weight = 0f;
        for (int i = 0; i < field.footprintParts.Length; i++)
        {
            PlannedFieldFootprintPart part = field.footprintParts[i];
            Vector2 local = RuntimeTerrainUtility.Rotate(worldXZ - part.center, -part.yawDegrees);
            Vector2 half = part.size * 0.5f;
            float partWeight = CalculateRectFlattenWeight(local, half, blendWidth, terrainBlendIrregularity, flattenBlendNoiseScale);
            if (partWeight <= weight)
            {
                continue;
            }

            weight = partWeight;
            targetWorldHeight = part.targetWorldHeight;
        }

        return weight;
    }

    private static float CalculateRectFlattenWeight(
        Vector2 local,
        Vector2 half,
        float blendWidth,
        float edgeNoiseStrength,
        float edgeNoiseScale)
    {
        float outsideX = Mathf.Max(Mathf.Abs(local.x) - half.x, 0f);
        float outsideZ = Mathf.Max(Mathf.Abs(local.y) - half.y, 0f);
        float outsideDistance = Mathf.Sqrt(outsideX * outsideX + outsideZ * outsideZ);

        if (outsideDistance <= 0f)
        {
            return 1f;
        }

        float noiseStrength = Mathf.Clamp01(edgeNoiseStrength);
        float maximumBlendReach = blendWidth * (1f + 0.45f * noiseStrength);
        if (blendWidth <= 0f || outsideDistance > maximumBlendReach)
        {
            return 0f;
        }

        if (noiseStrength > 0f)
        {
            float noise = FractalEdgeNoise(local, edgeNoiseScale) * 2f - 1f;
            float noiseOffset = noise * blendWidth * 0.45f * noiseStrength;
            outsideDistance = Mathf.Max(0f, outsideDistance + noiseOffset);
        }

        float t = Mathf.Clamp01(outsideDistance / blendWidth);
        return 1f - Mathf.SmoothStep(0f, 1f, t);
    }

    private static float FractalEdgeNoise(Vector2 local, float scale)
    {
        float safeScale = Mathf.Max(0.0001f, scale);
        float x = local.x * safeScale + 113.17f;
        float y = local.y * safeScale - 41.93f;
        float low = Mathf.PerlinNoise(x, y);
        float high = Mathf.PerlinNoise(x * 2.31f + 17.7f, y * 2.31f - 9.2f);
        return Mathf.Clamp01(low * 0.7f + high * 0.3f);
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

    private void OnDrawGizmosSelected()
    {
        if (!drawGenerationGizmos)
        {
            return;
        }

        Vector3 startPosition = ResolveDirectorStartPosition();
        DrawGizmoCircleXZ(new Vector2(startPosition.x, startPosition.z), nearFieldDistance, startPosition.y + 0.1f, new Color(0.2f, 1f, 0.35f, 0.35f));
        DrawGizmoCircleXZ(new Vector2(startPosition.x, startPosition.z), midFieldDistance, startPosition.y + 0.15f, new Color(1f, 0.85f, 0.2f, 0.35f));

        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(startPosition + Vector3.up * 0.5f, 1.5f);

        if (lastSpawnSafeZone != null)
        {
            Vector2 spawnCenter = new Vector2(lastSpawnSafeZone.center.x, lastSpawnSafeZone.center.z);
            DrawGizmoCircleXZ(spawnCenter, lastSpawnSafeZone.flatRadius, lastSpawnSafeZone.center.y + 0.2f, new Color(0.15f, 1f, 1f, 0.65f));
            DrawGizmoCircleXZ(spawnCenter, lastSpawnSafeZone.flatRadius + lastSpawnSafeZone.blendWidth, lastSpawnSafeZone.center.y + 0.2f, new Color(0.15f, 1f, 1f, 0.3f));
        }

        if (lastPlannedFields != null)
        {
            for (int i = 0; i < lastPlannedFields.Count; i++)
            {
                PlannedFarmField field = lastPlannedFields[i];
                if (field == null)
                {
                    continue;
                }

                Gizmos.color = GetFieldCategoryColor(field.category);
                if (field.footprintParts != null && field.footprintParts.Length > 0)
                {
                    for (int partIndex = 0; partIndex < field.footprintParts.Length; partIndex++)
                    {
                        PlannedFieldFootprintPart part = field.footprintParts[partIndex];
                        DrawRotatedRectGizmo(part.center, part.size, part.yawDegrees, field.targetWorldHeight + 0.25f);
                    }
                }
                else
                {
                    DrawRotatedRectGizmo(field.footprintCenter, field.footprintSize, field.yawDegrees, field.targetWorldHeight + 0.25f);
                }
            }
        }

        if (lastFieldPlacementDebug == null)
        {
            return;
        }

        for (int i = 0; i < lastFieldPlacementDebug.Count; i++)
        {
            RuntimeFieldPlacementDebugPoint point = lastFieldPlacementDebug[i];
            if (point.status != RuntimeFieldPlacementDebugStatus.Placed && !drawRejectedFieldCandidates)
            {
                continue;
            }

            Gizmos.color = GetPlacementDebugColor(point.status, point.category);
            Gizmos.DrawSphere(new Vector3(point.position.x, startPosition.y + 1.25f, point.position.y), 1.25f);
        }
    }

    private static void DrawRotatedRectGizmo(Vector2 center, Vector2 size, float yawDegrees, float y)
    {
        Vector2 half = size * 0.5f;
        Vector2 a = center + RuntimeTerrainUtility.Rotate(new Vector2(-half.x, -half.y), yawDegrees);
        Vector2 b = center + RuntimeTerrainUtility.Rotate(new Vector2(half.x, -half.y), yawDegrees);
        Vector2 c = center + RuntimeTerrainUtility.Rotate(new Vector2(half.x, half.y), yawDegrees);
        Vector2 d = center + RuntimeTerrainUtility.Rotate(new Vector2(-half.x, half.y), yawDegrees);

        DrawGizmoLineXZ(a, b, y);
        DrawGizmoLineXZ(b, c, y);
        DrawGizmoLineXZ(c, d, y);
        DrawGizmoLineXZ(d, a, y);
    }

    private static void DrawGizmoCircleXZ(Vector2 center, float radius, float y, Color color)
    {
        if (radius <= 0f)
        {
            return;
        }

        Color previousColor = Gizmos.color;
        Gizmos.color = color;
        const int segments = 64;
        Vector2 previous = center + new Vector2(radius, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float angle = i / (float)segments * Mathf.PI * 2f;
            Vector2 next = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            DrawGizmoLineXZ(previous, next, y);
            previous = next;
        }

        Gizmos.color = previousColor;
    }

    private static void DrawGizmoLineXZ(Vector2 a, Vector2 b, float y)
    {
        Gizmos.DrawLine(new Vector3(a.x, y, a.y), new Vector3(b.x, y, b.y));
    }

    private static Color GetFieldCategoryColor(RuntimeFieldCategory category)
    {
        switch (category)
        {
            case RuntimeFieldCategory.House:
                return new Color(0.25f, 0.9f, 0.45f, 0.9f);
            case RuntimeFieldCategory.Factory:
                return new Color(1f, 0.55f, 0.2f, 0.9f);
            case RuntimeFieldCategory.Storage:
                return new Color(0.25f, 0.65f, 1f, 0.9f);
            case RuntimeFieldCategory.Ruins:
                return new Color(0.65f, 0.65f, 0.65f, 0.9f);
            case RuntimeFieldCategory.Landmark:
                return new Color(1f, 0.25f, 0.25f, 0.9f);
            case RuntimeFieldCategory.Special:
                return new Color(0.75f, 0.35f, 1f, 0.9f);
            default:
                return Color.white;
        }
    }

    private static Color GetPlacementDebugColor(RuntimeFieldPlacementDebugStatus status, RuntimeFieldCategory category)
    {
        if (status == RuntimeFieldPlacementDebugStatus.Placed)
        {
            return GetFieldCategoryColor(category);
        }

        switch (status)
        {
            case RuntimeFieldPlacementDebugStatus.RejectedTerrain:
                return new Color(1f, 0.25f, 0.25f, 0.45f);
            case RuntimeFieldPlacementDebugStatus.RejectedReservation:
                return new Color(1f, 0.85f, 0.15f, 0.45f);
            case RuntimeFieldPlacementDebugStatus.RejectedNaturalFit:
                return new Color(0.2f, 0.75f, 1f, 0.45f);
            case RuntimeFieldPlacementDebugStatus.RejectedAccess:
                return new Color(1f, 0.2f, 0.8f, 0.45f);
            case RuntimeFieldPlacementDebugStatus.RejectedPreset:
                return new Color(0.6f, 0.6f, 0.6f, 0.45f);
            default:
                return new Color(1f, 1f, 1f, 0.35f);
        }
    }

    private void OnValidate()
    {
        frameBudgetMs = Mathf.Max(0.25f, frameBudgetMs);
        objectSpawnsPerFrame = Mathf.Max(1, objectSpawnsPerFrame);
        farmFieldCount = Mathf.Max(0, farmFieldCount);
        fieldSpacing = Mathf.Clamp01(fieldSpacing);
        spawnFlatRadius = Mathf.Max(0f, spawnFlatRadius);
        spawnBlendWidth = Mathf.Max(0f, spawnBlendWidth);
        spawnFieldExclusionPadding = Mathf.Max(0f, spawnFieldExclusionPadding);
        factoryPresence = Mathf.Clamp01(factoryPresence);
        rareLocationPresence = Mathf.Clamp01(rareLocationPresence);
        naturalProtection = Mathf.Clamp01(naturalProtection);
        placementForgiveness = Mathf.Clamp01(placementForgiveness);
        terrainBlendIrregularity = Mathf.Clamp01(terrainBlendIrregularity);
        forestPatchiness = Mathf.Clamp01(forestPatchiness);
        fieldEdgeClearance = Mathf.Clamp01(fieldEdgeClearance);

        ApplyDesignerTuning(null);

        fieldPlacementAttempts = Mathf.Max(1, fieldPlacementAttempts);
        fieldReservationPadding = Mathf.Max(0f, fieldReservationPadding);
        maxFarmFieldAverageSlopeDegrees = Mathf.Clamp(maxFarmFieldAverageSlopeDegrees, 0f, 90f);
        maxFarmFieldPeakSlopeDegrees = Mathf.Clamp(maxFarmFieldPeakSlopeDegrees, maxFarmFieldAverageSlopeDegrees, 90f);
        maxFarmFieldOverallGradeDegrees = Mathf.Clamp(maxFarmFieldOverallGradeDegrees, 0f, maxFarmFieldPeakSlopeDegrees);
        maxFarmFieldHeightDelta = Mathf.Max(0f, maxFarmFieldHeightDelta);
        fieldSectorColumns = Mathf.Max(1, fieldSectorColumns);
        fieldSectorRows = Mathf.Max(1, fieldSectorRows);
        fieldCandidatesPerSector = Mathf.Max(1, fieldCandidatesPerSector);
        minFieldCenterDistance = Mathf.Max(0f, minFieldCenterDistance);
        nearFieldDistance = Mathf.Max(0f, nearFieldDistance);
        midFieldDistance = Mathf.Max(nearFieldDistance, midFieldDistance);
        distanceBandExceptionWeight = Mathf.Clamp01(distanceBandExceptionWeight);
        houseTargetRatio = Mathf.Clamp01(houseTargetRatio);
        factoryTargetRatio = Mathf.Clamp01(factoryTargetRatio);
        storageTargetRatio = Mathf.Clamp01(storageTargetRatio);
        ruinsTargetRatio = Mathf.Clamp01(ruinsTargetRatio);
        landmarkTargetRatio = Mathf.Clamp01(landmarkTargetRatio);
        specialTargetRatio = Mathf.Clamp01(specialTargetRatio);
        categoryBudgetCorrectionStrength = Mathf.Max(0f, categoryBudgetCorrectionStrength);
        maxFarmFieldFlattenCost = Mathf.Max(0f, maxFarmFieldFlattenCost);
        maxFarmFieldSingleSampleFlattenCost = Mathf.Max(0f, maxFarmFieldSingleSampleFlattenCost);
        fieldClusterAffinity = Mathf.Clamp(fieldClusterAffinity, 0f, 2f);
        fieldClusterRadius = Mathf.Max(0f, fieldClusterRadius);
        detailClearPadding = Mathf.Max(0f, detailClearPadding);
        flattenBlendNoiseScale = Mathf.Max(0.0001f, flattenBlendNoiseScale);
        targetTreeCount = Mathf.Max(0, targetTreeCount);
        treePlacementAttempts = Mathf.Max(1, treePlacementAttempts);
        minTreeDistance = Mathf.Max(0f, minTreeDistance);
        treeFieldPadding = Mathf.Max(0f, treeFieldPadding);
        maxTreeWorldHeight = Mathf.Max(minTreeWorldHeight, maxTreeWorldHeight);
        maxTreeSlopeDegrees = Mathf.Clamp(maxTreeSlopeDegrees, 0f, 90f);
        naturalScatterNoiseScale = Mathf.Max(0.0001f, naturalScatterNoiseScale);
        naturalScatterNoiseThreshold = Mathf.Clamp01(naturalScatterNoiseThreshold);
        naturalScatterNoiseStrength = Mathf.Clamp01(naturalScatterNoiseStrength);
        naturalScatterDetailNoiseScale = Mathf.Max(0.0001f, naturalScatterDetailNoiseScale);
        naturalScatterCorridorNoiseScale = Mathf.Max(0.0001f, naturalScatterCorridorNoiseScale);
        naturalScatterCorridorStrength = Mathf.Clamp01(naturalScatterCorridorStrength);
        naturalScatterMinimumDensity = Mathf.Clamp01(naturalScatterMinimumDensity);
        rockClusterChance = Mathf.Clamp01(rockClusterChance);
        rockClusterMaxCount = Mathf.Max(1, rockClusterMaxCount);
        rockClusterRadius = Mathf.Max(0f, rockClusterRadius);
        maxDebugCandidateGizmos = Mathf.Max(0, maxDebugCandidateGizmos);
        seedValidationCount = Mathf.Max(1, seedValidationCount);

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

        if (naturalScatterPresets != null)
        {
            for (int i = 0; i < naturalScatterPresets.Length; i++)
            {
                RuntimeNaturalScatterPreset preset = naturalScatterPresets[i];
                if (preset == null)
                {
                    continue;
                }

                preset.weight = Mathf.Max(0f, preset.weight);
                preset.scaleRange = ValidateScaleRange(preset.scaleRange);
                preset.randomTiltDegrees = Mathf.Max(0f, preset.randomTiltDegrees);
                preset.clusterChance = Mathf.Clamp01(preset.clusterChance);
                preset.maxClusterCount = Mathf.Max(1, preset.maxClusterCount);
                preset.clusterRadius = Mathf.Max(0f, preset.clusterRadius);
            }
        }
    }
}
