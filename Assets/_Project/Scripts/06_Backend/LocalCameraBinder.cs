using UnityEngine;

/// <summary>
/// 로컬 플레이어에게만 카메라/오디오를 자동 바인딩한다.
/// 메인 카메라를 생성/고정하지 않고, 플레이어 CameraTransform 하위로 붙여 함께 이동시킨다.
/// non-state local에서는 Pitch를 별도 적용해 Yaw만 도는 문제를 방지한다.
/// </summary>
public class LocalCameraBinder : MonoBehaviour
{
    private const string LogCamera = "[LocalCameraBinder]";

    private PlayerController _playerController;
    private RemotePitchCompensator _pitchCompensator;

    private void Awake()
    {
        _playerController = GetComponent<PlayerController>();
        _pitchCompensator = GetComponent<RemotePitchCompensator>();
        if (_pitchCompensator == null)
            _pitchCompensator = gameObject.AddComponent<RemotePitchCompensator>();
    }

    public void Apply(bool isLocalPlayer, bool isStateAuthority)
    {
        if (!isLocalPlayer)
        {
            _pitchCompensator.Configure(false, null, null);
            return;
        }

        if (_playerController == null || _playerController.CameraTransform == null)
            return;

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            GameObject cameraGo = new("Main Camera");
            cameraGo.tag = "MainCamera";
            mainCamera = cameraGo.AddComponent<Camera>();
        }

        EnsureSingleAudioListener(mainCamera);
        AttachCameraToPlayer(mainCamera.transform, _playerController.CameraTransform);

        // state authority가 아닌 로컬 플레이어는 카메라 시점을 로컬에서 보정한다.
        bool usePitchCompensator = !isStateAuthority;
        _pitchCompensator.Configure(usePitchCompensator, _playerController, GetComponent<PlayerInputHandler>());

        Debug.Log($"{LogCamera} Bound local camera. object={name}, stateAuth={isStateAuthority}, pitchCompensator={usePitchCompensator}, camera={mainCamera.name}");
    }

    private static void AttachCameraToPlayer(Transform cameraTransform, Transform playerCameraTransform)
    {
        if (cameraTransform.parent != playerCameraTransform)
            cameraTransform.SetParent(playerCameraTransform, false);

        cameraTransform.localPosition = Vector3.zero;
        cameraTransform.localRotation = Quaternion.identity;
    }

    private static void EnsureSingleAudioListener(Camera mainCamera)
    {
        AudioListener listener = mainCamera.GetComponent<AudioListener>();
        if (listener == null)
            listener = mainCamera.gameObject.AddComponent<AudioListener>();

        listener.enabled = true;

        AudioListener[] all = FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != listener)
                all[i].enabled = false;
        }
    }
}
