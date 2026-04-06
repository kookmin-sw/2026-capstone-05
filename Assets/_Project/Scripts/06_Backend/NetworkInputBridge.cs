using System.Reflection;
using Fusion;
using UnityEngine;

/// <summary>
/// 02_Player.PlayerInputHandler를 수정하지 않고 네트워크 입력으로 연결한다.
/// 로컬에서는 입력 수집, state authority에서는 입력 적용(주입)만 수행한다.
/// </summary>
public class NetworkInputBridge : MonoBehaviour
{
    private const string LogInput = "[NetworkInputBridge]";
    private const int LogIntervalFrames = 20;

    private static readonly BindingFlags AutoPropFlags = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo MoveField = typeof(PlayerInputHandler).GetField("<MoveInput>k__BackingField", AutoPropFlags);
    private static readonly FieldInfo LookField = typeof(PlayerInputHandler).GetField("<LookInput>k__BackingField", AutoPropFlags);
    private static readonly FieldInfo SprintField = typeof(PlayerInputHandler).GetField("<IsSprinting>k__BackingField", AutoPropFlags);
    private static readonly FieldInfo JumpField = typeof(PlayerInputHandler).GetField("<JumpTriggered>k__BackingField", AutoPropFlags);

    private PlayerInputHandler _inputHandler;
    private NetworkRunner _runner;
    private bool _isLocal;

    private void Awake()
    {
        _inputHandler = GetComponent<PlayerInputHandler>();
    }

    private void OnDisable()
    {
        if (_isLocal)
            BackendLocalInputRegistry.Unregister(_runner, this);
    }

    public void Initialize(NetworkRunner runner)
    {
        _runner = runner;
    }

    public void SetLocalOwnership(bool isLocal)
    {
        _isLocal = isLocal;

        if (_inputHandler != null)
            _inputHandler.ConfigureForNetwork(isLocal);

        if (isLocal)
        {
            BackendLocalInputRegistry.Register(_runner, this);
            Debug.Log($"{LogInput} Registered local input source. object={name}");
        }
        else
        {
            BackendLocalInputRegistry.Unregister(_runner, this);
            Debug.Log($"{LogInput} Unregistered local input source. object={name}");
        }
    }

    public bool TryBuildNetworkInput(out BackendNetworkInputData inputData)
    {
        inputData = default;

        if (!_isLocal || _inputHandler == null)
            return false;

        inputData = new BackendNetworkInputData
        {
            Move = _inputHandler.MoveInput,
            Look = _inputHandler.LookInput,
            JumpPressed = _inputHandler.JumpTriggered,
            SprintPressed = _inputHandler.IsSprinting
        };

        if (Time.frameCount % LogIntervalFrames == 0 && (inputData.Move.sqrMagnitude > 0.0001f || inputData.Look.sqrMagnitude > 0.0001f || inputData.JumpPressed))
        {
            Debug.Log($"{LogInput} Input captured(local). object={name}, move={inputData.Move}, look={inputData.Look}, jump={inputData.JumpPressed}, sprint={inputData.SprintPressed}");
        }

        if (_inputHandler.JumpTriggered)
            _inputHandler.ConsumeJump();

        return true;
    }

    public void ApplyAuthoritativeInput(in BackendNetworkInputData inputData, bool isAlsoInputAuthority)
    {
        if (_inputHandler == null)
            return;

        // host 자신의 플레이어는 기존 로컬 입력 흐름 그대로 유지.
        if (isAlsoInputAuthority)
            return;

        _inputHandler.enabled = false;

        MoveField?.SetValue(_inputHandler, inputData.Move);
        LookField?.SetValue(_inputHandler, inputData.Look);
        SprintField?.SetValue(_inputHandler, (bool)inputData.SprintPressed);
        JumpField?.SetValue(_inputHandler, (bool)inputData.JumpPressed);

        if (Time.frameCount % LogIntervalFrames == 0 && (inputData.Move.sqrMagnitude > 0.0001f || inputData.Look.sqrMagnitude > 0.0001f || inputData.JumpPressed))
        {
            Debug.Log($"{LogInput} Input applied(server authority). object={name}, move={inputData.Move}, look={inputData.Look}, jump={inputData.JumpPressed}, sprint={inputData.SprintPressed}");
        }
    }
}
