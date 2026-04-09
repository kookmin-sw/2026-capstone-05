using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Origin Terrain(원본)에서 높이맵을 읽어 평탄화 후 Copy Terrain에만 씁니다.
/// Origin Terrain은 절대 수정되지 않습니다.
///
/// Inspector 연결:
///   Origin Terrains      [0~3] → _Project/Assets/01_PM/Terrain/Origin Terrain 1~4  (TerrainData 에셋)
///   Copy Terrains        [0~3] → _Project/Assets/01_PM/Terrain/Copy Terrain 1~4    (TerrainData 에셋)
///   Copy Terrain Objects [0~3] → 씬에 배치된 Copy Terrain GameObject (큐브 스폰 위치 계산용)
/// </summary>
public class TerrainFlattener : MonoBehaviour
{
    [Header("Terrain Data References")]
    [Tooltip("원본 터레인 (읽기 전용, 절대 수정 안 됨)")]
    public TerrainData[] originTerrains;

    [Tooltip("평탄화 결과를 저장할 카피 터레인 (에셋)")]
    public TerrainData[] copyTerrains;

    [Tooltip("씬에 배치된 Copy Terrain GameObject — 평지 위치 계산에 사용")]
    public Terrain[] copyTerrainObjects;

    [Header("Flat Zone Settings")]
    [Tooltip("터레인 1장당 생성할 평지 개수")]
    public int numFlatZones = 6;

    [Tooltip("평지 한 변 크기 (RAW 픽셀) — 1픽셀 = 2 월드유닛, 50px = 100 월드유닛 (큐브 한 변과 일치)")]
    public int flatZoneSize = 50;

    [Tooltip("평지 경계 블렌딩 반경 (픽셀)")]
    public int blendRadius = 25;

    [Tooltip("평지 간 최소 거리 (픽셀)")]
    public int minZoneDist = 120;

    [Tooltip("지형 가장자리 제외 범위 (픽셀)")]
    public int edgeMargin = 60;

    [Header("Height Filter")]
    [Range(0f, 1f), Tooltip("이 높이 이상(산꼭대기)은 후보 제외")]
    public float maxHeightThreshold = 0.85f;

    const float SeaLevelWorldHeight = 14f;

    [Header("Noise Variation")]
    [Tooltip("낮을수록 완만하고 큰 굴곡 — 2~6 권장")]
    public float noiseFrequency = 3f;

    [Range(0f, 0.15f), Tooltip("높이맵 변화량 (0.05 = 전체 높이의 5%)")]
    public float noiseAmplitude = 0.05f;

    [Header("Cube Material")]
    [Tooltip("Assets/_Project/Assets/01_PM/Material/FlatZone.mat 연결")]
    public Material flatZoneMaterial;

    // ──────────────────────────────────────────────────────────────
    //  Public API
    // ──────────────────────────────────────────────────────────────

    public void GenerateTerrainFromScratch()
    {
        if (copyTerrains == null || copyTerrains.Length == 0)
        { Debug.LogError("[TerrainFlattener] Copy Terrain이 연결되지 않았습니다."); return; }

        ClearChildren();

        Random.InitState((int)System.DateTime.Now.Ticks);
        float ox1 = Random.value * 9999f, oy1 = Random.value * 9999f;  // base
        float ox2 = Random.value * 9999f, oy2 = Random.value * 9999f;  // detail
        float ox3 = Random.value * 9999f, oy3 = Random.value * 9999f;  // biome

        int S = copyTerrains[0] != null ? copyTerrains[0].heightmapResolution : 513;
        int H = S * 2 - 1, W = S * 2 - 1;

        // 향상된 자연스러운 지형 생성을 위한 고정 파라미터 (인스펙터 노출 제거)
        float optBaseScale = 0.006f;        // 산맥의 빈도 조절 (조금 더 넓고 자연스럽게)
        float optDetailScale = 0.02f;       // 디테일 노이즈 크기
        float optDetailStrength = 0.05f;    // 뾰족함을 줄이기 위해 디테일 강도 완화
        float optBiomeScale = 0.01f;        // 산맥과 평야의 교차 빈도
        float optMountainExp = 1.2f;        // 산맥의 가파름 완화 (뾰족함 제거)
        float optMountainHeight = 0.8f;     // 산맥 높이 증폭 완화 (구릉과 산 사이의 자연스러운 높이)
        bool optFlatMaskEnabled = true;

        var full = TerrainGenerator.GenerateFromScratch(
            H, W, S,
            optBaseScale, optDetailScale, optDetailStrength,
            optBiomeScale, optMountainExp, optMountainHeight,
            optFlatMaskEnabled,
            ox1, oy1, ox2, oy2, ox3, oy3);

        full = TerrainGenerator.GaussianBlur(full, H, W, 2);

        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
            full[y, x] = Mathf.Clamp01(full[y, x]);

        TerrainGenerator.SplitTiles(full, S,
            out var t0, out var t1, out var t2, out var t3);
        float[][,] tiles = { t0, t1, t2, t3 };

        int count = Mathf.Min(copyTerrains.Length, 4);
        for (int i = 0; i < count; i++)
        {
            if (copyTerrains[i] == null) continue;
#if UNITY_EDITOR
            UnityEditor.Undo.RegisterCompleteObjectUndo(copyTerrains[i], "Generate Terrain From Scratch");
#endif
            copyTerrains[i].SetHeights(0, 0, tiles[i]);
        }

        Debug.Log("[TerrainFlattener] 신규 지형 생성 완료");
    }

    public void CopyOriginTerrainRaw()
    {
        if (originTerrains == null || copyTerrains == null)
        {
            Debug.LogError("[TerrainFlattener] Origin / Copy Terrain 배열이 비어 있습니다.");
            return;
        }

        ClearChildren();

        int count = Mathf.Min(originTerrains.Length, copyTerrains.Length);
        for (int i = 0; i < count; i++)
        {
            if (originTerrains[i] == null || copyTerrains[i] == null)
            {
                Debug.LogWarning($"[TerrainFlattener] 인덱스 {i} 슬롯이 비어 있어 건너뜁니다.");
                continue;
            }

            int res          = originTerrains[i].heightmapResolution;
            float[,] heights = originTerrains[i].GetHeights(0, 0, res, res);

#if UNITY_EDITOR
            UnityEditor.Undo.RegisterCompleteObjectUndo(copyTerrains[i], $"Copy Origin Raw Terrain {i + 1}");
#endif
            copyTerrains[i].SetHeights(0, 0, heights);
            Debug.Log($"[TerrainFlattener] Copy Terrain {i + 1} — 순수 원본 복제 완료");
        }

        Debug.Log($"[TerrainFlattener] 순수 원본 복제 전체 {count}장 완료.");
    }

    public void FlattenTerrain()
    {
        if (originTerrains == null || copyTerrains == null)
        {
            Debug.LogError("[TerrainFlattener] Origin / Copy Terrain 배열이 비어 있습니다.");
            return;
        }

        ClearChildren();

        // 노이즈 오프셋을 한 번만 생성 — 모든 타일이 공유해야 경계가 이어짐
        Random.InitState((int)System.DateTime.Now.Ticks);
        Vector2 noiseOffset = new(Random.value * 9999f, Random.value * 9999f);

        int count = Mathf.Min(originTerrains.Length, copyTerrains.Length);

        for (int i = 0; i < count; i++)
        {
            if (originTerrains[i] == null || copyTerrains[i] == null)
            {
                Debug.LogWarning($"[TerrainFlattener] 인덱스 {i} 슬롯이 비어 있어 건너뜁니다.");
                continue;
            }

            Terrain terrainObj = (copyTerrainObjects != null && i < copyTerrainObjects.Length)
                                 ? copyTerrainObjects[i] : null;

            ProcessTerrain(i + 1, originTerrains[i], copyTerrains[i], terrainObj, noiseOffset);
        }

        Debug.Log($"[TerrainFlattener] 전체 {count}장 처리 완료.");
    }

    // ──────────────────────────────────────────────────────────────
    //  Per-Terrain Processing
    // ──────────────────────────────────────────────────────────────

    void ProcessTerrain(int index, TerrainData origin, TerrainData copy, Terrain terrainObj, Vector2 noiseOffset)
    {
        int res = origin.heightmapResolution;

        // Origin에서만 읽음 — Origin 자체는 절대 수정하지 않음
        float[,] heights = origin.GetHeights(0, 0, res, res);

        // 공유된 오프셋 + 월드 좌표 기반 노이즈 → 타일 경계 연속성 보장
        Vector3 worldPos  = terrainObj != null ? terrainObj.transform.position : Vector3.zero;
        Vector3 worldSize = origin.size;
        ApplyPerlinNoise(heights, res, worldPos, worldSize, noiseOffset);

        List<(int cx, int cy)> zones = FindFlatZones(heights, res, origin.size.y);

        // ── target을 먼저 모두 계산 (높이맵 수정 전 원본 기준)
        var zoneTargets = new List<(int cx, int cy, float target)>();
        foreach (var (cx, cy) in zones)
            zoneTargets.Add((cx, cy, ComputeZoneTarget(heights, res, cx, cy)));

        // ── Pass 1: 블렌드 영역 적용 (인접 zone 간 간섭 가능)
        foreach (var (cx, cy, target) in zoneTargets)
            ApplyBlendZone(heights, res, cx, cy, target);

        // ── Pass 2: 내부 영역 재덮어쓰기 → 블렌드 간섭 제거, 항상 완벽한 평지 보장
        foreach (var (cx, cy, target) in zoneTargets)
            ApplyInnerZone(heights, res, cx, cy, target);

#if UNITY_EDITOR
        UnityEditor.Undo.RegisterCompleteObjectUndo(copy, $"Flatten Copy Terrain {index}");
#endif
        // Copy에만 씀
        copy.SetHeights(0, 0, heights);

        Debug.Log($"[TerrainFlattener] Copy Terrain {index} — {zones.Count}개 평지 적용 (해상도: {res}x{res})");
        for (int i = 0; i < zoneTargets.Count; i++)
            Debug.Log($"    B{i + 1} 중심({zoneTargets[i].cx}, {zoneTargets[i].cy})");

        if (terrainObj != null)
            SpawnZoneCubes(index, terrainObj, res, zoneTargets);
        else
            Debug.LogWarning($"[TerrainFlattener] Copy Terrain Objects [{index - 1}] 미연결 — 큐브 스폰 생략");
    }

    void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
#if UNITY_EDITOR
            UnityEditor.Undo.DestroyObjectImmediate(transform.GetChild(i).gameObject);
#else
            Destroy(transform.GetChild(i).gameObject);
#endif
        }
    }

    void SpawnZoneCubes(int terrainIndex, Terrain terrainObj, int res,
                        List<(int cx, int cy, float target)> zones)
    {
        Vector3 tp = terrainObj.transform.position;
        Vector3 ts = terrainObj.terrainData.size;

        if (flatZoneMaterial == null)
            Debug.LogWarning("[TerrainFlattener] Flat Zone Material이 연결되지 않았습니다. Inspector에서 FlatZone.mat을 연결하세요.");

        for (int i = 0; i < zones.Count; i++)
        {
            var (cx, cy, target) = zones[i];

            float wx = tp.x + (cx / (float)(res - 1)) * ts.x;
            float wz = tp.z + (cy / (float)(res - 1)) * ts.z;
            // 큐브 중심 = 지면 높이 - 4.9f (위 0.1, 아래 9.9) => 큐브가 살짝 땅에 묻히도록 (높이 10f 기준)
            float wy = tp.y + target * ts.y - 4.9f;    
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = $"FlatZone_T{terrainIndex}_B{i + 1}";
            cube.transform.SetParent(transform, worldPositionStays: true);
            cube.transform.localScale = new Vector3(100f, 10f, 100f);
            cube.transform.position   = new Vector3(wx, wy, wz);

            if (flatZoneMaterial != null)
                cube.GetComponent<Renderer>().sharedMaterial = flatZoneMaterial;

            // 추가적인 건물/오브젝트 배치는 전용 스크립트(BuildingSpawner)로 위임
            BuildingSpawner spawner = GetComponent<BuildingSpawner>();
            if (spawner != null)
            {
                float groundY = tp.y + target * ts.y;
                spawner.SpawnBuilding(new Vector3(wx, groundY, wz), transform, $"Building_T{terrainIndex}_B{i + 1}");
            }
        }
    }

    // ──────────────────────────────────────────────────────────────
    //  Zone Detection
    // ──────────────────────────────────────────────────────────────

    List<(int cx, int cy)> FindFlatZones(float[,] h, int res, float terrainSizeY)
    {
        float minNorm = SeaLevelWorldHeight / terrainSizeY;  // 월드 높이 → 정규화

        float[,] slope     = CalcSlope(h, res);
        float[,] avgSlope  = BoxFilter(slope, res, flatZoneSize);
        // 중심 픽셀 하나가 아닌 zone 전체 평균 높이로 판단 → target이 해수면 아래로 내려가는 것 방지
        float[,] avgHeight = BoxFilter(h, res, flatZoneSize);

        float[,] score = new float[res, res];
        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            bool excluded = y < edgeMargin || y >= res - edgeMargin
                         || x < edgeMargin || x >= res - edgeMargin
                         || avgHeight[y, x] > maxHeightThreshold
                         || avgHeight[y, x] < minNorm;
            score[y, x] = excluded ? float.MaxValue : avgSlope[y, x];
        }

        var zones     = new List<(int, int)>();
        int minDistSq = minZoneDist * minZoneDist;

        while (zones.Count < numFlatZones)
        {
            float minVal = float.MaxValue;
            int bx = -1, by = -1;
            for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                if (score[y, x] < minVal)
                {
                    minVal = score[y, x];
                    bx = x; by = y;
                }
            }

            if (minVal == float.MaxValue)
            {
                Debug.LogWarning($"[TerrainFlattener] 공간 부족 — {zones.Count}개만 배치됨");
                break;
            }

            zones.Add((bx, by));

            for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                int dx = x - bx, dy = y - by;
                if (dx * dx + dy * dy < minDistSq)
                    score[y, x] = float.MaxValue;
            }
        }

        return zones;
    }

    // ──────────────────────────────────────────────────────────────
    //  Noise Variation
    // ──────────────────────────────────────────────────────────────

    void ApplyPerlinNoise(float[,] h, int res, Vector3 worldPos, Vector3 worldSize, Vector2 noiseOffset)
    {
        // 픽셀 → 월드 좌표 → Perlin 좌표
        // 타일 크기(worldSize.x)로 나눠 정규화하므로 인접 타일과 연속성 보장
        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            float wx    = worldPos.x + x / (float)(res - 1) * worldSize.x;
            float wz    = worldPos.z + y / (float)(res - 1) * worldSize.z;
            float nx    = wx / worldSize.x * noiseFrequency + noiseOffset.x;
            float nz    = wz / worldSize.z * noiseFrequency + noiseOffset.y;
            float noise = Mathf.PerlinNoise(nx, nz) - 0.5f;
            h[y, x] = Mathf.Clamp01(h[y, x] + noise * noiseAmplitude);
        }
    }

    // ──────────────────────────────────────────────────────────────
    //  Flattening
    // ──────────────────────────────────────────────────────────────

    // 내부 zone 평균 높이 계산 (높이맵 수정 전 호출)
    float ComputeZoneTarget(float[,] h, int res, int cx, int cy)
    {
        int half = flatZoneSize / 2;
        int ry0 = Mathf.Max(0, cy - half), ry1 = Mathf.Min(res, cy + half + 1);
        int rx0 = Mathf.Max(0, cx - half), rx1 = Mathf.Min(res, cx + half + 1);
        float sum = 0f; int cnt = 0;
        for (int y = ry0; y < ry1; y++)
        for (int x = rx0; x < rx1; x++)
        { sum += h[y, x]; cnt++; }
        return sum / cnt;
    }

    // Pass 1: 블렌드 경계 적용 (인접 zone이 서로 영향을 줄 수 있음)
    void ApplyBlendZone(float[,] h, int res, int cx, int cy, float target)
    {
        int half = flatZoneSize / 2;
        int br   = blendRadius;
        int wy0 = Mathf.Max(0, cy - half - br), wy1 = Mathf.Min(res, cy + half + br + 1);
        int wx0 = Mathf.Max(0, cx - half - br), wx1 = Mathf.Min(res, cx + half + br + 1);

        for (int y = wy0; y < wy1; y++)
        for (int x = wx0; x < wx1; x++)
        {
            if (Mathf.Abs(x - cx) <= half && Mathf.Abs(y - cy) <= half) continue; // 내부는 Pass 2에서

            float odx   = Mathf.Max(Mathf.Abs(x - cx) - half, 0f);
            float ody   = Mathf.Max(Mathf.Abs(y - cy) - half, 0f);
            float od    = Mathf.Max(odx, ody);
            float t     = Mathf.Clamp01(od / br);
            float blend = t * t * (3f - 2f * t);   // smoothstep
            h[y, x] = blend * h[y, x] + (1f - blend) * target;
        }
    }

    // Pass 2: 내부 영역을 target으로 강제 덮어쓰기 → 항상 완벽한 평지 보장
    void ApplyInnerZone(float[,] h, int res, int cx, int cy, float target)
    {
        int half = flatZoneSize / 2;
        int ry0 = Mathf.Max(0, cy - half), ry1 = Mathf.Min(res, cy + half + 1);
        int rx0 = Mathf.Max(0, cx - half), rx1 = Mathf.Min(res, cx + half + 1);
        for (int y = ry0; y < ry1; y++)
        for (int x = rx0; x < rx1; x++)
            h[y, x] = target;
    }

    // ──────────────────────────────────────────────────────────────
    //  Math Helpers
    // ──────────────────────────────────────────────────────────────

    static float[,] CalcSlope(float[,] h, int res)
    {
        float[,] s = new float[res, res];
        for (int y = 1; y < res - 1; y++)
        for (int x = 1; x < res - 1; x++)
        {
            float gx = (h[y, x + 1] - h[y, x - 1]) * 0.5f;
            float gy = (h[y + 1, x] - h[y - 1, x]) * 0.5f;
            s[y, x]  = Mathf.Sqrt(gx * gx + gy * gy);
        }
        return s;
    }

    static float[,] BoxFilter(float[,] src, int res, int size)
    {
        int      half = size / 2;
        float[,] tmp  = new float[res, res];
        float[,] dst  = new float[res, res];

        for (int y = 0; y < res; y++)
        {
            float[] psum = new float[res + 1];
            for (int x = 0; x < res; x++) psum[x + 1] = psum[x] + src[y, x];
            for (int x = 0; x < res; x++)
            {
                int x0 = Mathf.Max(0,       x - half);
                int x1 = Mathf.Min(res - 1, x + half);
                tmp[y, x] = (psum[x1 + 1] - psum[x0]) / (x1 - x0 + 1);
            }
        }

        for (int x = 0; x < res; x++)
        {
            float[] psum = new float[res + 1];
            for (int y = 0; y < res; y++) psum[y + 1] = psum[y] + tmp[y, x];
            for (int y = 0; y < res; y++)
            {
                int y0 = Mathf.Max(0,       y - half);
                int y1 = Mathf.Min(res - 1, y + half);
                dst[y, x] = (psum[y1 + 1] - psum[y0]) / (y1 - y0 + 1);
            }
        }

        return dst;
    }
}
