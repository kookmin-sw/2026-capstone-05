using System.Collections.Generic;
using UnityEngine;

// ──────────────────────────────────────────────────────────────
//  분석 결과 저장 (Inspector에서 확인/수동 조정 가능)
// ──────────────────────────────────────────────────────────────
[System.Serializable]
public class TerrainAnalysisData
{
    public bool isValid = false;

    [Header("Perlin Parameters (분석으로 자동 설정)")]
    public float scale       = 200f;
    public int   octaves     = 6;
    public float persistence = 0.5f;
    public float lacunarity  = 2f;

    [Header("Height Distribution")]
    public float heightMean = 0.3f;
    public float heightStd  = 0.15f;

    [Header("Terrain 1 Cap (오른쪽 아래 — 산 없는 평지)")]
    public float t1HeightCap = 0.35f;

    [Header("Water Zones")]
    public int numWaterZones = 4;
    public int waterZoneSize = 200;

    [HideInInspector]
    public int tileRes = 513;
}

// ──────────────────────────────────────────────────────────────
//  생성 로직 (순수 정적 클래스, AI/ML 불필요)
//
//  타일 배치 (merged 1025×1025 기준):
//    top-left  = t3 (x-1, z1)  │  top-right  = t2 (x0, z1)
//    bot-left  = t4 (x-1, z0)  │  bot-right  = t1 (x0, z0)  ← 산 없는 구역
// ──────────────────────────────────────────────────────────────
public static class TerrainGenerator
{
    // ── 4타일 → 1025×1025 합치기 (Python merge_tiles 동일 로직)
    public static float[,] MergeTiles(float[,] t1, float[,] t2,
                                      float[,] t3, float[,] t4)
    {
        int S = t1.GetLength(0);          // 513
        int H = S * 2 - 1, W = S * 2 - 1; // 1025
        var m = new float[H, W];

        // flipud 적용 (Python과 동일): row y → row (S-1-y)
        for (int y = 0; y < S - 1; y++)
        {
            for (int x = 0; x < S - 1; x++) m[y, x]         = t3[S-1-y, x];  // top-left
            for (int x = 0; x < S;     x++) m[y, x + S - 1] = t2[S-1-y, x];  // top-right
        }
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S - 1; x++) m[y+S-1, x]         = t4[S-1-y, x]; // bot-left
            for (int x = 0; x < S;     x++) m[y+S-1, x + S - 1] = t1[S-1-y, x]; // bot-right
        }
        return m;
    }

    // ── 1025×1025 → 4타일 분리 (MergeTiles 역연산)
    public static void SplitTiles(float[,] full, int S,
        out float[,] t1, out float[,] t2,
        out float[,] t3, out float[,] t4)
    {
        t1 = new float[S, S]; t2 = new float[S, S];
        t3 = new float[S, S]; t4 = new float[S, S];

        for (int y = 0; y < S - 1; y++)
        {
            for (int x = 0; x < S - 1; x++) t3[S-1-y, x] = full[y, x];
            for (int x = 0; x < S;     x++) t2[S-1-y, x] = full[y, x + S - 1];
        }
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S - 1; x++) t4[S-1-y, x] = full[y + S - 1, x];
            for (int x = 0; x < S;     x++) t1[S-1-y, x] = full[y + S - 1, x + S - 1];
        }
        // 경계 행(full[S-1, :])을 t3/t2 row 0에도 복사 — 수평 경계 절벽 방지
        for (int x = 0; x < S - 1; x++) t3[0, x] = full[S-1, x];
        for (int x = 0; x < S;     x++) t2[0, x] = full[S-1, x + S - 1];

        // 경계 열(full[:, S-1])을 t3/t4 마지막 열에도 복사 — 수직 경계 절벽 방지
        for (int y = 0; y < S - 1; y++) t3[S-1-y, S-1] = full[y,       S-1];
        for (int y = 0; y < S;     y++) t4[S-1-y, S-1] = full[y + S-1, S-1];
    }

    // ── 원본 4타일 분석 → TerrainAnalysisData
    public static TerrainAnalysisData Analyze(float[,] t1, float[,] t2,
                                               float[,] t3, float[,] t4)
    {
        int S      = t1.GetLength(0);
        var merged = MergeTiles(t1, t2, t3, t4);
        int H = merged.GetLength(0), W = merged.GetLength(1);

        // 전체 높이 통계
        double sum = 0, sum2 = 0;
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        { sum += merged[y, x]; sum2 += (double)merged[y, x] * merged[y, x]; }
        double inv = 1.0 / (H * W);
        float mean = (float)(sum * inv);
        float std  = Mathf.Sqrt(Mathf.Max(0f, (float)(sum2 * inv - mean * (double)mean)));

        // 기울기 → roughness
        double rsum = 0;
        for (int y = 1; y < H-1; y++)
        for (int x = 1; x < W-1; x++)
        {
            float gx = (merged[y, x+1] - merged[y, x-1]) * 0.5f;
            float gy = (merged[y+1, x] - merged[y-1, x]) * 0.5f;
            rsum += Mathf.Sqrt(gx*gx + gy*gy);
        }
        float roughness = (float)(rsum / ((H-2.0) * (W-2.0)));

        // roughness → Perlin 파라미터 (FFT 대신 역수 추정)
        float scale       = Mathf.Clamp(0.25f / (roughness + 0.0003f), 80f, 450f);
        int   octaves     = Mathf.Clamp((int)(roughness * 8000f), 4, 8);
        float persistence = Mathf.Clamp(0.5f + roughness * 120f,  0.35f, 0.65f);

        // t1 영역(오른쪽 아래) 95th percentile → 산 높이 상한
        var t1h = new float[S * S];
        int idx = 0;
        for (int y = S-1; y < H; y++)
        for (int x = S-1; x < W; x++)
            t1h[idx++] = merged[y, x];
        System.Array.Sort(t1h);
        float t1Cap = t1h[(int)(t1h.Length * 0.95f)];

        Debug.Log($"[TerrainAnalysis] scale={scale:F1}  octaves={octaves}  " +
                  $"persistence={persistence:F3}  roughness={roughness:F5}  " +
                  $"mean={mean:F4}  std={std:F4}  t1Cap={t1Cap:F4}");

        return new TerrainAnalysisData
        {
            isValid       = true,
            scale         = scale,
            octaves       = octaves,
            persistence   = persistence,
            lacunarity    = 2f,
            heightMean    = mean,
            heightStd     = std,
            t1HeightCap   = t1Cap,
            numWaterZones = 4,
            waterZoneSize = 200,
            tileRes       = S,
        };
    }

    // ── Perlin 노이즈 지형 생성 (1025×1025)
    public static float[,] Generate(TerrainAnalysisData p, int seed)
    {
        int S = p.tileRes;
        int H = S * 2 - 1, W = S * 2 - 1;
        var arr = new float[H, W];

        Random.InitState(seed);
        float ox = Random.value * 9999f;
        float oy = Random.value * 9999f;

        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            float val = 0f, amp = 1f, freq = 1f, maxAmp = 0f;
            for (int o = 0; o < p.octaves; o++)
            {
                val   += Mathf.PerlinNoise(x / p.scale * freq + ox,
                                           y / p.scale * freq + oy) * amp;
                maxAmp += amp;
                amp    *= p.persistence;
                freq   *= p.lacunarity;
            }
            arr[y, x] = val / maxAmp;
        }

        // 0~1 정규화
        float min = float.MaxValue, max = float.MinValue;
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            if (arr[y,x] < min) min = arr[y,x];
            if (arr[y,x] > max) max = arr[y,x];
        }
        float range = Mathf.Max(max - min, 1e-6f);
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
            arr[y, x] = (arr[y, x] - min) / range;

        // 원본 높이 분포에 맞추기 (Python: arr * std*3.5 + mean - std*1.2)
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
            arr[y, x] = Mathf.Clamp01(arr[y, x] * p.heightStd * 3.5f
                                       + p.heightMean - p.heightStd * 1.2f);
        return arr;
    }

    // ── 원본 참조 없이 노이즈+바이옴 규칙으로 지형 생성 (HTML 시뮬레이터와 동일 로직)
    public static float[,] GenerateFromScratch(
        int H, int W, int S,
        float baseScale, float detailScale, float detailStrength,
        float biomeScale, float mountainExp, float mountainHeight,
        bool flatMaskEnabled,
        float ox1, float oy1, float ox2, float oy2, float ox3, float oy3)
    {
        var full = new float[H, W];
        float blendPixels = S * 0.5f;

        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            // 1. 기본 고도 노이즈
            float baseVal   = (Mathf.PerlinNoise(x * baseScale   + ox1, y * baseScale   + oy1));
            float detailVal = (Mathf.PerlinNoise(x * detailScale  + ox2, y * detailScale  + oy2) - 0.5f) * detailStrength;
            float elevation = Mathf.Clamp01(baseVal + detailVal);

            // 2. 바이옴 맵 (0=평야, 1=산맥)
            float biomeRaw = Mathf.PerlinNoise(x * biomeScale + ox3, y * biomeScale + oy3);

            // 오른쪽 아래 4분면 → 평야 강제 (경계 블렌딩)
            float maskStrength = 0f;
            if (flatMaskEnabled && x > W / 2 && y > H / 2)
            {
                float wx = Mathf.Clamp01((x - W / 2f) / blendPixels);
                float wy = Mathf.Clamp01((y - H / 2f) / blendPixels);
                wx = wx * wx * (3f - 2f * wx);
                wy = wy * wy * (3f - 2f * wy);
                maskStrength = wx * wy;
            }
            biomeRaw = biomeRaw * (1f - maskStrength);

            // 대비 증가 (0.3 이하 = 평야, 이상 = 산맥)
            float biomeVal = Mathf.Clamp01((biomeRaw - 0.3f) * 2f);

            // 3. 평야/산맥 합성
            float plainVal    = elevation * 0.6f;
            float mountainVal = Mathf.Pow(elevation, mountainExp) * mountainHeight;
            full[y, x] = Mathf.Clamp01(plainVal * (1f - biomeVal) + mountainVal * biomeVal);
        }

        return full;
    }

    // ── Box 필터 기반 Gaussian blur (prefix sum, 경량)
    public static float[,] GaussianBlur(float[,] src, int H, int W, int radius)
    {
        var tmp = new float[H, W];
        var dst = new float[H, W];

        for (int y = 0; y < H; y++)
        {
            var ps = new float[W + 1];
            for (int x = 0; x < W; x++) ps[x+1] = ps[x] + src[y, x];
            for (int x = 0; x < W; x++)
            {
                int x0 = Mathf.Max(0, x-radius), x1 = Mathf.Min(W-1, x+radius);
                tmp[y, x] = (ps[x1+1] - ps[x0]) / (x1 - x0 + 1);
            }
        }
        for (int x = 0; x < W; x++)
        {
            var ps = new float[H + 1];
            for (int y = 0; y < H; y++) ps[y+1] = ps[y] + tmp[y, x];
            for (int y = 0; y < H; y++)
            {
                int y0 = Mathf.Max(0, y-radius), y1 = Mathf.Min(H-1, y+radius);
                dst[y, x] = (ps[y1+1] - ps[y0]) / (y1 - y0 + 1);
            }
        }
        return dst;
    }

    // ── t1 영역(오른쪽 아래) 산 높이 상한 적용
    //   페이드 구간을 경계 양쪽(t1 안 + 인접 타일)에 걸쳐 적용
    //   → 경계선에서 절벽 없이 자연스럽게 이어짐
    public static void ApplyT1Cap(float[,] full, TerrainAnalysisData p)
    {
        int   S        = p.tileRes;
        int   H        = full.GetLength(0), W = full.GetLength(1);
        float cap      = p.t1HeightCap;
        float fadeHalf = S * 0.5f;   // 경계 양쪽으로 각각 절반 타일씩 페이드

        // 전체 픽셀에 대해 처리 — 경계 바깥(t2/t4)도 페이드 구간에 포함
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            float excess = full[y,x] - cap;
            if (excess <= 0f) continue;

            // t1 경계(S-1)로부터의 거리를 -fadeHalf~+fadeHalf 구간으로 정규화
            // -1=완전 바깥, 0=경계, +1=완전 안쪽
            float tx = ((x - (S-1)) + fadeHalf) / (2f * fadeHalf);
            float ty = ((y - (S-1)) + fadeHalf) / (2f * fadeHalf);
            tx = Mathf.Clamp01(tx);
            ty = Mathf.Clamp01(ty);

            // 두 축 모두 안쪽일수록 weight 증가 (곱으로 코너 처리)
            float wx     = tx * tx * (3f - 2f * tx);   // smoothstep
            float wy     = ty * ty * (3f - 2f * ty);
            float weight = wx * wy;

            full[y, x] = Mathf.Clamp01(full[y,x] - excess * weight);
        }
    }

    // ── 수면 구역 자동 탐지 + 적용
    //   낮고 평평한 곳을 numWaterZones개 찾아서
    //   각 구역의 최솟값 = 수면 높이 → 그 이하만 끌어올림
    public static void DetectAndApplyWaterZones(float[,] full, TerrainAnalysisData p)
    {
        int H        = full.GetLength(0), W = full.GetLength(1);
        int zoneHalf = p.waterZoneSize / 2;
        int minDist  = (int)(p.waterZoneSize * 1.3f);
        int M        = p.waterZoneSize;

        var slope = new float[H, W];
        for (int y = 1; y < H - 1; y++)
        for (int x = 1; x < W - 1; x++)
        {
            float gx = (full[y, x+1] - full[y, x-1]) * 0.5f;
            float gy = (full[y+1, x] - full[y-1, x]) * 0.5f;
            slope[y, x] = Mathf.Sqrt(gx*gx + gy*gy);
        }

        float hMin=float.MaxValue, hMax=float.MinValue;
        float sMin=float.MaxValue, sMax=float.MinValue;
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            if (full[y,x]  < hMin) hMin = full[y,x];
            if (full[y,x]  > hMax) hMax = full[y,x];
            if (slope[y,x] < sMin) sMin = slope[y,x];
            if (slope[y,x] > sMax) sMax = slope[y,x];
        }

        // score: 낮고 평탄할수록 낮은 값
        var score = new float[H, W];
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            if (y < M || y >= H - M || x < M || x >= W - M)
            { score[y, x] = float.MaxValue; continue; }
            float hn = (full[y,x]  - hMin) / (hMax - hMin + 1e-8f);
            float sn = (slope[y,x] - sMin) / (sMax - sMin + 1e-8f);
            score[y, x] = hn * 0.7f + sn * 0.3f;
        }

        int found     = 0;
        int minDistSq = minDist * minDist;

        while (found < p.numWaterZones)
        {
            float minVal = float.MaxValue; int bx = -1, by = -1;
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                if (score[y,x] < minVal) { minVal = score[y,x]; bx = x; by = y; }
            if (minVal >= float.MaxValue) break;

            int r0 = Mathf.Max(0, by - zoneHalf), r1 = Mathf.Min(H, by + zoneHalf);
            int c0 = Mathf.Max(0, bx - zoneHalf), c1 = Mathf.Min(W, bx + zoneHalf);

            // 구역 최솟값 = 이 지형의 자연스러운 수면 높이
            float waterHeight = float.MaxValue;
            for (int y = r0; y < r1; y++)
            for (int x = c0; x < c1; x++)
                if (full[y,x] < waterHeight) waterHeight = full[y,x];

            // 수면 이하만 끌어올림
            for (int y = r0; y < r1; y++)
            for (int x = c0; x < c1; x++)
                if (full[y,x] < waterHeight) full[y,x] = waterHeight;

            Debug.Log($"[WaterZone {found+1}] 중심({bx},{by}) 수면높이(norm)={waterHeight:F4}");
            found++;

            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int dx = x - bx, dy = y - by;
                if (dx*dx + dy*dy < minDistSq) score[y,x] = float.MaxValue;
            }
        }
    }
}
