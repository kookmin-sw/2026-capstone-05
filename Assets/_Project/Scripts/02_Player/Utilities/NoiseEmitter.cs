using UnityEngine;

public class NoiseEmitter : MonoBehaviour
{
    public void EmitNoise(NoiseData.NoiseType noiseType)
    {
        NoiseManager.Instance.GenerateNoise(transform.position, noiseType);
    }
}
