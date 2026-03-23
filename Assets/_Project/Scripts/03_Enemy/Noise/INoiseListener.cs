using UnityEngine;

public interface INoiseListener
{
    float NoiseDetectionRadius { get; }
    void OnNoiseDetected(Vector3 noisePosition, float noiseRadius);
}
