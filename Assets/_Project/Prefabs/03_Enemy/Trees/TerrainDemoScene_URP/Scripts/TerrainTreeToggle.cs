using UnityEngine;

public class TerrainTreeToggle : MonoBehaviour
{
    private void OnEnable()
    {
        var terrains = FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Terrain terrain in terrains)
        {
            terrain.drawTreesAndFoliage = false;
        }

        RenderSettings.fogDensity = (float)0.0001;
    }

    private void OnDisable()
    {
        var terrains = FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Terrain terrain in terrains)
        {
            terrain.drawTreesAndFoliage = true;
        }

        RenderSettings.fogDensity = (float)0.0005;
    }
}
