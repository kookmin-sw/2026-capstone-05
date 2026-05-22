using UnityEngine;

public static class PlayerAimRayProvider
{
    private static readonly Vector3 CenterViewportPoint = new(0.5f, 0.5f, 0f);

    public static bool TryGetAimRay(PlayerController player, out Ray ray)
    {
        return TryGetAimBasis(player, out ray, out _, out _, out _);
    }

    public static bool TryGetAimBasis(
        PlayerController player,
        out Ray ray,
        out Vector3 right,
        out Vector3 up,
        out Quaternion rotation)
    {
        Camera renderCamera = Camera.main;
        if (renderCamera != null && renderCamera.isActiveAndEnabled)
        {
            ray = renderCamera.ViewportPointToRay(CenterViewportPoint);
            Transform cameraTransform = renderCamera.transform;
            right = cameraTransform.right;
            up = cameraTransform.up;
            rotation = Quaternion.LookRotation(ray.direction, up);
            return true;
        }

        Transform fallbackTransform = player != null ? player.CameraTransform : null;
        if (fallbackTransform == null && player != null)
        {
            fallbackTransform = player.transform;
        }

        if (fallbackTransform == null)
        {
            ray = default;
            right = Vector3.right;
            up = Vector3.up;
            rotation = Quaternion.identity;
            return false;
        }

        ray = new Ray(fallbackTransform.position, fallbackTransform.forward);
        right = fallbackTransform.right;
        up = fallbackTransform.up;
        rotation = fallbackTransform.rotation;
        return true;
    }
}
