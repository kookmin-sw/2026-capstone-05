using UnityEngine;
using UnityEngine.UI;

public class AdvancedFrostEffectUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerCondition player;
    
    [Header("UI Layers (Images)")]
    public Image[] frostBaseImages;    
    public Image[] scratchImages;      

    [Header("Coldness Thresholds")]
    public float tier1Start = 25f;  
    public float tier2Start = 50f;
    public float tier3Start = 75f;
    public float maxColdness = 100f;

    [Header("Base Frost Settings")]
    public float shiverSpeed = 15f;      
    public float shiverAmount = 0.05f;   
    public float baseTransitionSpeed = 5f;   

    [Header("Scratch Crossfade Settings")]
    [Tooltip("스크래치가 차오르고 사라지는 전환 속도 (예: 0.5면 2초에 걸쳐 교체됨)")]
    public float scratchTransitionSpeed = 0.5f; 

    // 스크래치 크로스페이드를 위한 상태 변수
    private float scratchProgress = 0f; 
    private int currentScratchIndex = -1; 
    private int previousScratchIndex = -1; 

    private void Start()
    {
        foreach (var img in frostBaseImages) if (img != null) img.color = new Color(1, 1, 1, 0);
        foreach (var img in scratchImages) if (img != null) img.color = new Color(1, 1, 1, 0);
    }

    private void Update()
    {
        if (player == null) return;

        float cold = player.coldness.currentValue;

        // 공통 진동(Shiver) 효과 계산
        float vibration = Mathf.Sin(Time.time * shiverSpeed) * shiverAmount;


        // 1. 베이스 레이어 (1단계 25%부터 활성화)

        int activeBaseIndex = -1;
        if (cold >= tier3Start) activeBaseIndex = 2;
        else if (cold >= tier2Start) activeBaseIndex = 1;
        else if (cold >= tier1Start) activeBaseIndex = 0;

        float baseProportion = 0f;
        if (cold >= tier1Start) baseProportion = Mathf.InverseLerp(tier1Start, maxColdness, cold);
        
        float targetBaseAlpha = (cold >= tier1Start) ? Mathf.Clamp01(baseProportion + vibration) : 0f;

        for (int i = 0; i < frostBaseImages.Length; i++)
        {
            if (frostBaseImages[i] == null) continue;
            float imgTargetAlpha = (i == activeBaseIndex) ? targetBaseAlpha : 0f;
            float currentAlpha = frostBaseImages[i].color.a;
            frostBaseImages[i].color = new Color(1, 1, 1, Mathf.Lerp(currentAlpha, imgTargetAlpha, Time.deltaTime * baseTransitionSpeed));
        }


        // 2. 스크래치 레이어 (2단계 50%부터 활성화)

        if (cold >= tier2Start)
        {
            // 스크래치의 기본 최대 진하기 비율 (50~100 구간)
            float scratchProportion = Mathf.InverseLerp(tier2Start, maxColdness, cold);
            
            // 🔥 수정됨: 스크래치의 최대 알파값 한계치에도 진동(vibration)을 더해줍니다!
            // 베이스보다 약간 더 돋보이게 기본적으로 1.2배를 해준 뒤 진동을 섞습니다.
            float maxScratchAlphaWithVibration = Mathf.Clamp01((scratchProportion * 1.2f) + vibration); 

            if (currentScratchIndex == -1)
            {
                currentScratchIndex = Random.Range(0, scratchImages.Length);
            }

            // 크로스페이드 진행도 업데이트
            scratchProgress += Time.deltaTime * scratchTransitionSpeed;

            if (scratchProgress >= 1f)
            {
                scratchProgress = 0f;
                previousScratchIndex = currentScratchIndex;
                currentScratchIndex = GetNextScratchIndex(previousScratchIndex);
            }

            // 실제 이미지에 알파값 적용
            for (int i = 0; i < scratchImages.Length; i++)
            {
                if (scratchImages[i] == null) continue;

                float targetScratchAlpha = 0f;

                if (i == currentScratchIndex)
                {
                    // 새 스크래치: 진동이 포함된 최대치를 향해 차오름
                    targetScratchAlpha = scratchProgress * maxScratchAlphaWithVibration;
                }
                else if (i == previousScratchIndex)
                {
                    // 이전 스크래치: 진동이 포함된 최대치에서 서서히 옅어짐
                    targetScratchAlpha = (1f - scratchProgress) * maxScratchAlphaWithVibration;
                }

                scratchImages[i].color = new Color(1, 1, 1, targetScratchAlpha);
            }
        }
        else
        {
            currentScratchIndex = -1;
            previousScratchIndex = -1;
            scratchProgress = 0f;

            foreach (var img in scratchImages)
            {
                if (img != null) img.color = new Color(1, 1, 1, 0);
            }
        }
    }

    private int GetNextScratchIndex(int excludeIndex)
    {
        if (scratchImages.Length <= 1) return 0;
        int newIndex;
        do { newIndex = Random.Range(0, scratchImages.Length); }
        while (newIndex == excludeIndex);
        return newIndex;
    }
}