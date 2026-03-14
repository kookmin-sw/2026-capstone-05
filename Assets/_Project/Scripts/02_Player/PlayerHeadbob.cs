using UnityEngine;

public class PlayerHeadbob : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerController player;

    [Header("Headbob Settings")]
    public bool enableHeadbob = true;
    public float walkBobSpeed = 12f;
    public float walkBobAmount = 0.05f;
    public float sprintBobSpeed = 18f;
    public float sprintBobAmount = 0.1f;
    public float crouchBobSpeed = 8f;
    public float crouchBobAmount = 0.02f;

    private float defaultYPos;
    private float headbobTimer;

    private void Start()
    {
        defaultYPos = transform.localPosition.y;
    }

    private void Update()
    {
        if (!enableHeadbob || player == null || !player.canLook)
        {
            return;
        }

        HandleHeadbob();
    }

    private void HandleHeadbob()
    {
        Vector3 horizontalVelocity = new Vector3(player.currentVelocity.x, 0f, player.currentVelocity.z);
        float currentSpeed = horizontalVelocity.magnitude;

        if (player.IsGrounded && currentSpeed > 0.1f)
        {
            float bobSpeed = walkBobSpeed;
            float bobAmount = walkBobAmount;

            if (currentSpeed > player.walkSpeed + 0.5f)
            {
                bobSpeed = sprintBobSpeed;
                bobAmount = sprintBobAmount;
            }
            else if (currentSpeed < player.crouchSpeed + 0.5f)
            {
                bobSpeed = crouchBobSpeed;
                bobAmount = crouchBobAmount;
            }

            headbobTimer += Time.deltaTime * bobSpeed;

            float newY = defaultYPos + (Mathf.Sin(headbobTimer) * bobAmount);
            float newX = Mathf.Cos(headbobTimer / 2f) * bobAmount * 0.5f;

            transform.localPosition = new Vector3(newX, newY, transform.localPosition.z);
        }
        else
        {
            headbobTimer = 0f;
            Vector3 targetPosition = new Vector3(0f, defaultYPos, 0f);
            transform.localPosition = Vector3.Lerp(transform.localPosition, targetPosition, Time.deltaTime * 5f);
        }
    }
}