using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class NoiseManager : MonoBehaviour
{
    // Singleton
    public static NoiseManager Instance { get; private set; }

    // Serialized Fields
    [SerializeField] private NoiseData noiseData;
    
    [Header("Noise Calculation")]
    [SerializeField] private float radiusMultiplier = 0.5f;
    [SerializeField] private float variationRange = 0.05f;

    [Header("Obstacle Detection")]
    [SerializeField] private LayerMask soundObstacleMask;
    [SerializeField, Range(0f, 1f)] private float obstructedIntensityMultiplier = 0.35f;
    
#if UNITY_EDITOR
    [Header("Debug Settings")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private float displayTime = 1f;
#endif
    
    // Private Fields
    private readonly Dictionary<NoiseData.NoiseType, float> noiseDict = new Dictionary<NoiseData.NoiseType, float>();
    private static readonly HashSet<INoiseListener> registeredListeners = new HashSet<INoiseListener>();
    private readonly HashSet<INoiseListener> notifiedListeners = new HashSet<INoiseListener>();

    private float maxDecibel;
    public float MaxDecibel => maxDecibel;

    public static void RegisterListener(INoiseListener listener)
    {
        if (listener != null)
        {
            registeredListeners.Add(listener);
        }
    }

    public static void UnregisterListener(INoiseListener listener)
    {
        if (listener != null)
        {
            registeredListeners.Remove(listener);
        }
    }

#if UNITY_EDITOR
    private List<ActiveNoise> activeNoises = new List<ActiveNoise>();

    private struct ActiveNoise
    {
        public Vector3 position;
        public float radius;
        public NoiseData.NoiseType noiseType;
        public float expireTime;

        public ActiveNoise(Vector3 pos, float rad, NoiseData.NoiseType type, float duration)
        {
            position = pos;
            radius = rad;
            noiseType = type;
            expireTime = Time.time + duration;
        }
    }
#endif

    private void Awake()
    {
        if (Instance == null) 
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else 
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (noiseData == null) return;

        foreach (var noise in noiseData.noiseInfo)
        {
            if (!noiseDict.ContainsKey(noise.type))
            {
                noiseDict.Add(noise.type, noise.decibel);
                if (noise.decibel > maxDecibel)
                {
                    maxDecibel = noise.decibel;
                }
            }
        }
    }
    
#if UNITY_EDITOR
    private void Update()
    {
        for (int i = activeNoises.Count - 1; i >= 0; i--)
        {
            if (Time.time >= activeNoises[i].expireTime)
                activeNoises.RemoveAt(i);
        }
    }
#endif

    public float GetDecibel(NoiseData.NoiseType type)
    {
        return noiseDict.TryGetValue(type, out float db) ? db : 0f;
    }

    public void GenerateNoise(Vector3 position, NoiseData.NoiseType noiseType)
    {
        float decibel = GetDecibel(noiseType) * Random.Range(1f - variationRange, 1f + variationRange);
        float calculatedRadius = decibel * radiusMultiplier;

        if (calculatedRadius <= 0f) return;

#if UNITY_EDITOR
        activeNoises.Add(new ActiveNoise(position, calculatedRadius, noiseType, displayTime));
#endif
        NotifyEnemies(position, calculatedRadius, noiseType);
    }

    private void NotifyEnemies(Vector3 position, float radius, NoiseData.NoiseType noiseType)
    {
        notifiedListeners.Clear();

        foreach (INoiseListener listener in registeredListeners)
        {
            if (listener == null || !notifiedListeners.Add(listener)) continue;
            if (!TryGetListenerComponent(listener, out Component listenerComponent)) continue;

            float distance = GetClosestListenerDistance(position, listenerComponent, out Vector3 listenerPosition);
            if (distance > radius)
                continue;

            bool isObstructed = soundObstacleMask.value != 0 &&
                                Physics.Linecast(position, listenerPosition, soundObstacleMask, QueryTriggerInteraction.Ignore);
            float noiseIntensity = Mathf.Clamp01(1f - distance / radius);
            if (isObstructed)
            {
                noiseIntensity *= obstructedIntensityMultiplier;
            }

            if (noiseIntensity <= 0f)
                continue;

            listener.OnNoiseDetected(position, noiseIntensity, noiseType, isObstructed);
        }
    }

    private static bool TryGetListenerComponent(INoiseListener listener, out Component listenerComponent)
    {
        listenerComponent = listener as Component;
        if (listenerComponent == null)
            return false;

        if (!listenerComponent.gameObject.activeInHierarchy)
            return false;

        if (listenerComponent is Behaviour behaviour && !behaviour.isActiveAndEnabled)
            return false;

        return true;
    }

    private static float GetClosestListenerDistance(Vector3 sourcePosition, Component listenerComponent, out Vector3 listenerPosition)
    {
        Collider[] listenerColliders = listenerComponent.GetComponentsInChildren<Collider>();
        float closestSqrDistance = float.PositiveInfinity;
        Vector3 closestPoint = listenerComponent.transform.position;
        Vector3 closestBoundsCenter = listenerComponent.transform.position;

        foreach (Collider listenerCollider in listenerColliders)
        {
            if (listenerCollider == null || !listenerCollider.enabled || !listenerCollider.gameObject.activeInHierarchy)
                continue;

            Vector3 point = listenerCollider.ClosestPoint(sourcePosition);
            float sqrDistance = (point - sourcePosition).sqrMagnitude;
            if (sqrDistance >= closestSqrDistance)
                continue;

            closestSqrDistance = sqrDistance;
            closestPoint = point;
            closestBoundsCenter = listenerCollider.bounds.center;
        }

        if (float.IsPositiveInfinity(closestSqrDistance))
        {
            listenerPosition = listenerComponent.transform.position;
            return Vector3.Distance(sourcePosition, listenerPosition);
        }

        listenerPosition = closestBoundsCenter;
        return Vector3.Distance(sourcePosition, closestPoint);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showGizmos || activeNoises == null) return;

        foreach (var noise in activeNoises)
        {
            float remaining = Mathf.Clamp01((noise.expireTime - Time.time) / displayTime);
            Gizmos.color = new Color(1f, 0.1f, 0.1f, remaining * 0.5f);

            Gizmos.DrawSphere(noise.position, 0.2f);
            Gizmos.DrawWireSphere(noise.position, noise.radius);

            Handles.color = new Color(1f, 0.25f, 0.25f, remaining);
            Handles.Label(
                noise.position + Vector3.up * 0.5f,
                $"{noise.noiseType} Noise\nRadius: {noise.radius:F1}m");
        }
    }
#endif
}
