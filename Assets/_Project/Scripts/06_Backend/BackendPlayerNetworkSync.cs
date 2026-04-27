using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class BackendPlayerNetworkSync : NetworkBehaviour
{
    private const float SpawnLockDurationSeconds = 0.35f;

    private PlayerController _playerController;
    private PlayerInputHandler _inputHandler;
    private PlayerAnimator _playerAnimator;
    private Transform _cameraTransform;
    private Vector3 _authoritativeSpawnPosition;
    private Quaternion _authoritativeSpawnRotation;
    private float _spawnLockRemainingSeconds;
    private bool _spawnGravityWasEnabled;
    private bool _spawnLockInitialized;

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
            BeginServerSpawnLock();
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
            UpdateServerSpawnLock();

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

    private void BeginServerSpawnLock()
    {
        _authoritativeSpawnPosition = transform.position;
        _authoritativeSpawnRotation = transform.rotation;
        _spawnLockRemainingSeconds = SpawnLockDurationSeconds;
        _spawnLockInitialized = true;

        if (_playerController != null)
        {
            _spawnGravityWasEnabled = _playerController.useGravity;
            _playerController.useGravity = false;
            _playerController.currentVelocity = Vector3.zero;
        }
    }

    private void UpdateServerSpawnLock()
    {
        if (!_spawnLockInitialized)
            return;

        if (_spawnLockRemainingSeconds > 0f)
        {
            _spawnLockRemainingSeconds -= Runner != null ? Runner.DeltaTime : Time.deltaTime;

            transform.SetPositionAndRotation(_authoritativeSpawnPosition, _authoritativeSpawnRotation);

            if (_playerController != null)
            {
                _playerController.currentVelocity = Vector3.zero;
            }

            return;
        }

        _spawnLockInitialized = false;

        if (_playerController != null)
        {
            _playerController.useGravity = _spawnGravityWasEnabled;
        }
    }

    public override void Render()
    {
        if (HasStateAuthority)
            return;

        // 서버가 받은 값을 그대로 전파해 적용합니다.
        transform.SetPositionAndRotation(NetworkPosition, NetworkRotation);

        if (_cameraTransform != null)
        {
            _cameraTransform.localRotation = Quaternion.Euler(CameraPitch, 0f, 0f);
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
