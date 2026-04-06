using System.Reflection;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class PlayerAdapter : MonoBehaviour
{
    private const string LogPrefix = "[06_Backend][PlayerAdapter]";
    private const string PlayerCameraPrefabPath = "Assets/_Project/Prefabs/02_Player/PlayerCamera.prefab";

    private bool _isInitialized;
    private bool _lastLocalState;

    private PlayerNetworkSetup _networkSetup;

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
        CinemachineCamera existingCamera = FindAnyObjectByType<CinemachineCamera>();
        if (existingCamera != null)
        {
            return existingCamera;
        }

#if UNITY_EDITOR
        GameObject playerCameraPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerCameraPrefabPath);
        if (playerCameraPrefab != null)
        {
            GameObject instantiated = Instantiate(playerCameraPrefab);
            instantiated.name = playerCameraPrefab.name;

            CinemachineCamera prefabCamera = instantiated.GetComponent<CinemachineCamera>();
            if (prefabCamera != null)
            {
                Debug.Log($"{LogPrefix} PlayerCamera.prefab 인스턴스 생성 성공. path={PlayerCameraPrefabPath}");
                return prefabCamera;
            }

            Debug.LogWarning($"{LogPrefix} PlayerCamera.prefab에는 CinemachineCamera 컴포넌트가 없습니다. path={PlayerCameraPrefabPath}");
            Destroy(instantiated);
        }
        else
        {
            Debug.LogWarning($"{LogPrefix} PlayerCamera.prefab 로드 실패. path={PlayerCameraPrefabPath}");
        }
#else
        Debug.LogWarning($"{LogPrefix} 런타임 빌드에서는 에디터 전용 PlayerCamera.prefab 자동 로드가 비활성화됩니다. 씬에 PlayerCamera를 배치하세요.");
#endif

        return null;
    }

    private Transform TryGetCameraTarget()
    {
        PlayerController controller = GetComponent<PlayerController>();
        return controller != null ? controller.CameraTransform : null;
    }
}
