using UnityEngine;

public class PlayerNoiseEmitter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerController player;

    [Header("Stride Settings")]
    public float crouchStride = 1.2f;
    public float walkStride = 1.6f;
    public float sprintStride = 2.2f;

    private float accumulatedDistance = 0f;

    private void Update()
    {
        if (player == null || !player.IsGrounded)
        {
            accumulatedDistance = 0f;
            return;
        }

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
}