using FMODUnity;
using UnityEngine;

public readonly struct CameraSetupResult
{
    public Camera Camera { get; }
    public FirstPersonCamera FirstPersonCamera { get; }

    public CameraSetupResult(Camera camera, FirstPersonCamera firstPersonCamera)
    {
        Camera = camera;
        FirstPersonCamera = firstPersonCamera;
    }
}

public static class PlayerCameraRigSetup
{
    public static CameraSetupResult EnsureLocalCamera(Transform target)
    {
        FirstPersonCamera firstPersonCamera = Object.FindObjectOfType<FirstPersonCamera>();
        Camera camera;

        if (firstPersonCamera == null)
        {
            GameObject cameraRig = new GameObject("LocalPlayerCamera");
            camera = cameraRig.AddComponent<Camera>();
            cameraRig.AddComponent<AudioListener>();
            cameraRig.AddComponent<StudioListener>();
            firstPersonCamera = cameraRig.AddComponent<FirstPersonCamera>();
        }
        else
        {
            camera = firstPersonCamera.GetComponent<Camera>();
            if (camera == null)
                camera = firstPersonCamera.gameObject.AddComponent<Camera>();

            if (firstPersonCamera.GetComponent<AudioListener>() == null)
                firstPersonCamera.gameObject.AddComponent<AudioListener>();

            if (firstPersonCamera.GetComponent<StudioListener>() == null)
                firstPersonCamera.gameObject.AddComponent<StudioListener>();
        }

        firstPersonCamera.Target = target;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        return new CameraSetupResult(camera, firstPersonCamera);
    }
}
