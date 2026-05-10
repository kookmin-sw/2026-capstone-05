using System;
using System.Collections.Generic;
using MapMagic.Core;
using UnityEngine;

[Serializable]
public class PlannedFarmField
{
    public RuntimeFieldPreset preset;
    public Vector3 position;
    public Quaternion rotation;
    public Vector2 footprintSize;
    public Vector2 footprintCenter;
    public PlannedFieldFootprintPart[] footprintParts;
    public Rect worldRect;
    public float targetWorldHeight;
    public float yawDegrees;
    public float scale;

    public PlannedFarmField(
        RuntimeFieldPreset preset,
        Vector3 position,
        Quaternion rotation,
        Vector2 footprintSize,
        Vector2 footprintCenter,
        PlannedFieldFootprintPart[] footprintParts,
        Rect worldRect,
        float targetWorldHeight,
        float yawDegrees,
        float scale)
    {
        this.preset = preset;
        this.position = position;
        this.rotation = rotation;
        this.footprintSize = footprintSize;
        this.footprintCenter = footprintCenter;
        this.footprintParts = footprintParts;
        this.worldRect = worldRect;
        this.targetWorldHeight = targetWorldHeight;
        this.yawDegrees = yawDegrees;
        this.scale = scale;
    }
}

[Serializable]
public struct RuntimeFieldFootprintPartData
{
    public Vector2 size;
    public Vector2 centerOffset;
    public float yawDegrees;
    public float groundLocalY;

    public RuntimeFieldFootprintPartData(Vector2 size, Vector2 centerOffset, float yawDegrees, float groundLocalY)
    {
        this.size = size;
        this.centerOffset = centerOffset;
        this.yawDegrees = yawDegrees;
        this.groundLocalY = groundLocalY;
    }
}

[Serializable]
public struct RuntimeFarmFieldFootprintData
{
    public Vector2 size;
    public Vector2 centerOffset;
    public float groundLocalY;
    public RuntimeFieldFootprintPartData[] parts;

    public RuntimeFarmFieldFootprintData(
        Vector2 size,
        Vector2 centerOffset,
        float groundLocalY,
        RuntimeFieldFootprintPartData[] parts)
    {
        this.size = size;
        this.centerOffset = centerOffset;
        this.groundLocalY = groundLocalY;
        this.parts = parts;
    }
}

[Serializable]
public struct PlannedFieldFootprintPart
{
    public Vector2 center;
    public Vector2 size;
    public float yawDegrees;

    public PlannedFieldFootprintPart(Vector2 center, Vector2 size, float yawDegrees)
    {
        this.center = center;
        this.size = size;
        this.yawDegrees = yawDegrees;
    }
}

public struct RuntimeTerrainSample
{
    public Terrain terrain;
    public Vector3 position;
    public Vector3 normal;
    public float slopeDegrees;
}

public struct RuntimeReservedArea
{
    public Rect rect;

    public RuntimeReservedArea(Rect rect)
    {
        this.rect = rect;
    }
}

public class RuntimeTerrainReservationMask
{
    private readonly List<RuntimeReservedArea> areas = new List<RuntimeReservedArea>();

    public IReadOnlyList<RuntimeReservedArea> Areas => areas;

    public void Clear()
    {
        areas.Clear();
    }

    public void Add(Rect rect)
    {
        areas.Add(new RuntimeReservedArea(rect));
    }

    public void AddField(PlannedFarmField field, float padding)
    {
        if (field == null)
        {
            return;
        }

        if (field.footprintParts == null || field.footprintParts.Length == 0)
        {
            Add(Pad(field.worldRect, padding));
            return;
        }

        for (int i = 0; i < field.footprintParts.Length; i++)
        {
            Add(Pad(RuntimeTerrainUtility.GetRotatedWorldRect(
                field.footprintParts[i].center,
                field.footprintParts[i].size,
                field.footprintParts[i].yawDegrees), padding));
        }
    }

    public bool Contains(Vector2 point, float extraPadding = 0f)
    {
        for (int i = 0; i < areas.Count; i++)
        {
            if (Pad(areas[i].rect, extraPadding).Contains(point))
            {
                return true;
            }
        }

        return false;
    }

    public bool Overlaps(Rect rect)
    {
        for (int i = 0; i < areas.Count; i++)
        {
            if (areas[i].rect.Overlaps(rect))
            {
                return true;
            }
        }

        return false;
    }

    public static Rect Pad(Rect rect, float padding)
    {
        if (padding <= 0f)
        {
            return rect;
        }

        return new Rect(
            rect.xMin - padding,
            rect.yMin - padding,
            rect.width + padding * 2f,
            rect.height + padding * 2f);
    }
}

public class RuntimeSpatialHash2D
{
    private readonly Dictionary<Vector2Int, List<Vector2>> cells = new Dictionary<Vector2Int, List<Vector2>>();
    private readonly float cellSize;
    private readonly float inverseCellSize;

    public RuntimeSpatialHash2D(float cellSize)
    {
        this.cellSize = Mathf.Max(0.01f, cellSize);
        inverseCellSize = 1f / this.cellSize;
    }

    public void Add(Vector2 point)
    {
        Vector2Int cell = GetCell(point);
        if (!cells.TryGetValue(cell, out List<Vector2> points))
        {
            points = new List<Vector2>();
            cells.Add(cell, points);
        }

        points.Add(point);
    }

    public bool HasPointWithinDistance(Vector2 point, float distance)
    {
        if (distance <= 0f)
        {
            return false;
        }

        float distanceSqr = distance * distance;
        int radius = Mathf.CeilToInt(distance / cellSize);
        Vector2Int center = GetCell(point);

        for (int z = -radius; z <= radius; z++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                Vector2Int cell = new Vector2Int(center.x + x, center.y + z);
                if (!cells.TryGetValue(cell, out List<Vector2> points))
                {
                    continue;
                }

                for (int i = 0; i < points.Count; i++)
                {
                    if ((points[i] - point).sqrMagnitude < distanceSqr)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private Vector2Int GetCell(Vector2 point)
    {
        return new Vector2Int(
            Mathf.FloorToInt(point.x * inverseCellSize),
            Mathf.FloorToInt(point.y * inverseCellSize));
    }
}

public static class RuntimeTerrainUtility
{
    public static Terrain[] ResolveTerrains(MapMagicObject mapMagic, Terrain[] explicitTerrains)
    {
        List<Terrain> terrains = new List<Terrain>();

        if (explicitTerrains != null)
        {
            for (int i = 0; i < explicitTerrains.Length; i++)
            {
                AddTerrainIfValid(terrains, explicitTerrains[i]);
            }
        }

        if (terrains.Count == 0 && mapMagic != null)
        {
            foreach (Terrain terrain in mapMagic.tiles.AllActiveTerrains())
            {
                AddTerrainIfValid(terrains, terrain);
            }
        }

        if (terrains.Count == 0)
        {
            Terrain[] activeTerrains = Terrain.activeTerrains;
            for (int i = 0; i < activeTerrains.Length; i++)
            {
                AddTerrainIfValid(terrains, activeTerrains[i]);
            }
        }

        return terrains.ToArray();
    }

    public static bool TrySample(Terrain[] terrains, Vector2 worldXZ, out RuntimeTerrainSample sample)
    {
        sample = default;

        if (!TryGetTerrainAt(terrains, worldXZ, out Terrain terrain))
        {
            return false;
        }

        TerrainData data = terrain.terrainData;
        Vector3 terrainPosition = terrain.transform.position;
        Vector3 size = data.size;

        float normalizedX = Mathf.InverseLerp(terrainPosition.x, terrainPosition.x + size.x, worldXZ.x);
        float normalizedZ = Mathf.InverseLerp(terrainPosition.z, terrainPosition.z + size.z, worldXZ.y);

        if (normalizedX < 0f || normalizedX > 1f || normalizedZ < 0f || normalizedZ > 1f)
        {
            return false;
        }

        float worldHeight = terrain.SampleHeight(new Vector3(worldXZ.x, 0f, worldXZ.y)) + terrainPosition.y;
        Vector3 normal = terrain.transform.TransformDirection(data.GetInterpolatedNormal(normalizedX, normalizedZ)).normalized;

        sample = new RuntimeTerrainSample
        {
            terrain = terrain,
            position = new Vector3(worldXZ.x, worldHeight, worldXZ.y),
            normal = normal,
            slopeDegrees = data.GetSteepness(normalizedX, normalizedZ)
        };

        return true;
    }

    public static bool TryGetTerrainAt(Terrain[] terrains, Vector2 worldXZ, out Terrain terrain)
    {
        terrain = null;

        if (terrains == null)
        {
            return false;
        }

        for (int i = 0; i < terrains.Length; i++)
        {
            Terrain candidate = terrains[i];
            if (candidate == null || candidate.terrainData == null)
            {
                continue;
            }

            if (GetWorldRect(candidate).Contains(worldXZ))
            {
                terrain = candidate;
                return true;
            }
        }

        return false;
    }

    public static Terrain ChooseTerrainByArea(Terrain[] terrains, System.Random random)
    {
        if (terrains == null || terrains.Length == 0)
        {
            return null;
        }

        float totalArea = 0f;
        for (int i = 0; i < terrains.Length; i++)
        {
            Terrain terrain = terrains[i];
            if (terrain == null || terrain.terrainData == null)
            {
                continue;
            }

            Vector3 size = terrain.terrainData.size;
            totalArea += Mathf.Max(0f, size.x * size.z);
        }

        if (totalArea <= 0f)
        {
            return terrains[0];
        }

        float pick = NextFloat(random, 0f, totalArea);
        float accumulated = 0f;

        for (int i = 0; i < terrains.Length; i++)
        {
            Terrain terrain = terrains[i];
            if (terrain == null || terrain.terrainData == null)
            {
                continue;
            }

            Vector3 size = terrain.terrainData.size;
            accumulated += Mathf.Max(0f, size.x * size.z);
            if (pick <= accumulated)
            {
                return terrain;
            }
        }

        return terrains[terrains.Length - 1];
    }

    public static Rect GetWorldRect(Terrain terrain)
    {
        Vector3 position = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        return new Rect(position.x, position.z, size.x, size.z);
    }

    public static Vector2 Rotate(Vector2 value, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);
        return new Vector2(value.x * cos + value.y * sin, -value.x * sin + value.y * cos);
    }

    public static Rect GetRotatedWorldRect(Vector2 center, Vector2 size, float yawDegrees)
    {
        float radians = yawDegrees * Mathf.Deg2Rad;
        float sin = Mathf.Abs(Mathf.Sin(radians));
        float cos = Mathf.Abs(Mathf.Cos(radians));
        Vector2 bounds = new Vector2(size.x * cos + size.y * sin, size.x * sin + size.y * cos);
        return new Rect(center.x - bounds.x * 0.5f, center.y - bounds.y * 0.5f, bounds.x, bounds.y);
    }

    public static float NextFloat(System.Random random, float min, float max)
    {
        return min + (float)random.NextDouble() * (max - min);
    }

    private static void AddTerrainIfValid(List<Terrain> terrains, Terrain terrain)
    {
        if (terrain == null || terrain.terrainData == null || terrains.Contains(terrain))
        {
            return;
        }

        terrains.Add(terrain);
    }
}
