using UnityEngine;

public class NoiseTester : MonoBehaviour
{
    [Header("Noise Settings")]
    [SerializeField] private NoiseData.NoiseType noiseType = NoiseData.NoiseType.Jump;

    [SerializeField] private KeyCode triggerKey = KeyCode.Space;

    private void Update()
    {
        if (Input.GetKeyDown(triggerKey))
        {
            GenerateNoise();
        }
    }

    private void GenerateNoise()
    {
        if (NoiseManager.Instance == null)
        {
            return;
        }

        NoiseManager.Instance.GenerateNoise(transform.position, noiseType);
        Debug.Log($"[NoiseTester] 소음 발생: {noiseType} @ {transform.position}");
    }
}
