using UnityEngine;
using System.Collections.Generic;

public class NoiseManager : MonoBehaviour
{
    // Singleton
    public static NoiseManager Instance;

    // Serialized Fields
    [SerializeField] private NoiseData noiseData;
    
    [Header("Noise Calculation")]
    [SerializeField] private float radiusMultiplier = 0.2f;
    [SerializeField] private float variationRange = 0.05f;
    [SerializeField] private float maxListenerDetectionRadius = 20f;
    
    [Header("Debug Settings")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private float displayTime = 1f;
    
    // Private Fields
    private Dictionary<NoiseData.NoiseType, float> noiseDict = new Dictionary<NoiseData.NoiseType, float>();
    private List<ActiveNoise> activeNoises = new List<ActiveNoise>();

    // Nested Types
    private struct ActiveNoise
    {
        public Vector3 position;
        public float decibel;
        public float radius;
        public float expireTime;
        public NoiseData.NoiseType noiseType;

        public ActiveNoise(Vector3 pos, float decibel, float rad, float duration, NoiseData.NoiseType type)
        {
            position = pos;
            this.decibel = decibel;
            radius = rad;
            expireTime = Time.time + duration;
            noiseType = type;
        }
    }

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
    
    private void Update()
    {
        for (int i = activeNoises.Count - 1; i >= 0; i--)
        {
            if (Time.time >= activeNoises[i].expireTime)
            {
                activeNoises.RemoveAt(i);
            }
        }
    }

    public float GetDecibel(NoiseData.NoiseType type)
    {
        return noiseDict.TryGetValue(type, out float db) ? db : 0f;
    }

    public void GenerateNoise(Vector3 position, NoiseData.NoiseType noiseType)
    {
        float decibel = GetDecibel(noiseType) * Random.Range(1f - variationRange, 1f + variationRange);
        float calculatedRadius = decibel * radiusMultiplier;

        if (calculatedRadius <= 0f) return;

        activeNoises.Add(new ActiveNoise(position, decibel, calculatedRadius, displayTime, noiseType));
        NotifyEnemies(position, calculatedRadius);
    }

    private void NotifyEnemies(Vector3 position, float radius)
    {
        HashSet<INoiseListener> notified = new HashSet<INoiseListener>();

        Collider[] hitColliders = Physics.OverlapSphere(position, radius + maxListenerDetectionRadius);
        foreach (var hit in hitColliders)
        {
            INoiseListener listener = hit.GetComponentInParent<INoiseListener>();
            if (listener == null || !notified.Add(listener)) continue;

            if (listener is not MonoBehaviour listenerMb) continue;

            float distance = Vector3.Distance(listenerMb.transform.position, position);
            if (distance < radius + listener.DetectionRadius)
            {
                listener.OnNoiseDetected(position, radius);
            }
        }
    }

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
}