using UnityEngine;

public class PlayerNoiseEmitter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerController player;

    [Header("Stride Settings")]
    public float crouchStride = 1.2f;
    public float walkStride = 1.6f;
    public float sprintStride = 2.2f;

    [Header("Anti-Jitter")]
    public float minAirTimeForLanding = 0.2f;
    public float minFallSpeedForLanding = 2.0f;

    private float accumulatedDistance = 0f;

    private bool wasGrounded = true;
    private float lastFallSpeed = 0f;
    private float currentAirTime = 0f;

    private void Update()
    {
        if (player == null) return;

        bool isGrounded = player.StateMachine.CurrentState == player.GroundedState;

        if (!isGrounded)
        {
            currentAirTime += Time.deltaTime;
            lastFallSpeed = player.Controller.velocity.y;
            accumulatedDistance = 0f;
        }

        if (!wasGrounded && isGrounded)
        {
            float absFallSpeed = Mathf.Abs(lastFallSpeed);

            if (currentAirTime >= minAirTimeForLanding || absFallSpeed >= minFallSpeedForLanding)
            {
                GenerateLandingNoise(absFallSpeed, currentAirTime);
            }

            currentAirTime = 0f;
        }

        if (isGrounded)
        {
            CalculateMovementNoise();
        }

        wasGrounded = isGrounded;
    }

    private void CalculateMovementNoise()
    {
        Vector3 horizontalVelocity = new Vector3(player.Controller.velocity.x, 0f, player.Controller.velocity.z);
        float currentSpeed = horizontalVelocity.magnitude;

        if (currentSpeed > 0.1f)
        {
            accumulatedDistance += currentSpeed * Time.deltaTime;
            float currentStride = GetCurrentStride();

            if (accumulatedDistance >= currentStride)
            {
                GenerateFootstepNoise();
                accumulatedDistance -= currentStride;
            }
        }
        else
        {
            accumulatedDistance = 0f;
        }
    }

    private void GenerateLandingNoise(float impactSpeed, float airTime)
    {
        // TODO: Adjust noise type based on impact speed and air time for more realism
        NoiseManager.Instance.GenerateNoise(transform.position, NoiseData.NoiseType.Fall);
    }

    private float GetCurrentStride()
    {
        if (player.StateMachine.CurrentState == player.GroundedState)
        {
            if (player.GroundedState.CurrentLocomotion == PlayerGroundedLocomotion.Sprinting)
            {
                return sprintStride;
            }

            if (player.GroundedState.CurrentPosture == PlayerGroundedPosture.Crouching)
            {
                return crouchStride;
            }

            return walkStride;
        }

        return walkStride;
    }

    private void GenerateFootstepNoise()
    {
        NoiseData.NoiseType currentNoise = NoiseData.NoiseType.Walk;

        if (player.StateMachine.CurrentState == player.GroundedState)
        {
            if (player.GroundedState.CurrentLocomotion == PlayerGroundedLocomotion.Sprinting)
            {
                currentNoise = NoiseData.NoiseType.Sprint;
            }
            else if (player.GroundedState.CurrentPosture == PlayerGroundedPosture.Crouching)
            {
                currentNoise = NoiseData.NoiseType.Crouch;
            }
        }

        NoiseManager.Instance.GenerateNoise(transform.position, currentNoise);
    }

    public void GenerateJumpNoise()
    {
        NoiseManager.Instance.GenerateNoise(transform.position, NoiseData.NoiseType.Jump);
    }
}