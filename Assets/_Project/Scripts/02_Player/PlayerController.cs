using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour, IPlayerNetworkConfigurable
{
    public ItemData testItem;

    // FSM 및 상태 인스턴스 (FSM & State Instances)
    public PlayerStateMachine StateMachine { get; private set; }

    public PlayerGroundedState GroundedState { get; private set; }
    public PlayerAirborneState AirborneState { get; private set; }
    public PlayerInteractionState InteractionState { get; private set; }

    // 주요 컴포넌트 (Core Components)
    [Header("Components")]
    [SerializeField] private Transform cameraTransform;

    public CharacterController Controller { get; private set; }
    public PlayerInputHandler InputHandler { get; private set; }
    public PlayerAnimator Animator { get; private set; }
    public PlayerCondition Condition { get; private set; }
    public PlayerNoiseEmitter NoiseEmitter { get; private set; }
    public PlayerEquipment Equipment { get; private set; }
    public PlayerViewmodelController ViewmodelController { get; private set; }
    public DeathCameraDirector DeathCameraDirector { get; private set; }
    public Transform CameraTransform => cameraTransform;

    // 스탯 및 설정 (Stats & Settings)
    [Header("Network Settings")]
    public bool IsLocalPlayer { get; private set; }

    [Header("Movement Stats")]
    public float walkSpeed = 3f;
    public float sprintSpeed = 6f;
    public float crouchSpeed = 1.5f;
    public float jumpForce = 5f;
    public float gravityScale = 2f;

    [Header("Movement Settings")]
    public Vector3 currentVelocity = Vector3.zero;
    public bool useGravity = true;
    public bool IsGrounded { get; private set; }
    public bool canAction = true;

    [Header("Look Settings")]
    public float mouseSensitivity = 1.5f;
    public float upDownRange = 80f;
    public bool canLook = true;
    private float verticalRotation;

    [Header("Crouch Settings")]
    [Range(0.1f, 1f)] public float CrouchHeightRatio = 0.6f;
    public float crouchForwardOffset = 0.4f;
    public float crouchTransitionSpeed = 10f;
    public LayerMask obstacleLayer;

    public float StandingHeight { get; private set; }
    public float CrouchHeight { get; private set; }
    public float TargetHeight { get; set; }
    private Vector3 defaultCameraPosition;
    private Vector3 defaultCenter;

    [Header("Stamina Settings")]
    public float sprintStaminaCost = 15f; // 초당 스태미나 소모량
    public float minStaminaToSprint = 10f; // 다시 달리기를 시작하기 위한 최소 스태미나
    public float jumpStaminaCost = 10f; // 점프 시 소모되는 스태미나
    public float staminaRegenDelay = 1.5f;   // 소모 후 회복이 시작되기까지의 대기 시간
    public float idleRegenRate = 20f;
    public float crouchWalkRegenRate = 15f;
    public float walkRegenRate = 5f;
    public float airborneRegenRate = 0f;

    [Header("Camera Recoil")]
    public float recoilReturnSpeed = 2f; // 반동이 원위치로 돌아오는 속도
    private float currentRecoilOffset = 0f; // 현재 적용된 반동 수치

    private void Awake()
    {
        Controller = GetComponent<CharacterController>();
        InputHandler = GetComponent<PlayerInputHandler>();
        Animator = GetComponent<PlayerAnimator>();
        Condition = GetComponent<PlayerCondition>();
        NoiseEmitter = GetComponent<PlayerNoiseEmitter>();
        Equipment = GetComponent<PlayerEquipment>();
        ViewmodelController = GetComponentInChildren<PlayerViewmodelController>();
        DeathCameraDirector = GetComponentInChildren<DeathCameraDirector>();


        StateMachine = new PlayerStateMachine();

        GroundedState = new PlayerGroundedState(this, StateMachine);
        AirborneState = new PlayerAirborneState(this, StateMachine);
        InteractionState = new PlayerInteractionState(this, StateMachine);
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        StandingHeight = Controller.height;
        CrouchHeight = StandingHeight * CrouchHeightRatio;
        TargetHeight = StandingHeight;
        defaultCenter = Controller.center;

        if (CameraTransform != null)
        {
            defaultCameraPosition = CameraTransform.localPosition;
        }

        StateMachine.Initialize(GroundedState);

        if (testItem != null)
            Equipment.EquipItem(new ItemInstance(testItem));
    }

    private void OnEnable()
    {
        if (Condition != null)
        {
            if (Animator != null)
            {
                Condition.OnTakeDamageEvent += Animator.SetHitTrigger;
                Condition.OnDiedEvent += Animator.Die;
                Condition.OnReviveEvent += Animator.Revive;
            }
            if (InputHandler != null)
            {
                Condition.OnReviveEvent += InputHandler.EnableInput;
                Condition.OnDiedEvent += InputHandler.DisableInput;
            }
            if (DeathCameraDirector != null)
            {
                Condition.OnDiedEvent += DeathCameraDirector.PlayDeathSequence;
                Condition.OnReviveEvent += DeathCameraDirector.PlayReviveSequence;
            }
        }
    }

    private void OnDisable()
    {
        if (Condition != null)
        {
            if (Animator != null)
            {
                Condition.OnTakeDamageEvent -= Animator.SetHitTrigger;
                Condition.OnDiedEvent -= Animator.Die;
                Condition.OnReviveEvent -= Animator.Revive;
            }
            if (InputHandler != null)
            {
                Condition.OnReviveEvent -= InputHandler.EnableInput;
                Condition.OnDiedEvent -= InputHandler.DisableInput;
            }
            if (DeathCameraDirector != null)
            {
                Condition.OnDiedEvent -= DeathCameraDirector.PlayDeathSequence;
                Condition.OnReviveEvent -= DeathCameraDirector.PlayReviveSequence;
            }
        }
    }

    private void Update()
    {
        if (Condition != null && !Condition.IsAlive)
        {
            return;
        }

        CheckEnvironmentFlags();

        if (canLook)
        {
            HandleLook();
        }

        if (canAction)
        {
            HandleAction();
        }

        StateMachine.CurrentState.LogicUpdate();

        if (useGravity)
        {
            ApplyGravity();
        }

        Controller.Move(currentVelocity * Time.deltaTime);

        HandlePostureTransition();
    }

    private void FixedUpdate()
    {
        StateMachine.CurrentState.PhysicsUpdate();
    }


    private void CheckEnvironmentFlags()
    {
        IsGrounded = Controller.isGrounded;
    }

    private void ApplyGravity()
    {
        if (IsGrounded && currentVelocity.y < 0)
        {
            currentVelocity.y = -2f;
        }
        else
        {
            currentVelocity.y += Physics.gravity.y * gravityScale * Time.deltaTime;
        }
    }

    public void AddCameraRecoil(float recoilAmount, Vector3 viewmodelKickbackPos, Vector3 viewmodelKickbackRot)
    {
        currentRecoilOffset += recoilAmount;
        if (ViewmodelController != null)
        {
            ViewmodelController.ApplyRecoil(viewmodelKickbackPos, viewmodelKickbackRot);
        }
    }

    private void HandleLook()
    {
        if (InputHandler == null || CameraTransform == null)
        {
            return;
        }

        Vector2 lookInput = InputHandler.LookInput * mouseSensitivity;

        transform.Rotate(0f, lookInput.x, 0f);

        verticalRotation -= lookInput.y;
        verticalRotation = Mathf.Clamp(verticalRotation, -upDownRange, upDownRange);

        currentRecoilOffset = Mathf.Lerp(currentRecoilOffset, 0f, Time.deltaTime * recoilReturnSpeed);

        CameraTransform.localRotation = Quaternion.Euler(verticalRotation - currentRecoilOffset, 0f, 0f);
    }

    private void HandleAction()
    {
        if (InputHandler == null || Equipment == null)
        {
            return;
        }

        if (InputHandler.ActionTriggered)
        {
            Equipment.UseCurrentItem();

            InputHandler.ConsumeAction();
        }
    }

    private void HandlePostureTransition()
    {
        Controller.height = Mathf.Lerp(Controller.height, TargetHeight, Time.deltaTime * crouchTransitionSpeed);
        float centerOffsetY = (Controller.height - StandingHeight) / 2f;
        Controller.center = defaultCenter + new Vector3(0, centerOffsetY, 0);

        if (CameraTransform != null)
        {
            float headDropAmount = StandingHeight - Controller.height;
            Vector3 camPos = CameraTransform.localPosition;
            camPos.y = defaultCameraPosition.y - headDropAmount;
            camPos.z = defaultCameraPosition.z + (crouchForwardOffset * (1 - (Controller.height - CrouchHeight) / (StandingHeight - CrouchHeight)));

            CameraTransform.localPosition = camPos;
        }
    }

    /// <summary>
    /// 머리 위에 장애물이 없어서 일어설 수 있는지 확인
    /// </summary>
    public bool CanStandUp()
    {
        float radius = Controller.radius;
        Vector3 point1 = transform.position + Vector3.up * radius;
        Vector3 point2 = transform.position + Vector3.up * (StandingHeight - radius);

        return !Physics.CheckCapsule(point1, point2, radius, obstacleLayer, QueryTriggerInteraction.Ignore);
    }

    public void ConfigureForNetwork(bool isLocalPlayer)
    {
        IsLocalPlayer = isLocalPlayer;
    }
}