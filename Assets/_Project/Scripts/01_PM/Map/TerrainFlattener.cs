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

    [Tooltip("평지 한 변 크기 (월드 유닛) — RAW 픽셀로는 절반 값이 사용됨 (100 → 50px)")]
    public int flatZoneSize = 100;

    [Tooltip("평지 경계 블렌딩 반경 (픽셀)")]
    public int blendRadius = 25;

    [Tooltip("평지 간 최소 거리 (픽셀)")]
    public int minZoneDist = 120;

    [Tooltip("지형 가장자리 제외 범위 (픽셀)")]
    public int edgeMargin = 60;

    [Header("Height Filter")]
    [Range(0f, 1f), Tooltip("이 높이 이상(산꼭대기)은 후보 제외")]
    public float maxHeightThreshold = 0.85f;

    [Range(0f, 1f), Tooltip("이 높이 이하(바다/절벽 바닥)는 후보 제외")]
    public float minHeightThreshold = 0.05f;

    [Header("Randomness")]
    [Range(0f, 1f), Tooltip("0 = 항상 가장 평탄한 곳, 1 = 완전 랜덤")]
    public float randomness = 0.5f;

    [Header("Cube Material")]
    [Tooltip("Assets/_Project/Assets/01_PM/Material/FlatZone.mat 연결")]
    public Material flatZoneMaterial;

    // ──────────────────────────────────────────────────────────────
    //  Public API
    // ──────────────────────────────────────────────────────────────

    public void FlattenTerrain()
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

            Terrain terrainObj = (copyTerrainObjects != null && i < copyTerrainObjects.Length)
                                 ? copyTerrainObjects[i] : null;

            ProcessTerrain(i + 1, originTerrains[i], copyTerrains[i], terrainObj);
        }

        Debug.Log($"[TerrainFlattener] 전체 {count}장 처리 완료.");
    }

    // ──────────────────────────────────────────────────────────────
    //  Per-Terrain Processing
    // ──────────────────────────────────────────────────────────────

    void ProcessTerrain(int index, TerrainData origin, TerrainData copy, Terrain terrainObj)
    {
        int res = origin.heightmapResolution;

        // Origin에서만 읽음 — Origin 자체는 절대 수정하지 않음
        float[,] heights = origin.GetHeights(0, 0, res, res);

        List<(int cx, int cy)> zones = FindFlatZones(heights, res);

        var zoneTargets = new List<(int cx, int cy, float target)>();
        foreach (var (cx, cy) in zones)
        {
            float target = ApplyFlatZone(heights, res, cx, cy);
            zoneTargets.Add((cx, cy, target));
        }

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
            DestroyImmediate(transform.GetChild(i).gameObject);
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
            float wy = tp.y + target * ts.y;    // 큐브 중심 = 지면 높이 (위 5, 아래 5)

            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = $"FlatZone_T{terrainIndex}_B{i + 1}";
            cube.transform.SetParent(transform, worldPositionStays: true);
            cube.transform.localScale = new Vector3(100f, 10f, 100f);
            cube.transform.position   = new Vector3(wx, wy, wz);

            if (flatZoneMaterial != null)
                cube.GetComponent<Renderer>().sharedMaterial = flatZoneMaterial;
        }
    }

    // ──────────────────────────────────────────────────────────────
    //  Zone Detection
    // ──────────────────────────────────────────────────────────────

    List<(int cx, int cy)> FindFlatZones(float[,] h, int res)
    {
        int pixelSize = flatZoneSize / 2;   // 월드 유닛 → RAW 픽셀 변환

        float[,] slope    = CalcSlope(h, res);
        float[,] avgSlope = BoxFilter(slope, res, pixelSize);

        float[,] score = new float[res, res];
        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            float ht      = h[y, x];
            bool excluded = y < edgeMargin || y >= res - edgeMargin
                         || x < edgeMargin || x >= res - edgeMargin
                         || ht > maxHeightThreshold
                         || ht < minHeightThreshold;
            score[y, x] = excluded ? float.MaxValue : avgSlope[y, x];
        }

        // 랜덤 노이즈 추가 — score 범위에 비례하게 섞어서 평탄한 구역 선호는 유지
        if (randomness > 0f)
        {
            Random.InitState((int)System.DateTime.Now.Ticks);
            float minS = float.MaxValue, maxS = 0f;
            for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                if (score[y, x] < float.MaxValue)
                {
                    if (score[y, x] < minS) minS = score[y, x];
                    if (score[y, x] > maxS) maxS = score[y, x];
                }
            }
            float range = maxS - minS;
            if (range > 0f)
                for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                    if (score[y, x] < float.MaxValue)
                        score[y, x] += Random.value * randomness * range;
        }

        var zones     = new List<(int, int)>();
        int halfDist  = minZoneDist / 2;
        int minDistSq = halfDist * halfDist;

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
    //  Flattening
    // ──────────────────────────────────────────────────────────────

    float ApplyFlatZone(float[,] h, int res, int cx, int cy)
    {
        int pixelSize = flatZoneSize / 2;   // 월드 유닛 → RAW 픽셀
        int half      = pixelSize / 2;
        int br        = blendRadius;

        int ry0 = Mathf.Max(0, cy - half), ry1 = Mathf.Min(res, cy + half);
        int rx0 = Mathf.Max(0, cx - half), rx1 = Mathf.Min(res, cx + half);
        float sum = 0f; int cnt = 0;
        for (int y = ry0; y < ry1; y++)
        for (int x = rx0; x < rx1; x++)
        { sum += h[y, x]; cnt++; }
        float target = sum / cnt;

        int wy0 = Mathf.Max(0, cy - half - br), wy1 = Mathf.Min(res, cy + half + br);
        int wx0 = Mathf.Max(0, cx - half - br), wx1 = Mathf.Min(res, cx + half + br);

        for (int y = wy0; y < wy1; y++)
        for (int x = wx0; x < wx1; x++)
        {
            bool inner = Mathf.Abs(x - cx) <= half && Mathf.Abs(y - cy) <= half;
            if (inner)
            {
                h[y, x] = target;
                continue;
            }
            float odx   = Mathf.Max(Mathf.Abs(x - cx) - half, 0f);
            float ody   = Mathf.Max(Mathf.Abs(y - cy) - half, 0f);
            float od    = Mathf.Max(odx, ody);
            float t     = Mathf.Clamp01(od / br);
            float blend = t * t * (3f - 2f * t);   // smoothstep
            h[y, x] = blend * h[y, x] + (1f - blend) * target;
        }

        return target;
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
