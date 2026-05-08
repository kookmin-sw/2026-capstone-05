using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class NoiseMeterUI : MonoBehaviour
{
    [Header("UI 연결")]
    public CanvasGroup noiseCanvasGroup; // 투명도 조절을 위해 전체를 묶은 CanvasGroup
    public Image noiseFillImage;         // 게이지가 차오르는 이미지 (Filled 방식)

    [Header("투명도 페이드 설정")]
    public float fadeSpeed = 3f;
    public float minAlpha = 0.2f;

    [Header("오디오 피크(게이지) 설정")]
    public float riseSpeed = 15f; // 소음이 커질 때 차오르는 속도 (빠름)
    public float fallSpeed = 2f;  // 소음이 줄어들 때 떨어지는 속도 (느림 - 잔향 효과)

    [Header("위험 경고 설정")]
    public float dangerThreshold = 0.7f; // 70% 이상일 때 위험
    private bool isDangerPulsing = false;

    private void Start()
    {
        if (noiseCanvasGroup != null) noiseCanvasGroup.alpha = minAlpha;
    }

    // 플레이어의 행동(걷기, 뛰기 등)에 따라 실시간으로 호출해주세요 (0.0 ~ 1.0)
    public void UpdateNoiseLevel(float targetNoiseLevel)
    {
        // 1. 오디오 피크 미터 효과 (오를 땐 빠르게, 내릴 땐 느리게)
        float currentFill = noiseFillImage.fillAmount;
        float currentSpeed = (targetNoiseLevel > currentFill) ? riseSpeed : fallSpeed;
        noiseFillImage.fillAmount = Mathf.Lerp(currentFill, targetNoiseLevel, Time.deltaTime * currentSpeed);

        // 2. 투명도 동적 조절 (소음이 있으면 선명하게, 없으면 투명하게)
        float targetAlpha = (targetNoiseLevel > 0.05f) ? 1f : minAlpha;
        if (noiseCanvasGroup != null)
        {
            noiseCanvasGroup.alpha = Mathf.Lerp(noiseCanvasGroup.alpha, targetAlpha, Time.deltaTime * fadeSpeed);
        }

        // 3. 위험 구역 펄스 효과 (70% 이상일 때 쿵쾅거림)
        if (targetNoiseLevel >= dangerThreshold)
        {
            if (!isDangerPulsing) StartDangerPulse();
        }
        else
        {
            if (isDangerPulsing) StopDangerPulse();
        }
    }

    private void StartDangerPulse()
    {
        isDangerPulsing = true;
        // UI 전체가 1.1배로 미세하게 쿵쾅거림
        transform.DOScale(1.1f, 0.2f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
    }

    private void StopDangerPulse()
    {
        isDangerPulsing = false;
        transform.DOKill(); // 애니메이션 중지
        transform.DOScale(1f, 0.2f); // 원래 크기로 복구
    }
}