using Fusion;
using Unity.Cinemachine;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class BackendPlayerNetworkAdapter : NetworkBehaviour
{
    public override void Spawned()
    {
        bool isLocal = Object.HasInputAuthority;

        EnsureCinemachineCameraForLocal(isLocal);
        ConfigureNetworkAwareComponents(isLocal);
    }

    private void ConfigureNetworkAwareComponents(bool isLocal)
    {
        var configurables = GetComponentsInChildren<IPlayerNetworkConfigurable>(true);
        foreach (IPlayerNetworkConfigurable configurable in configurables)
        {
            configurable.ConfigureForNetwork(isLocal);
        }

        CharacterController controller = GetComponent<CharacterController>();
        if (controller != null)
            controller.enabled = isLocal;

        PlayerController playerController = GetComponent<PlayerController>();
        if (playerController != null)
            playerController.enabled = isLocal;

        PlayerInputHandler inputHandler = GetComponent<PlayerInputHandler>();
        if (inputHandler != null)
            inputHandler.enabled = isLocal;

        PlayerCameraHandler cameraHandler = GetComponent<PlayerCameraHandler>();
        if (cameraHandler != null)
            cameraHandler.enabled = isLocal;

        Cursor.lockState = isLocal ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !isLocal;
    }

    private static void EnsureCinemachineCameraForLocal(bool isLocal)
    {
        if (!isLocal)
            return;

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            var cameraGo = new GameObject("Main Camera");
            mainCamera = cameraGo.AddComponent<Camera>();
            cameraGo.tag = "MainCamera";
        }

        if (mainCamera.GetComponent<CinemachineBrain>() == null)
            mainCamera.gameObject.AddComponent<CinemachineBrain>();

        if (UnityEngine.Object.FindAnyObjectByType<CinemachineCamera>() == null)
        {
            GameObject vcamGo = new("Runtime Cinemachine Camera");
            vcamGo.AddComponent<CinemachineCamera>();
            vcamGo.AddComponent<CinemachineBasicMultiChannelPerlin>();
        }
    }
}
