using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class UINeonAnimator : MonoBehaviour
{
    public enum NeonMode { Breathing, Flickering }

    [Header("설정")]
    public NeonMode mode = NeonMode.Breathing;
    public Image neonImage; // 네온 효과 이미지 할당

    [Header("은은한 숨쉬기 설정 (Breathing)")]
    public float minAlpha = 0.2f;   // 최소 밝기
    public float maxAlpha = 1.0f;   // 최대 밝기
    public float duration = 2.0f;   // 한 주기 시간

    [Header("지지직 깜빡임 설정 (Flickering)")]
    public float flickerInterval = 0.1f; // 깜빡임 간격

    private void Start()
    {
        if (neonImage == null) neonImage = GetComponent<Image>();
        
        PlayNeonEffect();
    }

    public void PlayNeonEffect()
    {
        // 기존 애니메이션 중복 실행 방지
        neonImage.DOKill();

        if (mode == NeonMode.Breathing)
        {
            // 부드럽게 밝아졌다 어두워졌다 반복
            neonImage.DOFade(minAlpha, duration)
                .From(maxAlpha)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }
        else if (mode == NeonMode.Flickering)
        {
            // 불규칙하게 지지직거리는 네온 사인 효과
            Sequence flickerSeq = DOTween.Sequence();
            
            // 0.05초~0.2초 사이의 랜덤한 간격으로 알파값을 조절하여 깜빡임 연출
            flickerSeq.AppendCallback(() => {
                float randomAlpha = Random.Range(0.3f, 1.0f);
                neonImage.canvasRenderer.SetAlpha(randomAlpha);
            });
            flickerSeq.AppendInterval(flickerInterval);
            flickerSeq.SetLoops(-1);
        }
    }
}