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

    [Header("Action Triggers (One-shot)")]
    public bool JumpTriggered { get; private set; }
    public bool CrouchTriggered { get; private set; }
    public bool InteractTriggered { get; private set; }
    public bool ActionTriggered { get; private set; }

    private void Awake()
    {
        inputActions = new PlayerInputActions();

        // Triggers
        inputActions.Player.Jump.started += ctx => JumpTriggered = true;
        inputActions.Player.Crouch.started += ctx => CrouchTriggered = true;
        inputActions.Player.Interact.started += ctx => InteractTriggered = true;
        inputActions.Player.Action.started += ctx => ActionTriggered = true;

        // Holds
        inputActions.Player.Sprint.started += ctx => IsSprinting = true;
        inputActions.Player.Sprint.canceled += ctx => IsSprinting = false;
        inputActions.Player.Aim.started += ctx => IsAiming = true;
        inputActions.Player.Aim.canceled += ctx => IsAiming = false;
    }

    private void Update()
    {
        // Pollings
        MoveInput = inputActions.Player.Move.ReadValue<Vector2>();
        LookInput = inputActions.Player.Look.ReadValue<Vector2>();
    }

    private void OnEnable()
    {
        inputActions.Enable();
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

    /// <summary>
    /// 플레이어의 조작 차단/복구
    /// </summary>
    public void SetInputActive(bool isActive)
    {
        if (isActive)
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
        JumpTriggered = false;
        CrouchTriggered = false;
        InteractTriggered = false;
        ActionTriggered = false;
    }


    public void ConfigureForNetwork(bool isLocalPlayer)
    {
        if (isLocalPlayer)
        {
            SetInputActive(true);
        }
        else
        {
            SetInputActive(false);
        }
    }
}
