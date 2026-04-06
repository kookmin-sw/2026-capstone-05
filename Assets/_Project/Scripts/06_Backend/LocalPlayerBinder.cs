using Fusion;
using UnityEngine;

/// <summary>
/// authority 판정과 초기화 순서를 관리한다.
/// 순서: authority 판정 -> 입력 등록 -> 제어권 설정 -> 1P/3P 표시 -> 카메라/오디오 바인딩.
/// </summary>
public class LocalPlayerBinder : MonoBehaviour
{
    private const string LogBinder = "[LocalPlayerBinder]";
    private const string LogFlow = "[BackendFlow]";

    private NetworkRunner _runner;
    private NetworkObject _networkObject;

    private NetworkInputBridge _inputBridge;
    private LocalCameraBinder _cameraBinder;
    private PlayerPresentationController _presentationController;
    private AuthorityStateController _authorityController;

    private bool _lastLocal;
    private bool _lastState;
    private bool _initialized;

    public void Initialize(
        NetworkRunner runner,
        NetworkObject networkObject,
        NetworkInputBridge inputBridge,
        LocalCameraBinder cameraBinder,
        PlayerPresentationController presentationController,
        AuthorityStateController authorityController)
    {
        _runner = runner;
        _networkObject = networkObject;
        _inputBridge = inputBridge;
        _cameraBinder = cameraBinder;
        _presentationController = presentationController;
        _authorityController = authorityController;

        _inputBridge.Initialize(runner);
        _initialized = true;
    }

    public void Apply(bool force)
    {
        if (!_initialized || _networkObject == null)
            return;

        bool isLocal = _networkObject.HasInputAuthority;
        bool isStateAuthority = _networkObject.HasStateAuthority;

        if (!force && _lastLocal == isLocal && _lastState == isStateAuthority)
            return;

        _lastLocal = isLocal;
        _lastState = isStateAuthority;

        Debug.Log($"{LogBinder} Apply. object={name}, local={isLocal}, state={isStateAuthority}, runnerLocal={_runner?.LocalPlayer}");

        _inputBridge.SetLocalOwnership(isLocal);
        _authorityController.Apply(isLocal, isStateAuthority);
        _presentationController.Apply(isLocal);
        _cameraBinder.Apply(isLocal, isStateAuthority);
    }

    public void ApplyNetworkInput(in BackendNetworkInputData inputData)
    {
        if (_networkObject == null || !_networkObject.HasStateAuthority)
            return;

        // state authority만 실제 mover(PlayerController)의 입력 상태를 갱신한다.
        _inputBridge.ApplyAuthoritativeInput(inputData, _networkObject.HasInputAuthority);

        if (Time.frameCount % 20 == 0 && (inputData.Move.sqrMagnitude > 0.0001f || inputData.Look.sqrMagnitude > 0.0001f || inputData.JumpPressed))
        {
            Debug.Log($"{LogFlow} Server calculate. object={name}, move={inputData.Move}, look={inputData.Look}, jump={inputData.JumpPressed}, sprint={inputData.SprintPressed}");
        }
    }

    public void ApplyReplicatedInput(in BackendNetworkInputData inputData)
    {
        if (_networkObject == null || _networkObject.HasStateAuthority)
            return;

        _inputBridge.ApplyAuthoritativeInput(inputData, _networkObject.HasInputAuthority);
    }
}
