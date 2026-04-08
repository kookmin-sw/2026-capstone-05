using UnityEngine;

public class PlayerViewmodelController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private PlayerInputHandler inputHandler;
    [SerializeField] private Transform viewmodelTransform;

    [Header("Weapon Sway")]
    public float swayMultiplier = 2f;
    public float maxSwayAmount = 5f;
    public float swaySmoothSpeed = 8f;

    [Header("Pitch Offset")]
    public Vector3 maxLookDownOffset = new Vector3(0f, 0f, -0.4f);
    public float pitchSmoothSpeed = 10f;

    private Vector3 defaultLocalPos;
    private Quaternion defaultLocalRot;

    private Vector3 currentSwayPos;
    private Quaternion currentSwayRot;

    private Vector3 currentPitchOffset;

    private void Awake()
    {
        defaultLocalPos = viewmodelTransform.localPosition;
        defaultLocalRot = viewmodelTransform.localRotation;
    }

    private void LateUpdate()
    {
        if (cameraTransform == null || inputHandler == null) return;

        // 1. 고개 숙임 보정 계산 (Pitch)
        float pitch = cameraTransform.localEulerAngles.x;
        if (pitch > 180f) pitch -= 360f;

        float lookDownFactor = Mathf.Clamp01(pitch / 80f);
        Vector3 targetPitchOffset = maxLookDownOffset * lookDownFactor;

        currentPitchOffset = Vector3.Lerp(currentPitchOffset, targetPitchOffset, pitchSmoothSpeed * Time.deltaTime);

        // 2. 마우스 스웨이 계산 (Sway)
        Vector2 lookInput = inputHandler.LookInput;
        float mouseX = Mathf.Clamp(lookInput.x * swayMultiplier, -maxSwayAmount, maxSwayAmount);
        float mouseY = Mathf.Clamp(lookInput.y * swayMultiplier * 0.2f, -maxSwayAmount, maxSwayAmount);

        Quaternion targetSwayRot = Quaternion.Euler(-mouseY, mouseX, 0f);
        Vector3 targetSwayPos = new Vector3(-mouseX * 0.01f, -mouseY * 0.01f, 0f);

        currentSwayRot = Quaternion.Slerp(currentSwayRot, targetSwayRot, swaySmoothSpeed * Time.deltaTime);
        currentSwayPos = Vector3.Lerp(currentSwayPos, targetSwayPos, swaySmoothSpeed * Time.deltaTime);

        // 최종 적용
        Vector3 finalPos = defaultLocalPos + currentPitchOffset + currentSwayPos;
        Quaternion finalRot = defaultLocalRot * currentSwayRot;

        viewmodelTransform.localPosition = finalPos;
        viewmodelTransform.localRotation = finalRot;
    }
}