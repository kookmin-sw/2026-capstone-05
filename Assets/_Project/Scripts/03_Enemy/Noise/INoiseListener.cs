using UnityEngine;

public interface INoiseListener
{
    float DetectionRadius { get; }
    void OnNoiseDetected(Vector3 noisePosition, float noiseRadius);
}
