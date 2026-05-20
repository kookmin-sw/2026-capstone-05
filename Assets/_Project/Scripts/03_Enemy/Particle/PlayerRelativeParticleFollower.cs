using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.Serialization;

[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
public class PlayerRelativeParticleFollower : MonoBehaviour, IPlayerNetworkConfigurable
{
    [Header("Follow")]
    [SerializeField] private Transform followTarget;
    [SerializeField] private bool autoFindPlayerTarget = true;
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2.5f, 0f);
    [SerializeField] private bool keepInitialWorldRotation = true;

    [Header("Particle")]
    [SerializeField] private bool localPlayerOnly = true;
    [SerializeField] private bool syncWithWeatherState = true;
    [SerializeField] private NetworkWeatherState activeWeatherState = NetworkWeatherState.Snow;
    [SerializeField] private float fadeSeconds = 0.75f;
    [SerializeField, HideInInspector] private bool forceWorldSimulationSpace = true;
    [SerializeField, HideInInspector] private ParticleSystem[] particleSystems;

    [Header("Snow Volume")]
    [SerializeField] private bool useSnowOcclusionVolumes = true;

    private Quaternion fixedWorldRotation;
    private bool hasManualFollowTarget;
    private bool shouldRun = true;
    private bool weatherHidden;
    private bool particlesPlaying;
    private float emissionScale = 1f;
    private float targetEmissionScale = 1f;
    private float[] emissionDefaults;
    private CinemachineCamera parentCinemachineCamera;

    private bool ShouldPlay => shouldRun && !weatherHidden;

    private void Awake()
    {
        hasManualFollowTarget = followTarget != null;
        parentCinemachineCamera = GetComponentInParent<CinemachineCamera>();

        CacheParticleSystems();
        fixedWorldRotation = keepInitialWorldRotation ? transform.rotation : Quaternion.identity;

        if (forceWorldSimulationSpace)
        {
            ApplyWorldSimulationSpace();
        }

        ResolveFollowTarget();
        SnapToTarget();
        ApplyWeatherState(BackendRoundManager.Instance != null
            ? BackendRoundManager.Instance.CurrentWeatherState
            : activeWeatherState);
        RefreshPlayback(immediate: true);
    }

    private void OnEnable()
    {
        BackendRoundManager.WeatherStateChanged += ApplyWeatherState;
        SnowOcclusionVolume.SnowMaskVolumesChanged += RefreshSnowParticleMasks;
        RefreshSnowParticleMasks();
    }

    private void OnDisable()
    {
        BackendRoundManager.WeatherStateChanged -= ApplyWeatherState;
        SnowOcclusionVolume.SnowMaskVolumesChanged -= RefreshSnowParticleMasks;
    }

    private void OnValidate()
    {
        fadeSeconds = Mathf.Max(0f, fadeSeconds);
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
            UpdateEmissionFade();
            return;
        }

        transform.position = followTarget.position + worldOffset;
        transform.rotation = fixedWorldRotation;

        UpdateEmissionFade();
    }

    public void ConfigureForNetwork(bool isLocalPlayer)
    {
        shouldRun = !localPlayerOnly || isLocalPlayer;

        if (!shouldRun)
        {
            ClearSnowParticleMasks();
            StopParticles(ParticleSystemStopBehavior.StopEmittingAndClear);
            enabled = false;
            return;
        }

        enabled = true;
        ResolveFollowTarget();
        SnapToTarget();
        ApplyWeatherState(BackendRoundManager.Instance != null
            ? BackendRoundManager.Instance.CurrentWeatherState
            : activeWeatherState);
        RefreshSnowParticleMasks();
        RefreshPlayback(immediate: true);
    }

    public void SetFollowTarget(Transform target)
    {
        followTarget = target;
        hasManualFollowTarget = target != null;
        SnapToTarget();
    }

    public void ApplyWeatherState(NetworkWeatherState weatherState)
    {
        weatherHidden = syncWithWeatherState && activeWeatherState != weatherState;
        RefreshPlayback(immediate: weatherHidden);
    }

    private void ResolveFollowTarget()
    {
        if (hasManualFollowTarget || !autoFindPlayerTarget)
        {
            return;
        }

        if (parentCinemachineCamera == null)
        {
            parentCinemachineCamera = GetComponentInParent<CinemachineCamera>();
        }

        if (parentCinemachineCamera != null)
        {
            if (parentCinemachineCamera.Follow != null)
            {
                followTarget = parentCinemachineCamera.Follow;
                return;
            }

            if (parentCinemachineCamera.LookAt != null)
            {
                followTarget = parentCinemachineCamera.LookAt;
                return;
            }

            followTarget = parentCinemachineCamera.transform;
            return;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            followTarget = mainCamera.transform;
        }
    }

    private void SnapToTarget()
    {
        if (followTarget == null)
        {
            return;
        }

        transform.position = followTarget.position + worldOffset;
        transform.rotation = fixedWorldRotation;
    }

    private void RefreshPlayback(bool immediate)
    {
        CacheParticleSystems();

        if (ShouldPlay)
        {
            PlayParticles();
            targetEmissionScale = 1f;
            if (immediate || fadeSeconds <= 0f)
            {
                emissionScale = 1f;
                ApplyEmissionScale(emissionScale);
            }
            return;
        }

        targetEmissionScale = 0f;

        if (immediate || weatherHidden || !shouldRun || fadeSeconds <= 0f)
        {
            emissionScale = 0f;
            ApplyEmissionScale(emissionScale);
            StopParticles(ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void UpdateEmissionFade()
    {
        if (Mathf.Approximately(emissionScale, targetEmissionScale))
        {
            return;
        }

        float speed = fadeSeconds <= 0f ? float.MaxValue : 1f / fadeSeconds;
        emissionScale = Mathf.MoveTowards(emissionScale, targetEmissionScale, speed * Time.deltaTime);
        ApplyEmissionScale(emissionScale);

        if (emissionScale <= 0f && !ShouldPlay)
        {
            StopParticles(ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void CacheParticleSystems()
    {
        if (particleSystems == null || particleSystems.Length == 0)
        {
            particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        }

        if (emissionDefaults != null && emissionDefaults.Length == particleSystems.Length)
        {
            return;
        }

        emissionDefaults = new float[particleSystems.Length];
        for (int i = 0; i < particleSystems.Length; i++)
        {
            emissionDefaults[i] = particleSystems[i] != null
                ? particleSystems[i].emission.rateOverTimeMultiplier
                : 0f;
        }

    }

    private void RefreshSnowParticleMasks()
    {
        CacheParticleSystems();

        if (!useSnowOcclusionVolumes || SnowOcclusionVolume.SnowMaskColliders.Count == 0)
        {
            ClearSnowParticleMasks();
            return;
        }

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem == null)
            {
                continue;
            }

            ParticleSystem.TriggerModule trigger = particleSystem.trigger;
            trigger.enabled = true;
            trigger.inside = ParticleSystemOverlapAction.Kill;
            trigger.enter = ParticleSystemOverlapAction.Kill;
            trigger.exit = ParticleSystemOverlapAction.Ignore;
            trigger.outside = ParticleSystemOverlapAction.Ignore;
            RemoveAllTriggerColliders(trigger);

            for (int colliderIndex = 0; colliderIndex < SnowOcclusionVolume.SnowMaskColliders.Count; colliderIndex++)
            {
                trigger.AddCollider(SnowOcclusionVolume.SnowMaskColliders[colliderIndex]);
            }
        }
    }

    private void ClearSnowParticleMasks()
    {
        if (particleSystems == null)
        {
            return;
        }

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem == null)
            {
                continue;
            }

            ParticleSystem.TriggerModule trigger = particleSystem.trigger;
            RemoveAllTriggerColliders(trigger);
            trigger.enabled = false;
        }
    }

    private static void RemoveAllTriggerColliders(ParticleSystem.TriggerModule trigger)
    {
        for (int i = trigger.colliderCount - 1; i >= 0; i--)
        {
            trigger.RemoveCollider(i);
        }
    }

    private void ApplyWorldSimulationSpace()
    {
        foreach (ParticleSystem particleSystem in particleSystems)
        {
            if (particleSystem == null)
            {
                continue;
            }

            ParticleSystem.MainModule main = particleSystem.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
        }
    }

    private void ApplyEmissionScale(float scale)
    {
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem == null)
            {
                continue;
            }

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverTimeMultiplier = emissionDefaults[i] * scale;
        }
    }

    private void PlayParticles()
    {
        if (particlesPlaying)
        {
            return;
        }

        particlesPlaying = true;
        foreach (ParticleSystem particleSystem in particleSystems)
        {
            if (particleSystem != null)
            {
                particleSystem.Play(true);
            }
        }
    }

    private void StopParticles(ParticleSystemStopBehavior stopBehavior)
    {
        if (!particlesPlaying && stopBehavior != ParticleSystemStopBehavior.StopEmittingAndClear)
        {
            return;
        }

        particlesPlaying = false;
        foreach (ParticleSystem particleSystem in particleSystems)
        {
            if (particleSystem != null)
            {
                particleSystem.Stop(true, stopBehavior);
            }
        }
    }
}
