using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening; // 네온의 DOTween을 제어하기 위해 추가

public class StatWarningUI : MonoBehaviour
{
    public enum StatType { Normal, Coldness }

    [Header("UI 연결")]
    public Image iconImage;           
    public Image fillImage;           
    public TextMeshProUGUI percentText; 

    [Header("스탯 타입 및 임계값 설정")]
    public StatType statType = StatType.Normal;
    public float dangerThreshold = 25f;
    public float coldPenaltyMin = 75f;
    public float coldPenaltyMax = 100f;

    [Header("번쩍임(Flashing) 애니메이션 설정")]
    public float flashSpeed = 4f; 
    public float minAlpha = 0.2f;      

    [Header("네온 효과 연동 (선택 사항)")]
    [Tooltip("배경 네온을 같이 깜빡이게 하려면 해당 스크립트를 연결하세요.")]
    public UINeonAnimator neonAnimator; 
    public float neonFlashMax = 1.0f; // 위험할 때 네온의 최대 밝기 (눈에 확 띄게)
    public float neonFlashMin = 0.3f; // 위험할 때 네온의 최소 밝기

    private bool isFlashing = false;

    public void UpdateStatWarning(float currentValue)
    {
        bool isDangerous = false;

        // 1. 위험 상태 판별
        if (statType == StatType.Normal)
        {
            if (currentValue <= dangerThreshold) isDangerous = true;
        }
        else if (statType == StatType.Coldness)
        {
            if (currentValue >= coldPenaltyMin && currentValue <= coldPenaltyMax) isDangerous = true;
        }

        // 2. 상태 전환에 따른 애니메이션 제어
        if (isDangerous && !isFlashing)
        {
            isFlashing = true;
            
            // 위험 상태 진입 시: 네온의 평상시 DOTween 애니메이션 강제 정지
            if (neonAnimator != null && neonAnimator.neonImage != null)
            {
                neonAnimator.neonImage.DOKill(); 
            }
        }
        else if (!isDangerous && isFlashing)
        {
            isFlashing = false;
            SetAlpha(1f); // 아이콘/텍스트 투명도 원상복구
            
            // 안전 상태 복귀 시: 네온의 평상시 숨쉬기 효과 재시작
            if (neonAnimator != null)
            {
                neonAnimator.PlayNeonEffect(); 
            }
        }
    }

    private void Update()
    {
        if (isFlashing)
        {
            // Time.time을 기준으로 한 완벽한 동기화 파동 (0.0 ~ 1.0)
            float wave = (Mathf.Sin(Time.time * flashSpeed) + 1f) * 0.5f; 
            
            // 1. 기본 UI (아이콘, 게이지, 텍스트) 알파값 적용
            float currentAlpha = Mathf.Lerp(minAlpha, 1f, wave);
            SetAlpha(currentAlpha);

            // 2. 네온 이미지 알파값 동시 적용 (동일한 wave 사용)
            if (neonAnimator != null && neonAnimator.neonImage != null)
            {
                float neonAlpha = Mathf.Lerp(neonFlashMin, neonFlashMax, wave);
                
                Color nc = neonAnimator.neonImage.color;
                nc.a = neonAlpha;
                neonAnimator.neonImage.color = nc;
            }
        }
    }

    private void SetAlpha(float alpha)
    {
        if (iconImage != null)
        {
            Color c = iconImage.color;
            c.a = alpha;
            iconImage.color = c;
        }

        if (fillImage != null)
        {
            Color c = fillImage.color;
            c.a = alpha;
            fillImage.color = c;
        }

        if (percentText != null)
        {
            Color c = percentText.color;
            c.a = alpha;
            percentText.color = c;
        }
    }
}