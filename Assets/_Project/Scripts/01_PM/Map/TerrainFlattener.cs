using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Terrain HeightMap에서 기울기가 낮은 구역을 자동 탐색해 부드럽게 평탄화합니다.
/// - 에디터 : Inspector의 "▶ Flatten Terrain" 버튼 클릭 (Undo 지원)
/// - 런타임 : UI Button → FlattenTerrain() 연결
/// </summary>
[RequireComponent(typeof(Terrain))]
public class TerrainFlattener : MonoBehaviour
{
    [Header("Flat Zone Settings")]
    [Tooltip("생성할 평지 개수")]
    public int numFlatZones = 6;

    [Tooltip("평지 한 변 크기 (heightmap 픽셀 단위)")]
    public int flatZoneSize = 50;

    [Tooltip("평지 경계 블렌딩 반경 (픽셀) — 클수록 자연스럽게 연결됨")]
    public int blendRadius = 25;

    [Tooltip("평지 간 최소 거리 (픽셀) — 겹침 방지")]
    public int minZoneDist = 120;

    [Tooltip("지형 가장자리 제외 범위 (픽셀)")]
    public int edgeMargin = 60;

    [Header("Height Filter")]
    [Range(0f, 1f), Tooltip("이 높이 이상(산꼭대기)은 후보에서 제외")]
    public float maxHeightThreshold = 0.85f;

    [Range(0f, 1f), Tooltip("이 높이 이하(바다/절벽 바닥)는 후보에서 제외")]
    public float minHeightThreshold = 0.05f;

    // ──────────────────────────────────────────────────────────────
    //  Public API
    // ──────────────────────────────────────────────────────────────

    public void FlattenTerrain()
    {
        Terrain terrain = GetComponent<Terrain>();
        TerrainData td  = terrain.terrainData;
        int res         = td.heightmapResolution;

        float[,] heights = td.GetHeights(0, 0, res, res);

        List<(int cx, int cy)> zones = FindFlatZones(heights, res);

        foreach (var (cx, cy) in zones)
            ApplyFlatZone(heights, res, cx, cy);

#if UNITY_EDITOR
        UnityEditor.Undo.RegisterCompleteObjectUndo(td, "Flatten Terrain");
#endif
        td.SetHeights(0, 0, heights);

        Debug.Log($"[TerrainFlattener] {zones.Count}개 평지 생성 완료 (해상도: {res}x{res})");
        for (int i = 0; i < zones.Count; i++)
            Debug.Log($"  B{i + 1} — 중심({zones[i].cx}, {zones[i].cy})");
    }

    // ──────────────────────────────────────────────────────────────
    //  Zone Detection
    // ──────────────────────────────────────────────────────────────

    List<(int cx, int cy)> FindFlatZones(float[,] h, int res)
    {
        float[,] slope    = CalcSlope(h, res);
        float[,] avgSlope = BoxFilter(slope, res, flatZoneSize);

        // score 맵: 평탄할수록 낮음 / 제외 구역 = float.MaxValue
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

        var zones = new List<(int, int)>();
        int minDistSq = minZoneDist * minZoneDist;

        while (zones.Count < numFlatZones)
        {
            // 가장 평탄한 위치 탐색
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

            // 선택 위치 주변 후보 제거
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

    void ApplyFlatZone(float[,] h, int res, int cx, int cy)
    {
        int half = flatZoneSize / 2;
        int br   = blendRadius;

        // 내부 영역 평균 높이 → 평탄화 목표값
        int ry0 = Mathf.Max(0, cy - half), ry1 = Mathf.Min(res, cy + half);
        int rx0 = Mathf.Max(0, cx - half), rx1 = Mathf.Min(res, cx + half);
        float sum = 0f; int cnt = 0;
        for (int y = ry0; y < ry1; y++)
        for (int x = rx0; x < rx1; x++)
        { sum += h[y, x]; cnt++; }
        float target = sum / cnt;

        // 블렌딩까지 포함한 처리 영역
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
            // 경계에서 바깥쪽으로의 거리 (Chebyshev)
            float odx = Mathf.Max(Mathf.Abs(x - cx) - half, 0f);
            float ody = Mathf.Max(Mathf.Abs(y - cy) - half, 0f);
            float od  = Mathf.Max(odx, ody);

            float t     = Mathf.Clamp01(od / br);
            float blend = t * t * (3f - 2f * t);        // smoothstep: 1=원본, 0=평지
            h[y, x] = blend * h[y, x] + (1f - blend) * target;
        }
    }

    // ──────────────────────────────────────────────────────────────
    //  Math Helpers
    // ──────────────────────────────────────────────────────────────

    /// <summary>중앙 차분으로 기울기 맵 계산</summary>
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

    /// <summary>분리 가능한 박스 필터 (prefix sum, O(N²)) — scipy uniform_filter 대응</summary>
    static float[,] BoxFilter(float[,] src, int res, int size)
    {
        int     half = size / 2;
        float[,] tmp = new float[res, res];
        float[,] dst = new float[res, res];

        // 수평 패스
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

        // 수직 패스
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
