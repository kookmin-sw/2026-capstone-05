using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class BackendPlayerNetworkSync : NetworkBehaviour
{
    private const float ProxyCorrectionSpeed = 14f;
    private const float InputAuthorityCorrectionSpeed = 8f;
    private const float InputAuthoritySnapDistance = 2.5f;
    private const float InputAuthoritySnapAngle = 40f;
    private const float InputAuthorityHardSnapDistance = 6f;
    private const float InputAuthorityHardSnapAngle = 100f;
    private const float PositionDeadZone = 0.03f;
    private const float RotationDeadZoneAngle = 1.5f;

    private PlayerController _playerController;
    private PlayerInputHandler _inputHandler;
    private PlayerAnimator _playerAnimator;
    private Transform _cameraTransform;

    [Networked] private Vector3 NetworkPosition { get; set; }
    [Networked] private Quaternion NetworkRotation { get; set; }
    [Networked] private float CameraPitch { get; set; }
    [Networked] private Vector2 NetworkMoveInput { get; set; }
    [Networked] private NetworkBool NetworkIsGrounded { get; set; }
    [Networked] private NetworkBool NetworkIsSprinting { get; set; }
    [Networked] private NetworkBool NetworkIsCrouching { get; set; }

    public override void Spawned()
    {
        _playerController = GetComponent<PlayerController>();
        _inputHandler = GetComponent<PlayerInputHandler>();
        _playerAnimator = GetComponent<PlayerAnimator>();
        _cameraTransform = _playerController != null ? _playerController.CameraTransform : null;

        // 입력 권한이 있는 클라이언트는 로컬에서 즉시 시뮬레이션해 체감 지연을 줄입니다.
        if (_playerController != null && !HasStateAuthority && !Object.HasInputAuthority)
        {
            _playerController.enabled = false;
        }

        SyncInputOverrideState();

        if (HasStateAuthority)
        {
            NetworkPosition = transform.position;
            NetworkRotation = transform.rotation;
            CameraPitch = ReadPitch();
            NetworkMoveInput = Vector2.zero;
            NetworkIsGrounded = true;
            NetworkIsSprinting = false;
            NetworkIsCrouching = false;
        }
    }

    public override void FixedUpdateNetwork()
    {
        SyncInputOverrideState();

        if (HasStateAuthority)
        {
            if (_inputHandler != null &&
                !Object.HasInputAuthority &&
                GetInput(out BackendPlayerNetworkInput input))
            {
                _inputHandler.ApplyNetworkSnapshot(new PlayerInputSnapshot
                {
                    Move = input.Move,
                    Look = input.Look,
                    Sprint = input.Buttons.IsSet(BackendPlayerNetworkInput.SprintButton),
                    Jump = input.Buttons.IsSet(BackendPlayerNetworkInput.JumpButton),
                    Crouch = input.Buttons.IsSet(BackendPlayerNetworkInput.CrouchButton),
                    Interact = input.Buttons.IsSet(BackendPlayerNetworkInput.InteractButton),
                    Action = input.Buttons.IsSet(BackendPlayerNetworkInput.ActionButton)
                });
            }

            NetworkPosition = transform.position;
            NetworkRotation = transform.rotation;
            CameraPitch = ReadPitch();
            NetworkMoveInput = _inputHandler != null ? _inputHandler.MoveInput : Vector2.zero;
            NetworkIsGrounded = _playerController != null && _playerController.IsGrounded;
            NetworkIsSprinting = _inputHandler != null && _inputHandler.IsSprinting;
            NetworkIsCrouching = _playerController != null &&
                                _playerController.GroundedState != null &&
                                _playerController.GroundedState.CurrentPosture == PlayerGroundedPosture.Crouching;
        }
    }

    public override void Render()
    {
        if (HasStateAuthority)
            return;

        float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
        float positionDistance = Vector3.Distance(transform.position, NetworkPosition);
        float rotationAngle = Quaternion.Angle(transform.rotation, NetworkRotation);
        bool isInputAuthority = Object != null && Object.HasInputAuthority;

        // 입력 권한 오브젝트는 보정 불일치를 줄이되, 강한 스냅으로 인한 "원래 방향으로 끊겨 돌아가는" 체감을 최소화합니다.
        if (isInputAuthority && (positionDistance >= InputAuthorityHardSnapDistance || rotationAngle >= InputAuthorityHardSnapAngle))
        {
            transform.SetPositionAndRotation(NetworkPosition, NetworkRotation);
        }
        else
        {
            float correctionSpeed = isInputAuthority ? InputAuthorityCorrectionSpeed : ProxyCorrectionSpeed;

            if (isInputAuthority && positionDistance < InputAuthoritySnapDistance && rotationAngle < InputAuthoritySnapAngle)
            {
                correctionSpeed *= 0.7f;
            }

            float correctionFactor = 1f - Mathf.Exp(-correctionSpeed * deltaTime);

            if (positionDistance > PositionDeadZone)
            {
                transform.position = Vector3.Lerp(transform.position, NetworkPosition, correctionFactor);
            }

            if (rotationAngle > RotationDeadZoneAngle)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, NetworkRotation, correctionFactor);
            }
        }

        if (_cameraTransform != null)
        {
            float cameraCorrectionSpeed = isInputAuthority ? InputAuthorityCorrectionSpeed : ProxyCorrectionSpeed;
            float cameraLerpFactor = 1f - Mathf.Exp(-cameraCorrectionSpeed * deltaTime);
            Quaternion targetLocal = Quaternion.Euler(CameraPitch, 0f, 0f);
            _cameraTransform.localRotation = Quaternion.Slerp(_cameraTransform.localRotation, targetLocal, cameraLerpFactor);
        }

        ApplyProxyAnimationState();
    }

    private void ApplyProxyAnimationState()
    {
        if (_playerAnimator == null)
            return;

        _playerAnimator.SetGrounded(NetworkIsGrounded);
        _playerAnimator.SetSprinting(NetworkIsSprinting);
        _playerAnimator.SetCrouching(NetworkIsCrouching);
        _playerAnimator.UpdateMovement(NetworkMoveInput);
    }

    private float ReadPitch()
    {
        if (_cameraTransform == null)
            return 0f;

        float rawX = _cameraTransform.localEulerAngles.x;
        if (rawX > 180f)
            rawX -= 360f;

        return rawX;
    }

    private void SyncInputOverrideState()
    {
        if (_inputHandler == null || Object == null)
            return;

        _inputHandler.SetNetworkInputOverride(!Object.HasInputAuthority);
    }
}
