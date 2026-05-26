using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour, IPlayerNetworkConfigurable
{
    private PlayerInputActions inputActions;

    [Header("Movement Inputs (Polling)")]
    public Vector2 MoveInput { get; private set; }
    public Vector2 LookInput { get; private set; }

    [Header("Action States (Hold)")]
    public bool IsSprinting { get; private set; }
    public bool IsAiming { get; private set; } // 필요하다면 추가
    public bool IsCrouchPressed { get; private set; }

    [Header("Action Triggers (One-shot)")]
    public bool JumpTriggered { get; private set; }
    public bool CrouchTriggered { get; private set; }
    public bool InteractTriggered { get; private set; }
    public bool IsInteractPressed { get; private set; }
    public bool ActionTriggered { get; private set; }

    private bool _useNetworkInputOverride;
    private PlayerInputSnapshot _networkSnapshot;
    private bool _previousNetworkCrouch;
    private bool _networkConfigured;
    private bool _isLocalPlayer;

    private void Awake()
    {
        inputActions = new PlayerInputActions();

        // Triggers
        inputActions.Player.Jump.started += ctx => JumpTriggered = true;
        inputActions.Player.Crouch.started += ctx =>
        {
            IsCrouchPressed = true;
            CrouchTriggered = true;
        };
        inputActions.Player.Crouch.canceled += ctx => IsCrouchPressed = false;
        inputActions.Player.Interact.started += ctx =>
        {
            InteractTriggered = true;
            IsInteractPressed = true;
        };
        inputActions.Player.Interact.canceled += ctx => IsInteractPressed = false;
        inputActions.Player.Action.started += ctx => ActionTriggered = true;

        // Holds
        inputActions.Player.Sprint.started += ctx => IsSprinting = true;
        inputActions.Player.Sprint.canceled += ctx => IsSprinting = false;
        inputActions.Player.Aim.started += ctx => IsAiming = true;
        inputActions.Player.Aim.canceled += ctx => IsAiming = false;
    }

    private void Update()
    {
        if (ShouldBlockLocalGameplayInput())
        {
            ResetAllInputs();
            return;
        }

        if (_useNetworkInputOverride)
        {
            ApplySnapshotToCurrentState(_networkSnapshot);
            return;
        }

        // Pollings
        MoveInput = inputActions.Player.Move.ReadValue<Vector2>();
        LookInput = inputActions.Player.Look.ReadValue<Vector2>();
    }

    private void OnEnable()
    {
        if (!ShouldBlockLocalGameplayInput())
        {
            inputActions.Enable();
        }
        else
        {
            ResetAllInputs();
        }
    }

    private void OnDisable()
    {
        inputActions.Disable();
    }

    // One-shot 트리거는 소비 메서드를 통해 상태 초기화
    public void ConsumeJump() => JumpTriggered = false;
    public void ConsumeCrouch() => CrouchTriggered = false;
    public void ConsumeInteract() => InteractTriggered = false;
    public void ConsumeAction() => ActionTriggered = false;

    public void EnableInput()
    {
        SetInputActive(true);
    }
    public void DisableInput()
    {
        SetInputActive(false);
    }

    /// <summary>
    /// 플레이어의 조작 차단/복구
    /// </summary>
    public void SetInputActive(bool isActive)
    {
        if (isActive && !ShouldBlockLocalGameplayInput())
        {
            inputActions.Enable();
        }
        else
        {
            inputActions.Disable();
            ResetAllInputs();
        }
    }

    private void ResetAllInputs()
    {
        MoveInput = Vector2.zero;
        LookInput = Vector2.zero;
        IsSprinting = false;
        IsAiming = false;
        IsCrouchPressed = false;
        JumpTriggered = false;
        CrouchTriggered = false;
        InteractTriggered = false;
        IsInteractPressed = false;
        ActionTriggered = false;
        _previousNetworkCrouch = false;
    }

    public void ClearInputState()
    {
        ResetAllInputs();
    }

    private bool ShouldBlockLocalGameplayInput()
    {
        if (!PauseMenuManager.IsAnyUIOpen())
        {
            return false;
        }

        if (_networkConfigured)
        {
            return _isLocalPlayer;
        }

        return LocalPlayerReferenceResolver.TryGetLocalPlayer(out PlayerController localPlayer) &&
               localPlayer != null &&
               localPlayer.InputHandler == this;
    }



    public void SetNetworkInputOverride(bool enabled)
    {
        if (_useNetworkInputOverride == enabled)
            return;

        _useNetworkInputOverride = enabled;

        if (enabled)
        {
            if (inputActions != null)
                inputActions.Disable();
            ResetAllInputs();
            return;
        }

        if (isActiveAndEnabled && inputActions != null && !ShouldBlockLocalGameplayInput())
        {
            inputActions.Enable();
        }
        else
        {
            ResetAllInputs();
        }
    }

    public void ApplyNetworkSnapshot(PlayerInputSnapshot snapshot)
    {
        _networkSnapshot = snapshot;
    }

    private void ApplySnapshotToCurrentState(PlayerInputSnapshot snapshot)
    {
        MoveInput = snapshot.Move;
        LookInput = snapshot.Look;
        IsSprinting = snapshot.Sprint;
        IsCrouchPressed = snapshot.Crouch;

        if (snapshot.Jump)
            JumpTriggered = true;
        if (snapshot.Crouch && !_previousNetworkCrouch)
            CrouchTriggered = true;
        if (snapshot.Interact)
        {
            InteractTriggered = true;
            IsInteractPressed = true;
        }
        else
        {
            IsInteractPressed = false;
        }
        if (snapshot.Action)
            ActionTriggered = true;

        _previousNetworkCrouch = snapshot.Crouch;
    }

    public void ConfigureForNetwork(bool isLocalPlayer)
    {
        _networkConfigured = true;
        _isLocalPlayer = isLocalPlayer;

        if (isLocalPlayer)
        {
            SetInputActive(true);
        }
        else
        {
            SetInputActive(false);
        }
    }

    public string GetInteractKey()
    {
        // TODO: 게임패드 지원 시 컨트롤러 입력에 따른 키 표시도 필요
        return inputActions.Player.Interact.GetBindingDisplayString(0);
    }
}
