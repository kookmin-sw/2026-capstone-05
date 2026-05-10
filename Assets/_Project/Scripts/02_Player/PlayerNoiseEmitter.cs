using FMOD.Studio;
using FMODUnity;
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

    [Header("Exploit Settings")]
    public float distanceDecayRate = 0.5f;

    [Header("Footstep Settings")]
    [SerializeField] private EventReference footstepEvent;
    [SerializeField] private EventReference landingEvent;
    [SerializeField] private LayerMask groundLayer;

    [Header("Vital Signal Settings")]
    public float satietyNoiseThreshold = 30f;
    public float coldnessNoiseThreshold = 70f;
    public Vector2 noiseIntervalRange = new Vector2(10f, 25f);

    [SerializeField] private EventReference hungerEvent;
    [SerializeField] private EventReference shiverEvent;

    private float accumulatedDistance = 0f;

    private bool wasGrounded = true;
    private float lastFallSpeed = 0f;
    private float currentAirTime = 0f;

    private float nextHungerTime = 0f;
    private float nextShiverTime = 0f;

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

            if (currentAirTime >= minAirTimeForLanding && absFallSpeed >= minFallSpeedForLanding)
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

        HandleConditionNoises();
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
            if (accumulatedDistance > 0f)
            {
                accumulatedDistance = Mathf.Max(0f, accumulatedDistance - (distanceDecayRate * Time.deltaTime));
            }
        }
    }

    private void GenerateLandingNoise(float impactSpeed, float airTime)
    {
        // TODO: Adjust noise type based on impact speed and air time
        NoiseManager.Instance.GenerateNoise(transform.position, NoiseData.NoiseType.Fall);

        PlayLandingSound();
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

        int movementType = (currentNoise == NoiseData.NoiseType.Sprint) ? 2 : (currentNoise == NoiseData.NoiseType.Crouch) ? 0 : 1;
        PlayFootstepSound(movementType);
    }

    public void GenerateJumpNoise()
    {
        NoiseManager.Instance.GenerateNoise(transform.position, NoiseData.NoiseType.Jump);
    }

    private SurfaceType GetFloorSurface()
    {
        SurfaceType currentSurface = SurfaceType.Snow;

        if (Physics.Raycast(player.transform.position + Vector3.up * 0.1f, Vector3.down, out RaycastHit hit, 0.5f, groundLayer))
        {
            SurfaceMaterial surface = hit.collider.GetComponent<SurfaceMaterial>();
            if (surface != null)
            {
                currentSurface = surface.surfaceType;
            }
        }

        return currentSurface;
    }

    private void PlayFootstepSound(int movementType)    // 0 = Crouch, 1 = Walk, 2 = Sprint
    {
        EventInstance footstepInstance = RuntimeManager.CreateInstance(footstepEvent);
        RuntimeManager.AttachInstanceToGameObject(footstepInstance, player.gameObject);

        SurfaceType surface = GetFloorSurface();
        footstepInstance.setParameterByName("Surface", (float)surface);
        footstepInstance.setParameterByName("PlayerMovementType", movementType);

        footstepInstance.start();
        footstepInstance.release();
    }

    private void PlayLandingSound()
    {
        EventInstance landingInstance = RuntimeManager.CreateInstance(landingEvent);
        RuntimeManager.AttachInstanceToGameObject(landingInstance, player.gameObject);

        SurfaceType surface = GetFloorSurface();
        landingInstance.setParameterByName("Surface", (float)surface);

        landingInstance.start();
        landingInstance.release();
    }

    private void HandleConditionNoises()
    {
        if (player.Condition.satiety.currentValue <= satietyNoiseThreshold)
        {
            nextHungerTime -= Time.deltaTime;
            if (nextHungerTime <= 0f)
            {
                GenerateConditionNoise(hungerEvent, NoiseData.NoiseType.Hunger);
                nextHungerTime = Random.Range(noiseIntervalRange.x, noiseIntervalRange.y);
            }
        }
        else
        {
            nextHungerTime = Random.Range(noiseIntervalRange.x, noiseIntervalRange.y);
        }

        if (player.Condition.coldness.currentValue >= coldnessNoiseThreshold)
        {
            nextShiverTime -= Time.deltaTime;
            if (nextShiverTime <= 0f)
            {
                GenerateConditionNoise(shiverEvent, NoiseData.NoiseType.Cough);
                nextShiverTime = Random.Range(noiseIntervalRange.x, noiseIntervalRange.y);
            }
        }
        else
        {
            nextShiverTime = Random.Range(noiseIntervalRange.x, noiseIntervalRange.y);
        }
    }

    private void GenerateConditionNoise(EventReference fmodEvent, NoiseData.NoiseType noiseType)
    {
        NoiseManager.Instance.GenerateNoise(transform.position, noiseType);

        EventInstance instance = RuntimeManager.CreateInstance(fmodEvent);
        RuntimeManager.AttachInstanceToGameObject(instance, player.gameObject);
        instance.start();
        instance.release();
    }
}