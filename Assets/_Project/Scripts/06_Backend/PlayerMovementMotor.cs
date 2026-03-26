using UnityEngine;

public class PlayerMovementMotor
{
    private Vector3 _velocity;

    public void Simulate(
        CharacterController controller,
        Transform playerTransform,
        Vector2 moveInput,
        bool isSprinting,
        bool isCrouching,
        bool jumpPressed,
        float playerSpeed,
        float sprintSpeed,
        float crouchSpeed,
        float jumpForce,
        float gravity,
        float airControlMultiplier,
        float airControlLerpSpeed,
        float deltaTime)
    {
        if (controller.isGrounded && _velocity.y < 0f)
            _velocity.y = -2f;

        Vector3 moveDirection = (playerTransform.right * moveInput.x + playerTransform.forward * moveInput.y).normalized;
        float moveSpeed = isCrouching ? crouchSpeed : (isSprinting ? sprintSpeed : playerSpeed);
        Vector3 desiredHorizontalVelocity = moveDirection * moveSpeed;

        if (controller.isGrounded)
        {
            _velocity.x = desiredHorizontalVelocity.x;
            _velocity.z = desiredHorizontalVelocity.z;
        }
        else
        {
            float airControlSpeed = airControlLerpSpeed * deltaTime;
            float targetSpeed = playerSpeed * airControlMultiplier;
            _velocity.x = Mathf.Lerp(_velocity.x, moveDirection.x * targetSpeed, airControlSpeed);
            _velocity.z = Mathf.Lerp(_velocity.z, moveDirection.z * targetSpeed, airControlSpeed);
        }

        _velocity.y += gravity * deltaTime;

        if (!isCrouching && jumpPressed && controller.isGrounded)
            _velocity.y = jumpForce;

        controller.Move(_velocity * deltaTime);
    }
}
