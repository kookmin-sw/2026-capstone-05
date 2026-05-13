using UnityEngine;

public interface INoiseListener
{
    void OnNoiseDetected(Vector3 noisePosition, float noiseIntensity, NoiseData.NoiseType noiseType, bool isObstructed);
}
