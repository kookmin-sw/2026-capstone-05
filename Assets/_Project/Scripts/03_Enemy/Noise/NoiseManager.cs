using UnityEngine;
using System.Collections.Generic;

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
    
#if UNITY_EDITOR
    [Header("Debug Settings")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private float displayTime = 1f;
#endif
    
    // Private Fields
    private readonly Dictionary<NoiseData.NoiseType, float> noiseDict = new Dictionary<NoiseData.NoiseType, float>();
    private readonly Collider[] overlapBuffer = new Collider[64];
    private readonly HashSet<INoiseListener> notifiedListeners = new HashSet<INoiseListener>();

#if UNITY_EDITOR
    private List<ActiveNoise> activeNoises = new List<ActiveNoise>();

    private struct ActiveNoise
    {
        public Vector3 position;
        public float radius;
        public float expireTime;

        public ActiveNoise(Vector3 pos, float rad, float duration)
        {
            position = pos;
            radius = rad;
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
                noiseDict.Add(noise.type, noise.decibel);
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
        activeNoises.Add(new ActiveNoise(position, calculatedRadius, displayTime));
#endif
        NotifyEnemies(position, calculatedRadius);
    }

    private void NotifyEnemies(Vector3 position, float radius)
    {
        notifiedListeners.Clear();

        int count = Physics.OverlapSphereNonAlloc(position, radius, overlapBuffer);
        for (int i = 0; i < count; i++)
        {
            INoiseListener listener = overlapBuffer[i].GetComponentInParent<INoiseListener>();
            if (listener == null || !notifiedListeners.Add(listener)) continue;

            Vector3 listenerPosition = overlapBuffer[i].bounds.center;
            if (Physics.Linecast(position, listenerPosition, soundObstacleMask))
                continue;

            float distance = Vector3.Distance(overlapBuffer[i].ClosestPoint(position), position);
            float noiseIntensity = 1f - distance / radius;
            listener.OnNoiseDetected(position, noiseIntensity);
        }
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
        }
    }
#endif
}
