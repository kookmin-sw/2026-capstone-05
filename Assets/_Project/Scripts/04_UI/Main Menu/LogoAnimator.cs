using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class LogoAnimator : MonoBehaviour
{
    [Header("UI 대상")]
    [Tooltip("전체 로고를 움직일 부모 RectTransform")]
    public RectTransform logoContainer; 
    [Tooltip("알파 블렌딩을 적용할 얼음 필터 이미지")]
    public Image frostFilterImage;

    [Header("부유 효과 (Floating)")]
    public float floatDistance = 15f; // 위아래로 움직일 거리
    public float floatDuration = 5f; // 한 방향으로 이동하는 데 걸리는 시간

    [Header("얼음 필터 효과 (Alpha Blending)")]
    public float minAlpha = 0.1f; // 필터 최소 투명도
    public float maxAlpha = 0.3f; // 필터 최대 투명도
    public float fadeDuration = 3f; // 투명도가 변하는 데 걸리는 시간

    void Start()
    {
        // 1. 전체 로고 부유 효과 (Y축 이동)
        // 현재 위치에서 floatDistance만큼 위로 이동 후 다시 제자리로 돌아오기를 무한 반복
        logoContainer.DOAnchorPosY(logoContainer.anchoredPosition.y + floatDistance, floatDuration)
            .SetEase(Ease.InOutSine) // 시작과 끝이 부드러운 호러/몽환적인 느낌에 적합한 Ease
            .SetLoops(-1, LoopType.Yoyo);

        // 2. 얼음 필터 알파 블렌딩 (Fade 효과)
        // 시작할 때 알파값을 최소값으로 강제 설정
        Color startColor = frostFilterImage.color;
        startColor.a = minAlpha;
        frostFilterImage.color = startColor;

        // maxAlpha 값까지 서서히 진해졌다가 다시 연해지기를 무한 반복
        frostFilterImage.DOFade(maxAlpha, fadeDuration)
            .SetEase(Ease.InOutQuad) // 서서히 얼어붙는 듯한 자연스러운 곡선
            .SetLoops(-1, LoopType.Yoyo);
    }

    private void OnDestroy()
    {
        // 오브젝트가 파괴될 때 트윈을 정리하여 메모리 누수 방지
        logoContainer.DOKill();
        frostFilterImage.DOKill();
    }
}