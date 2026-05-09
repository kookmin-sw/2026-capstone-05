using UnityEngine;

public enum SurfaceType
{
    Snow = 0,
    Stone = 1,
}

public class SurfaceMaterial : MonoBehaviour
{
    public SurfaceType surfaceType;
}