using System;
using UnityEngine;

public static class CastingSystem
{
    public static Action<float, bool, Color> OnCastingUpdated;

    public static void UpdateCasting(float progress, bool isActive, Color color = default)
    {
        if (color == default)
        {
            color = Color.white;
        }

        OnCastingUpdated?.Invoke(progress, isActive, color);
    }
}
