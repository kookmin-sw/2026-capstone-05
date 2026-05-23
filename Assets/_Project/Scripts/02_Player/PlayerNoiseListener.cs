using UnityEngine;

public class PlayerNoiseListener : MonoBehaviour, INoiseListener
{
    [Header("Noise Detection Settings")]
    [SerializeField, Tooltip("소음이 감지되지 않을 때 초당 감소하는 데시벨 수치")]
    private float decayRate = 30f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLog = false;

    public float CurrentDecibel { get; private set; }

    private void OnEnable()
    {
        NoiseManager.RegisterListener(this);
    }

    private void OnDisable()
    {
        NoiseManager.UnregisterListener(this);
    }

    private void Update()
    {
        if (CurrentDecibel > 0f)
        {
            CurrentDecibel = Mathf.MoveTowards(CurrentDecibel, 0f, decayRate * Time.deltaTime);
        }
    }

    public void OnNoiseDetected(Vector3 noisePosition, float noiseIntensity, NoiseData.NoiseType noiseType, bool isObstructed)
    {
        float baseDecibel = NoiseManager.Instance.GetDecibel(noiseType);
        float perceivedDecibel = baseDecibel * noiseIntensity;

        if (perceivedDecibel > CurrentDecibel)
        {
            CurrentDecibel = perceivedDecibel;

            if (showDebugLog)
            {
                Debug.Log($"[PlayerNoise] 새로운 소음 감지: {noiseType} / 현재 데시벨: {CurrentDecibel:F1}dB (장애물: {isObstructed})");
            }
        }
    }
}
