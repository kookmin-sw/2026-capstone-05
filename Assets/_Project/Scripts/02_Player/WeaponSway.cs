using UnityEngine;

public class WeaponSway : MonoBehaviour
{
    [Header("Sway Settings")]
    [SerializeField] private float smoothStep = 8f;
    [SerializeField] private float swayMultiplier = 2f;
    [SerializeField] private float maxSwayAmount = 5f;

    private PlayerInputHandler inputHandler;
    private Quaternion startRotation;

    private void Start()
    {
        inputHandler = GetComponentInParent<PlayerInputHandler>();
        startRotation = transform.localRotation;
    }

    private void Update()
    {
        if (inputHandler == null)
        {
            return;
        }

        Vector2 lookInput = inputHandler.LookInput;

        float mouseX = Mathf.Clamp(lookInput.x * swayMultiplier, -maxSwayAmount, maxSwayAmount);
        float mouseY = Mathf.Clamp(lookInput.y * swayMultiplier, -maxSwayAmount, maxSwayAmount);

        Quaternion targetRotation = Quaternion.Euler(-mouseY, mouseX, 0f) * startRotation;
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, smoothStep * Time.deltaTime);
    }
}