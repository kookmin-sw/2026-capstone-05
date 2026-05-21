using UnityEngine;
using UnityEngine.UI;

public class NoiseUIController : MonoBehaviour
{
    private const int SpeakerLevelCount = 4;

    [Header("References")]
    [SerializeField] private PlayerNoiseListener noiseListener;
    [SerializeField] private float playerSearchInterval = 0.5f;
    
    [Header("개별 블록 UI 연결 (1~30)")]
    public Image[] noiseBars; 
    public Image[] neonBars;

    [Header("Speaker Icon UI")]
    [SerializeField] private Image[] speakerIconLevels = new Image[SpeakerLevelCount];
    [SerializeField, Range(0f, 1f)] private float speakerLevel1Threshold = 0.2f;
    [SerializeField, Range(0f, 1f)] private float speakerLevel2Threshold = 0.5f;
    [SerializeField, Range(0f, 1f)] private float speakerLevel3Threshold = 0.8f;

    [Header("Settings")]
    [SerializeField] private float smoothSpeed = 8f; 
    [SerializeField] private float dangerThreshold = 0.7f; 
    
    [Header("위험 수치 깜빡임 설정")]
    public float flashSpeed = 8f;       // 깜빡이는 속도 (높을수록 빠름)
    
    [Tooltip("위험할 때 '기본 노이즈 바'의 최소 투명도")]
    public float minNoiseAlpha = 0.3f;  // 1.0(선명)과 이 수치 사이를 깜빡임

    public float maxNeonAlpha = 1.0f;   // 위험할 때 네온 최대 밝기
    public float minNeonAlpha = 0.2f;   // 위험할 때 네온 최소 밝기
    public float normalNeonAlpha = 0.0f;// 평소 안전할 때 네온 밝기 (0 = 안 보임)
    
    private float targetFill = 0f;          
    private float currentFill = 0f; 
    private float playerSearchTimer;
    private int currentSpeakerLevel = -1;

    private void Start()
    {
        TryBindLocalPlayer();
        TryAutoBindSpeakerIcons();
        UpdateSpeakerIcon(0f);
    }

    private void Update()
    {
        if (noiseListener == null)
        {
            TryBindLocalPlayerByInterval();
        }

        UpdateNoiseLogic();
        
        // 1. 부드러운 타겟 수치(0.0 ~ 1.0) 계산
        currentFill = Mathf.Lerp(currentFill, targetFill, Time.deltaTime * smoothSpeed);

        // 2. 0.0~1.0 사이의 값을 전체 블록 개수에 곱해 몇 칸을 켤지 계산
        int totalBars = noiseBars != null ? noiseBars.Length : 0;
        int activeBarsCount = Mathf.RoundToInt(currentFill * totalBars);

        // 3. 알파값 계산 (위험 수치일 때 파동을 일으켜 깜빡임)
        float currentNeonAlpha = normalNeonAlpha;
        float currentNoiseAlpha = 1.0f; // 평소 기본 바는 100% 선명함

        if (currentFill >= dangerThreshold)
        {
            // Time.time을 이용한 동기화된 파동 (0.0 ~ 1.0)
            float wave = (Mathf.Sin(Time.time * flashSpeed) + 1f) * 0.5f; 
            
            // 동일한 파동(wave)을 이용해 네온과 기본 바의 투명도를 동시에 조절
            currentNeonAlpha = Mathf.Lerp(minNeonAlpha, maxNeonAlpha, wave);
            currentNoiseAlpha = Mathf.Lerp(minNoiseAlpha, 1.0f, wave);
        }

        float speakerFill = totalBars > 0 && activeBarsCount <= 0 ? 0f : currentFill;
        UpdateSpeakerIcon(speakerFill);

        // 4. 1번부터 30번까지 루프를 돌며 각 블록 켜고 끄기 및 색상 적용
        for (int i = 0; i < totalBars; i++)
        {
            bool isActive = i < activeBarsCount; 

            // 기본 노이즈 바 제어
            if (noiseBars[i] != null)
            {
                noiseBars[i].enabled = isActive;
                if (isActive)
                {
                    Color c = noiseBars[i].color;
                    c.a = currentNoiseAlpha; // 계산된 투명도 적용
                    noiseBars[i].color = c;
                }
            }

            // 네온 바 제어
            if (neonBars.Length > i && neonBars[i] != null)
            {
                neonBars[i].enabled = isActive;
                if (isActive)
                {
                    Color c = neonBars[i].color;
                    c.a = currentNeonAlpha; // 계산된 투명도 적용
                    neonBars[i].color = c;
                }
            }
        }
    }

    private void UpdateNoiseLogic()
    {
        if (noiseListener == null)
        {
            return;
        }

        if (NoiseManager.Instance == null || NoiseManager.Instance.MaxDecibel <= 0f)
        {
            targetFill = 0f;
            return;
        }

        float currentDecibel = noiseListener.CurrentDecibel;
        float maxDecibel = NoiseManager.Instance.MaxDecibel; 
        targetFill = Mathf.Clamp01(currentDecibel / maxDecibel);
    }

    private void UpdateSpeakerIcon(float normalizedDecibel)
    {
        if (speakerIconLevels == null || speakerIconLevels.Length == 0)
            return;

        int nextLevel = GetSpeakerLevel(normalizedDecibel);
        if (currentSpeakerLevel == nextLevel)
            return;

        currentSpeakerLevel = nextLevel;
        for (int i = 0; i < speakerIconLevels.Length; i++)
        {
            if (speakerIconLevels[i] != null)
            {
                speakerIconLevels[i].enabled = i == currentSpeakerLevel;
            }
        }
    }

    private int GetSpeakerLevel(float normalizedDecibel)
    {
        if (normalizedDecibel <= 0f)
            return 0;

        if (normalizedDecibel >= speakerLevel3Threshold)
            return 3;

        if (normalizedDecibel >= speakerLevel2Threshold)
            return 2;

        if (normalizedDecibel >= speakerLevel1Threshold)
            return 1;

        return 1;
    }

    private void TryAutoBindSpeakerIcons()
    {
        EnsureSpeakerIconArray();

        Transform searchRoot = transform.parent != null ? transform.parent : transform;
        Image[] candidateImages = searchRoot.GetComponentsInChildren<Image>(true);

        for (int level = 0; level < SpeakerLevelCount; level++)
        {
            if (speakerIconLevels[level] != null)
                continue;

            string expectedName = $"Speaker_Icon_{level}";
            foreach (Image candidate in candidateImages)
            {
                if (candidate != null && candidate.name == expectedName)
                {
                    speakerIconLevels[level] = candidate;
                    break;
                }
            }
        }
    }

    private void EnsureSpeakerIconArray()
    {
        if (speakerIconLevels != null && speakerIconLevels.Length >= SpeakerLevelCount)
            return;

        Image[] resizedIcons = new Image[SpeakerLevelCount];
        if (speakerIconLevels != null)
        {
            for (int i = 0; i < speakerIconLevels.Length; i++)
            {
                resizedIcons[i] = speakerIconLevels[i];
            }
        }

        speakerIconLevels = resizedIcons;
    }

    private void TryBindLocalPlayerByInterval()
    {
        playerSearchTimer -= Time.deltaTime;
        if (playerSearchTimer > 0f) return;

        playerSearchTimer = playerSearchInterval;
        TryBindLocalPlayer();
    }

    private void TryBindLocalPlayer()
    {
        if (!LocalPlayerReferenceResolver.TryGetLocalNoiseListener(out PlayerNoiseListener localListener)) return;

        noiseListener = localListener;
    }
}
