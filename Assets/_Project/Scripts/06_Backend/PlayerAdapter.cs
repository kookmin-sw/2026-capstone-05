using System.Reflection;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;

public class PlayerAdapter : MonoBehaviour
{
    private const string LogPrefix = "[06_Backend][PlayerAdapter]";
    private const string PlayerCameraObjectName = "PlayerCamera";

    private bool _isInitialized;
    private bool _lastLocalState;

    private PlayerNetworkSetup _networkSetup;
    [SerializeField] private GameObject playerCameraPrefab;

    public void ApplyNetworkSetup(bool isLocalPlayer, string reason)
    {
        if (_isInitialized && _lastLocalState == isLocalPlayer)
        {
            Debug.Log($"{LogPrefix} 중복 초기화 방지: 동일 상태 재호출 건너뜀. name={name}, isLocal={isLocalPlayer}, reason={reason}");
            return;
        }

        _networkSetup = _networkSetup != null ? _networkSetup : GetComponent<PlayerNetworkSetup>();
        if (_networkSetup == null)
        {
            Debug.LogWarning($"{LogPrefix} PlayerNetworkSetup을 찾지 못했습니다. name={name}");
            return;
        }

        _networkSetup.isLocalPlayerTest = isLocalPlayer;
        Debug.Log($"{LogPrefix} isLocalPlayerTest 값 설정 완료. name={name}, value={isLocalPlayer}, reason={reason}");

        Debug.Log($"{LogPrefix} InitializeNetworkState() 호출 시점 진입. name={name}, reason={reason}");
        InvokeInitializeNetworkState(_networkSetup, isLocalPlayer);

        _isInitialized = true;
        _lastLocalState = isLocalPlayer;

        LogVisualModeResult(isLocalPlayer);
        LogInputModeResult(isLocalPlayer);
        VerifyCameraBinding(isLocalPlayer);
    }

    private static void InvokeInitializeNetworkState(PlayerNetworkSetup setup, bool isLocalPlayer)
    {
        MethodInfo initializeMethod = typeof(PlayerNetworkSetup).GetMethod(
            "InitializeNetworkState",
            BindingFlags.Instance | BindingFlags.NonPublic);

        if (initializeMethod != null)
        {
            initializeMethod.Invoke(setup, null);
            Debug.Log($"{LogPrefix} PlayerNetworkSetup.InitializeNetworkState() 반사 호출 완료. name={setup.name}, isLocal={isLocalPlayer}");
            return;
        }

        IPlayerNetworkConfigurable[] configurables = setup.GetComponentsInChildren<IPlayerNetworkConfigurable>(true);
        foreach (IPlayerNetworkConfigurable configurable in configurables)
        {
            configurable.ConfigureForNetwork(isLocalPlayer);
        }

        Debug.LogWarning($"{LogPrefix} InitializeNetworkState() 미탐지로 fallback ConfigureForNetwork 순회 실행. name={setup.name}, isLocal={isLocalPlayer}, configurableCount={configurables.Length}");
    }


    private void LogVisualModeResult(bool isLocalPlayer)
    {
        PlayerVisualMode visualMode = GetComponent<PlayerVisualMode>();
        if (visualMode == null)
        {
            Debug.LogWarning($"{LogPrefix} 1P/3P 모드 적용 확인 실패: PlayerVisualMode 없음. name={name}");
            return;
        }

        FieldInfo modelField = typeof(PlayerVisualMode).GetField("model1P", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo rendererField = typeof(PlayerVisualMode).GetField("renderers3P", BindingFlags.Instance | BindingFlags.NonPublic);

        GameObject model1P = modelField?.GetValue(visualMode) as GameObject;
        Renderer[] renderers3P = rendererField?.GetValue(visualMode) as Renderer[];

        bool model1PActive = model1P != null && model1P.activeSelf;
        bool allShadowsOnly = true;
        bool has3PRenderer = false;

        if (renderers3P != null)
        {
            foreach (Renderer renderer in renderers3P)
            {
                if (renderer == null)
                    continue;

                has3PRenderer = true;
                if (renderer.shadowCastingMode != ShadowCastingMode.ShadowsOnly)
                {
                    allShadowsOnly = false;
                    break;
                }
            }
        }

        Debug.Log($"{LogPrefix} 1P/3P 모드 적용 결과. name={name}, expectedLocal={isLocalPlayer}, model1PActive={model1PActive}, has3PRenderer={has3PRenderer}, all3PShadowOnly={allShadowsOnly}");
    }

    private void LogInputModeResult(bool isLocalPlayer)
    {
        PlayerInputHandler inputHandler = GetComponent<PlayerInputHandler>();
        if (inputHandler == null)
        {
            Debug.LogWarning($"{LogPrefix} 입력 허용/차단 확인 실패: PlayerInputHandler 없음. name={name}");
            return;
        }

        FieldInfo inputActionsField = typeof(PlayerInputHandler).GetField("inputActions", BindingFlags.Instance | BindingFlags.NonPublic);
        object inputActions = inputActionsField?.GetValue(inputHandler);

        bool? inputEnabled = null;
        if (inputActions != null)
        {
            PropertyInfo enabledProperty = inputActions.GetType().GetProperty("enabled", BindingFlags.Instance | BindingFlags.Public);
            if (enabledProperty != null)
            {
                object enabledValueRaw = enabledProperty.GetValue(inputActions);
                if (enabledValueRaw is bool enabledValue)
                {
                    inputEnabled = enabledValue;
                }
            }
        }

        Debug.Log($"{LogPrefix} 입력 허용/차단 결과. name={name}, expectedLocal={isLocalPlayer}, inputActionsEnabled={(inputEnabled.HasValue ? inputEnabled.Value.ToString() : "Unknown")}");
    }

    private void VerifyCameraBinding(bool isLocalPlayer)
    {
        if (isLocalPlayer)
        {
            EnsureRenderCameraReady();
        }

        CinemachineCamera virtualCamera = isLocalPlayer
            ? EnsureLocalCinemachineCamera()
            : FindAnyObjectByType<CinemachineCamera>();

        if (virtualCamera == null)
        {
            Debug.LogWarning($"{LogPrefix} 카메라 연결 실패: CinemachineCamera를 찾지 못했습니다. name={name}");
            return;
        }

        Transform cameraTarget = TryGetCameraTarget();
        if (cameraTarget == null)
        {
            Debug.LogWarning($"{LogPrefix} 카메라 연결 실패: PlayerController.CameraTransform을 찾지 못했습니다. name={name}");
            return;
        }

        if (isLocalPlayer)
        {
            bool followMatched = virtualCamera.Follow == cameraTarget;
            bool lookAtMatched = virtualCamera.LookAt == cameraTarget;
            if (followMatched && lookAtMatched)
            {
                Debug.Log($"{LogPrefix} 카메라 연결 성공. source=SceneOrPlayerCameraPrefab, local={isLocalPlayer}, follow={virtualCamera.Follow?.name}, lookAt={virtualCamera.LookAt?.name}");
            }
            else
            {
                Debug.LogWarning($"{LogPrefix} 카메라 연결 확인 필요. local={isLocalPlayer}, followMatched={followMatched}, lookAtMatched={lookAtMatched}");
            }
        }
        else
        {
            Debug.Log($"{LogPrefix} 리모트 플레이어 카메라 비활성 경로 확인 완료. name={name}");
        }
    }

    private CinemachineCamera EnsureLocalCinemachineCamera()
    {
        CinemachineCamera existingCamera = FindPlayerCameraInstance();
        if (existingCamera != null)
        {
            return existingCamera;
        }

        if (playerCameraPrefab != null)
        {
            GameObject instantiated = Instantiate(playerCameraPrefab);
            instantiated.name = playerCameraPrefab.name;

            CinemachineCamera prefabCamera = instantiated.GetComponent<CinemachineCamera>();
            if (prefabCamera != null)
            {
                Debug.Log($"{LogPrefix} PlayerCamera.prefab 인스턴스 생성 성공. source=SerializedField");
                return prefabCamera;
            }

            Debug.LogWarning($"{LogPrefix} PlayerCamera.prefab에는 CinemachineCamera 컴포넌트가 없습니다. source=SerializedField");
            Destroy(instantiated);
        }
        else
        {
            Debug.LogWarning($"{LogPrefix} PlayerCamera.prefab 참조가 비어 있습니다. PlayerAdapter.playerCameraPrefab에 프리팹을 할당해 주세요.");
        }
        Debug.LogWarning($"{LogPrefix} 씬에 PlayerCamera 인스턴스를 미리 배치하거나 PlayerAdapter.playerCameraPrefab을 설정해 주세요.");

        return FindPlayerCameraInstance();
    }

    private static CinemachineCamera FindPlayerCameraInstance()
    {
        CinemachineCamera[] allCameras = FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (CinemachineCamera candidate in allCameras)
        {
            if (candidate == null)
                continue;

            if (string.Equals(candidate.gameObject.name, PlayerCameraObjectName, System.StringComparison.Ordinal))
                return candidate;
        }

        return FindAnyObjectByType<CinemachineCamera>();
    }

    private void EnsureRenderCameraReady()
    {
        Camera renderCamera = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
        if (renderCamera == null)
        {
            GameObject cameraGo = new("Main Camera");
            cameraGo.tag = "MainCamera";
            renderCamera = cameraGo.AddComponent<Camera>();
            Debug.LogWarning($"{LogPrefix} 씬에 렌더 카메라가 없어 런타임 Main Camera를 생성했습니다.");
        }

        if (!renderCamera.enabled)
        {
            renderCamera.enabled = true;
        }

        CinemachineBrain brain = renderCamera.GetComponent<CinemachineBrain>();
        if (brain == null)
        {
            brain = renderCamera.gameObject.AddComponent<CinemachineBrain>();
            Debug.Log($"{LogPrefix} 렌더 카메라에 CinemachineBrain을 추가했습니다. camera={renderCamera.name}");
        }

        if (renderCamera.GetComponent<AudioListener>() == null && FindAnyObjectByType<AudioListener>() == null)
        {
            renderCamera.gameObject.AddComponent<AudioListener>();
        }
    }

    private Transform TryGetCameraTarget()
    {
        PlayerController controller = GetComponent<PlayerController>();
        return controller != null ? controller.CameraTransform : null;
    }
}
