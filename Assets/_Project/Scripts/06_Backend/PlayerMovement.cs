using Fusion;
using UnityEngine;
using FMODUnity;

public class PlayerMovement : NetworkBehaviour
{
    private CharacterController _controller;
    private NetworkButtons _previousButtons;
    private FirstPersonCamera _firstPersonCamera;

    private readonly PlayerMovementMotor _movementMotor = new PlayerMovementMotor();
    private readonly PlayerLookController _lookController = new PlayerLookController();
    private readonly PlayerPostureController _postureController = new PlayerPostureController();

    [Header("Movement")]
    public float PlayerSpeed = 3f;
    public float SprintSpeed = 6f;
    public float CrouchSpeed = 1.5f;
    public float JumpForce = 5f;
    public float GravityValue = -9.81f;
    public float AirControlMultiplier = 1f;
    public float AirControlLerpSpeed = 10f;

    [Header("Crouch")]
    [Range(0.1f, 1f)] public float CrouchHeightRatio = 0.6f;
    public float CrouchTransitionSpeed = 10f;
    public LayerMask ObstacleLayer;

    [Header("Look (02_Player style)")]
    public float MouseSensitivity = 1.5f;
    public float GamepadLookSensitivity = 180f;
    public float UpDownRange = 80f;

    public Camera Camera;

    [Networked] private float VerticalRotation { get; set; }
    [Networked] private NetworkBool IsCrouching { get; set; }

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _postureController.Initialize(_controller, CrouchHeightRatio);
    }

    public override void Spawned()
    {
        Debug.Log($"Spawned - InputAuth:{HasInputAuthority}, StateAuth:{HasStateAuthority}, Local:{Object.InputAuthority} / {Object.StateAuthority}");

        if (!HasInputAuthority)
            return;

        CameraSetupResult cameraSetup = PlayerCameraRigSetup.EnsureLocalCamera(transform);
        Camera = cameraSetup.Camera;
        _firstPersonCamera = cameraSetup.FirstPersonCamera;

        _postureController.BindCamera(_firstPersonCamera);
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
            return;

        if (!Runner.TryGetInputForPlayer(Object.InputAuthority, out NetworkInputData inputData))
            return;

        if (Camera == null && HasInputAuthority)
        {
            Camera = Camera.main;
            if (Camera == null)
                return;
        }

        VerticalRotation = _lookController.ApplyLook(transform, inputData.Look, VerticalRotation, UpDownRange);
        _firstPersonCamera?.SetPitch(VerticalRotation);

        IsCrouching = _postureController.HandleCrouchToggle(inputData, _previousButtons, IsCrouching, _controller, transform, ObstacleLayer);

        bool isSprinting = !IsCrouching &&
                           inputData.Buttons.IsSet(NetworkInputData.Sprint) &&
                           inputData.Move.y > 0f;

        _movementMotor.Simulate(
            _controller,
            transform,
            inputData.Move,
            isSprinting,
            IsCrouching,
            inputData.Buttons.WasPressed(_previousButtons, NetworkInputData.Jump),
            PlayerSpeed,
            SprintSpeed,
            CrouchSpeed,
            JumpForce,
            GravityValue,
            AirControlMultiplier,
            AirControlLerpSpeed,
            Runner.DeltaTime);

        _postureController.ApplyTransition(_controller, _firstPersonCamera, IsCrouching, CrouchTransitionSpeed, Runner.DeltaTime);
        _previousButtons = inputData.Buttons;
    }

    public override void Render()
    {
        _postureController.ApplyTransition(_controller, _firstPersonCamera, IsCrouching, CrouchTransitionSpeed, Time.deltaTime);

        if (HasInputAuthority && _firstPersonCamera != null)
            _firstPersonCamera.SetPitch(VerticalRotation);
    }
}
