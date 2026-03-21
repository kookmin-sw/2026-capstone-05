using UnityEngine;
using System.Collections.Generic;

public class NoiseManager : MonoBehaviour
{
    public static NoiseManager Instance;
    
    [SerializeField] private NoiseData noiseData;
    
    [Header("Noise Calculation")]
    [SerializeField] private float radiusMultiplier = 0.2f;
    [SerializeField] private float variationRange = 0.05f;
    
    [Header("Debug Settings")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private float displayTime = 2f;
    
    // 데이터 조회 최적화용 사전
    private Dictionary<NoiseData.NoiseType, float> noiseDict = new Dictionary<NoiseData.NoiseType, float>();

    // 활성 소음 관리용 구조체
    private struct ActiveNoise
    {
        public Vector3 position;
        public float deceibel;
        public float radius;
        public float expireTime;
        public NoiseData.NoiseType noiseType;

        public ActiveNoise(Vector3 pos, float decibel, float rad, float duration, NoiseData.NoiseType type)
        {
            position = pos;
            deceibel = decibel;
            radius = rad;
            expireTime = Time.time + duration;
            noiseType = type;
        }
    }
    
    private List<ActiveNoise> activeNoises = new List<ActiveNoise>();
    
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
        // 시간이 다 된 소음 정보들을 리스트에서 제거
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

    // 외부에서 소음 발생 요청 시 호출
    public void GenerateNoise(Vector3 position, NoiseData.NoiseType noiseType)
    {
        float decibel = GetDecibel(noiseType) * Random.Range(1f - variationRange, 1f + variationRange);
        float calculatedRadius = decibel * radiusMultiplier;

        if (calculatedRadius <= 0f)
        {
            Debug.LogWarning($"[NoiseManager] '{noiseType}' 소음 반경이 0입니다. NoiseData가 할당되었는지, 해당 타입의 데시벨 값이 입력되었는지 확인하세요.");
            return;
        }

        activeNoises.Add(new ActiveNoise(position, decibel, calculatedRadius, displayTime, noiseType));
        NotifyEnemies(position, calculatedRadius);
    }

    // 소음 발생 위치와 범위를 감지 가능한 적들에게 알림
    private void NotifyEnemies(Vector3 position, float radius)
    {
        Collider[] hitColliders = Physics.OverlapSphere(position, radius);
        foreach (var hit in hitColliders)
        {
            INoiseListener listener = hit.GetComponentInParent<INoiseListener>();
            if (listener != null)
            {
                listener.ListenNoise(position);
            }
        }
    }

    // 에디터 씬 뷰에서 소음 범위를 시각화
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