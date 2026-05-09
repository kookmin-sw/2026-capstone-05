using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.Serialization;

[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
public class PlayerRelativeParticleFollower : MonoBehaviour, IPlayerNetworkConfigurable
{
    [Header("Follow Target")]
    [SerializeField] private Transform followTarget;
    [SerializeField, FormerlySerializedAs("autoFindPlayerTarget")] private bool autoResolveFollowTarget = true;
    [SerializeField] private bool preferCinemachineFollowTarget = true;
    [SerializeField] private bool fallbackToCameraTransform = true;

    [Header("Position")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2.5f, 0f);
    [SerializeField] private bool useTargetYawForOffset = false;
    [SerializeField] private bool smoothFollow = false;
    [SerializeField] private float followSpeed = 20f;

    [Header("Rotation")]
    [SerializeField] private bool keepInitialWorldRotation = true;
    [SerializeField] private Vector3 fixedWorldEulerAngles = Vector3.zero;

    [Header("Particle")]
    [SerializeField] private bool localPlayerOnly = true;
    [SerializeField] private bool forceWorldSimulationSpace = true;
    [SerializeField] private ParticleSystem[] particleSystems;

    [Header("Indoor Occlusion")]
    [SerializeField] private bool useCeilingOcclusion = false;
    [SerializeField] private bool filterCeilingByLayer = false;
    [SerializeField] private LayerMask ceilingOcclusionMask = ~0;
    [SerializeField] private Vector3 ceilingCheckOffset = new Vector3(0f, 0.25f, 0f);
    [SerializeField] private float ceilingCheckDistance = 30f;
    [SerializeField] private float ceilingCheckInterval = 0.25f;
    [SerializeField] private bool clearParticlesWhenOccluded = true;

    private Quaternion fixedWorldRotation;
    private bool shouldRun = true;
    private bool hasManualFollowTarget;
    private bool isOccluded;
    private bool particlesPlaying;
    private bool playbackStateInitialized;
    private float nextCeilingCheckTime;
    private CinemachineCamera parentCinemachineCamera;
    private readonly RaycastHit[] ceilingHits = new RaycastHit[8];

    private void Awake()
    {
        hasManualFollowTarget = followTarget != null;
        parentCinemachineCamera = GetComponentInParent<CinemachineCamera>();

        ResolveFollowTarget();
        CacheParticleSystems();

        fixedWorldRotation = keepInitialWorldRotation
            ? transform.rotation
            : Quaternion.Euler(fixedWorldEulerAngles);

        if (forceWorldSimulationSpace)
        {
            ApplyWorldSimulationSpace();
        }

        SnapToTarget();
        RefreshParticlePlayback();
    }

    private void OnValidate()
    {
        followSpeed = Mathf.Max(0f, followSpeed);
        ceilingCheckDistance = Mathf.Max(0f, ceilingCheckDistance);
        ceilingCheckInterval = Mathf.Max(0.02f, ceilingCheckInterval);
    }

    private void LateUpdate()
    {
        if (!shouldRun)
        {
            return;
        }

        ResolveFollowTarget();

        if (followTarget == null)
        {
            return;
        }

        Vector3 targetPosition = GetTargetPosition();

        if (smoothFollow)
        {
            float t = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, targetPosition, t);
        }
        else
        {
            transform.position = targetPosition;
        }

        transform.rotation = fixedWorldRotation;

        UpdateOcclusion();
    }

    public void ConfigureForNetwork(bool isLocalPlayer)
    {
        shouldRun = !localPlayerOnly || isLocalPlayer;
        enabled = shouldRun;
        RefreshParticlePlayback();
    }

    public void SetFollowTarget(Transform target)
    {
        followTarget = target;
        hasManualFollowTarget = target != null;
        SnapToTarget();
    }

    public void SetIndoorBlocked(bool blocked)
    {
        isOccluded = blocked;
        RefreshParticlePlayback();
    }

    private void ResolveFollowTarget()
    {
        if (hasManualFollowTarget || !autoResolveFollowTarget)
        {
            return;
        }

        Transform resolvedTarget = ResolveCinemachineTarget();
        if (resolvedTarget != null)
        {
            followTarget = resolvedTarget;
            return;
        }

        PlayerController player = GetComponentInParent<PlayerController>();
        if (player != null)
        {
            followTarget = player.transform;
            return;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            followTarget = mainCamera.transform;
        }
    }

    private Transform ResolveCinemachineTarget()
    {
        if (!preferCinemachineFollowTarget)
        {
            return null;
        }

        if (parentCinemachineCamera == null)
        {
            parentCinemachineCamera = GetComponentInParent<CinemachineCamera>();
        }

        if (parentCinemachineCamera == null)
        {
            Camera parentCamera = GetComponentInParent<Camera>();
            return parentCamera != null && fallbackToCameraTransform ? parentCamera.transform : null;
        }

        if (parentCinemachineCamera.Follow != null)
        {
            return parentCinemachineCamera.Follow;
        }

        if (parentCinemachineCamera.LookAt != null)
        {
            return parentCinemachineCamera.LookAt;
        }

        return fallbackToCameraTransform ? parentCinemachineCamera.transform : null;
    }

    private void SnapToTarget()
    {
        if (followTarget == null)
        {
            return;
        }

        transform.position = GetTargetPosition();
        transform.rotation = fixedWorldRotation;
    }

    private Vector3 GetTargetPosition()
    {
        Vector3 offset = worldOffset;

        if (useTargetYawForOffset)
        {
            Quaternion yawRotation = Quaternion.Euler(0f, followTarget.eulerAngles.y, 0f);
            offset = yawRotation * worldOffset;
        }

        return followTarget.position + offset;
    }

    private void CacheParticleSystems()
    {
        if (particleSystems != null && particleSystems.Length > 0)
        {
            return;
        }

        particleSystems = GetComponentsInChildren<ParticleSystem>(true);
    }

    private void ApplyWorldSimulationSpace()
    {
        foreach (ParticleSystem particleSystem in particleSystems)
        {
            ParticleSystem.MainModule main = particleSystem.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
        }
    }

    private void UpdateOcclusion()
    {
        if (!useCeilingOcclusion || Time.time < nextCeilingCheckTime)
        {
            return;
        }

        nextCeilingCheckTime = Time.time + ceilingCheckInterval;

        Vector3 rayOrigin = followTarget != null
            ? followTarget.position + ceilingCheckOffset
            : transform.position + ceilingCheckOffset;

        int queryMask = filterCeilingByLayer
            ? ceilingOcclusionMask
            : Physics.DefaultRaycastLayers;

        int hitCount = Physics.RaycastNonAlloc(
            rayOrigin,
            Vector3.up,
            ceilingHits,
            ceilingCheckDistance,
            queryMask,
            QueryTriggerInteraction.Ignore);

        bool blocked = HasValidCeilingHit(hitCount);

        if (isOccluded == blocked)
        {
            return;
        }

        isOccluded = blocked;
        RefreshParticlePlayback();
    }

    private bool HasValidCeilingHit(int hitCount)
    {
        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = ceilingHits[i].collider;

            if (hitCollider == null)
            {
                continue;
            }

            Transform hitTransform = hitCollider.transform;

            if (hitTransform == transform || hitTransform.IsChildOf(transform))
            {
                continue;
            }

            if (followTarget != null && hitTransform.IsChildOf(followTarget))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private void RefreshParticlePlayback()
    {
        SetParticlePlayback(shouldRun && !isOccluded);
    }

    private void SetParticlePlayback(bool play)
    {
        if (playbackStateInitialized && particlesPlaying == play)
        {
            return;
        }

        playbackStateInitialized = true;
        particlesPlaying = play;

        foreach (ParticleSystem particleSystem in particleSystems)
        {
            if (play)
            {
                particleSystem.Play(true);
            }
            else
            {
                ParticleSystemStopBehavior stopBehavior = clearParticlesWhenOccluded
                    ? ParticleSystemStopBehavior.StopEmittingAndClear
                    : ParticleSystemStopBehavior.StopEmitting;

                particleSystem.Stop(true, stopBehavior);
            }
        }
    }
}
