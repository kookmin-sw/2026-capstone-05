using UnityEngine;

public class PlayerViewmodelController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerController player;
    [SerializeField] private PlayerInputHandler inputHandler;
    [SerializeField] private Transform viewmodelTransform;

    [Header("Sway Settings")]
    public float swayMultiplier = 1f;
    public float maxSwayAmount = 3f;
    public float swaySmoothSpeed = 15f;
    public float verticalSwayRatio = 0.5f;

    [Header("Hand Bobbing Settings")]
    public float walkBobSpeed = 14f;
    public Vector3 walkBobAmount = new Vector3(0.06f, 0.08f, 0f);

    public float sprintBobSpeed = 18f;
    public Vector3 sprintBobAmount = new Vector3(0.1f, 0.12f, 0f);

    public float crouchBobSpeed = 10f;
    public Vector3 crouchBobAmount = new Vector3(0.04f, 0.05f, 0f);

    public float bobTransitionSpeed = 5f;

    private float bobTimer;
    private float currentBobSpeed;
    private Vector3 currentBobAmount;

    [Header("Idle Breathing Settings")]
    public float breathingSpeed = 2f;
    public float breathingAmount = 0.015f;
    private float breathingTimer;

    [Header("Crouch Offset")]
    public Vector3 crouchPosOffset = new Vector3(0f, -0.08f, -0.05f);
    public Vector3 crouchRotOffset = new Vector3(0f, 0f, 5f);
    public float crouchSmoothSpeed = 10f;

    private Vector3 defaultLocalPos;
    private Quaternion defaultLocalRot;

    private Vector3 currentSwayPos, currentBobPos, currentCrouchPos;
    private Quaternion currentSwayRot, currentCrouchRot;

    private void Awake()
    {
        if (viewmodelTransform == null) viewmodelTransform = transform;
        defaultLocalPos = viewmodelTransform.localPosition;
        defaultLocalRot = viewmodelTransform.localRotation;

        // 초기화
        currentBobSpeed = walkBobSpeed;
        currentBobAmount = walkBobAmount;
    }

    private void LateUpdate()
    {
        if (inputHandler == null || player == null) return;

        // ==========================================
        // 1. Sway
        // ==========================================
        Vector2 lookInput = inputHandler.LookInput;
        float mouseX = Mathf.Clamp(lookInput.x * swayMultiplier, -maxSwayAmount, maxSwayAmount);
        float mouseY = Mathf.Clamp(lookInput.y * swayMultiplier * verticalSwayRatio, -maxSwayAmount, maxSwayAmount);

        Quaternion targetSwayRot = Quaternion.Euler(-mouseY, mouseX, 0f);
        Vector3 targetSwayPos = new Vector3(-mouseX * 0.01f, -mouseY * 0.01f, 0f);

        currentSwayRot = Quaternion.Slerp(currentSwayRot, targetSwayRot, swaySmoothSpeed * Time.deltaTime);
        currentSwayPos = Vector3.Lerp(currentSwayPos, targetSwayPos, swaySmoothSpeed * Time.deltaTime);

        // ==========================================
        // 2. Bobbing & Breathing (카메라 동기화 적용)
        // ==========================================
        Vector3 horizontalVelocity = new Vector3(player.Controller.velocity.x, 0f, player.Controller.velocity.z);
        float currentSpeed = horizontalVelocity.magnitude;
        Vector3 targetBobPos = Vector3.zero;

        if (currentSpeed > 0.1f && player.IsGrounded)
        {
            // ⭐️ 목표 속도와 크기 결정 (PlayerCameraHandler 로직과 동일)
            float targetBobSpeed = walkBobSpeed;
            Vector3 targetBobAmount = walkBobAmount;

            if (currentSpeed > player.walkSpeed + 0.5f)
            {
                targetBobSpeed = sprintBobSpeed;
                targetBobAmount = sprintBobAmount;
            }
            else if (currentSpeed < player.crouchSpeed + 0.5f)
            {
                targetBobSpeed = crouchBobSpeed;
                targetBobAmount = crouchBobAmount;
            }

            // ⭐️ 목표값으로 부드럽게 전환 (Lerp)
            currentBobSpeed = Mathf.Lerp(currentBobSpeed, targetBobSpeed, Time.deltaTime * bobTransitionSpeed);
            currentBobAmount = Vector3.Lerp(currentBobAmount, targetBobAmount, Time.deltaTime * bobTransitionSpeed);

            // 걷는 중 흔들림 연산
            bobTimer += Time.deltaTime * currentBobSpeed;
            targetBobPos = new Vector3(
                Mathf.Cos(bobTimer / 2f) * currentBobAmount.x,
                Mathf.Sin(bobTimer) * currentBobAmount.y,
                0f
            );
            breathingTimer = bobTimer;
        }
        else if (player.IsGrounded)
        {
            // 가만히 서 있는 중
            bobTimer = 0f;
            breathingTimer += Time.deltaTime * breathingSpeed;
            targetBobPos = new Vector3(
                0f,
                Mathf.Sin(breathingTimer) * breathingAmount,
                0f
            );
        }

        currentBobPos = Vector3.Lerp(currentBobPos, targetBobPos, swaySmoothSpeed * Time.deltaTime);

        // ==========================================
        // 3. Crouching
        // ==========================================
        bool isCrouching = player.StateMachine.CurrentState is PlayerGroundedState && (player.StateMachine.CurrentState as PlayerGroundedState).CurrentPosture == PlayerGroundedPosture.Crouching;

        Vector3 targetCrouchPos = isCrouching ? crouchPosOffset : Vector3.zero;
        Quaternion targetCrouchRot = isCrouching ? Quaternion.Euler(crouchRotOffset) : Quaternion.identity;

        currentCrouchPos = Vector3.Lerp(currentCrouchPos, targetCrouchPos, crouchSmoothSpeed * Time.deltaTime);
        currentCrouchRot = Quaternion.Slerp(currentCrouchRot, targetCrouchRot, crouchSmoothSpeed * Time.deltaTime);

        // ==========================================
        // 4. 최종 적용
        // ==========================================
        viewmodelTransform.localPosition = defaultLocalPos + currentSwayPos + currentBobPos + currentCrouchPos;
        viewmodelTransform.localRotation = defaultLocalRot * currentSwayRot * currentCrouchRot;
    }
}