using UnityEngine;

public class NoiseTester : MonoBehaviour
{
    [Header("Noise Settings")]
    [SerializeField] private NoiseData.NoiseType noiseType = NoiseData.NoiseType.Sprint;

    [SerializeField] private KeyCode triggerKey = KeyCode.Tab;

    private void Update()
    {
        if (Input.GetKeyDown(triggerKey))
        {
            GenerateTestNoise();
        }
    }

    private void GenerateTestNoise()
    {
        if (NoiseManager.Instance == null)
        {
            return;
        }

        NoiseManager.Instance.GenerateNoise(transform.position, noiseType);
    }
}
