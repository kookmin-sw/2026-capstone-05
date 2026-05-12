using UnityEngine;

public enum SurfaceType
{
    Snow = 0,
    Wood = 1,
    Stone = 2,
    Metal = 3,
}

public class SurfaceMaterial : MonoBehaviour
{
    public SurfaceType surfaceType;
}