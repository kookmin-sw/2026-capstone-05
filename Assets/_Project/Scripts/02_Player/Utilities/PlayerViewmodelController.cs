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

    private Vector3 defaultLocalPos;
    private Quaternion defaultLocalRot;

    private Vector3 currentSwayPos, currentBobPos;
    private Quaternion currentSwayRot;

    [Header("Recoil Settings")]
    public float recoilSnappiness = 10f; // 쏠 때 훅! 들어오는 속도
    public float recoilReturnSpeed = 5f; // 원위치로 스르륵 돌아가는 속도

    private Vector3 currentRecoilPos;
    private Vector3 targetRecoilPos;
    private Quaternion currentRecoilRot;
    private Quaternion targetRecoilRot;

    private void Awake()
    {
        if (viewmodelTransform == null)
        {
            viewmodelTransform = transform;
        }

        defaultLocalPos = viewmodelTransform.localPosition;
        defaultLocalRot = viewmodelTransform.localRotation;

        currentBobSpeed = walkBobSpeed;
        currentBobAmount = walkBobAmount;
    }

    public void ApplyRecoil(Vector3 kickbackPos, Vector3 kickbackRot)
    {
        targetRecoilPos += kickbackPos;
        targetRecoilRot *= Quaternion.Euler(kickbackRot);
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
        // 3. Recoil
        // ==========================================
        targetRecoilPos = Vector3.Lerp(targetRecoilPos, Vector3.zero, Time.deltaTime * recoilReturnSpeed);
        targetRecoilRot = Quaternion.Slerp(targetRecoilRot, Quaternion.identity, Time.deltaTime * recoilReturnSpeed);

        currentRecoilPos = Vector3.Lerp(currentRecoilPos, targetRecoilPos, Time.deltaTime * recoilSnappiness);
        currentRecoilRot = Quaternion.Slerp(currentRecoilRot, targetRecoilRot, Time.deltaTime * recoilSnappiness);

        // ==========================================
        // 최종 적용
        // ==========================================
        viewmodelTransform.localPosition = defaultLocalPos + currentSwayPos + currentBobPos + currentRecoilPos;
        viewmodelTransform.localRotation = defaultLocalRot * currentSwayRot * currentRecoilRot;
    }
}