using UnityEngine;
using DG.Tweening;

public class UIPanelController : MonoBehaviour
{
    [Header("애니메이션 속도")]
    public float openDuration = 0.3f;
    public float closeDuration = 0.25f;

    private RectTransform rectTransform;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    // 버튼 OnClick 이벤트에서 이 함수를 호출하여 창을 엽니다.
    public void OpenPanel()
    {
        // 1. 오브젝트를 먼저 켭니다.
        gameObject.SetActive(true);

        // 2. 초기 상태 세팅 (X축 스케일을 0으로 만들어 중앙 세로선 형태로 대기)
        rectTransform.localScale = new Vector3(0, 1, 1);

        // 3. '뻔쩍' 열리는 시퀀스 애니메이션
        Sequence seq = DOTween.Sequence();
        seq.SetDelay(closeDuration); // 닫히는 애니메이션이 끝난 후 약간의 딜레이를 주고 열리도록
        
        // 살짝 원래 크기(1)보다 커지게 오버슈팅해서 튀어나오는 느낌
        seq.Append(rectTransform.DOScaleX(1.05f, openDuration * 0.7f).SetEase(Ease.OutExpo));
        // 다시 원래 크기(1)로 돌아옴
        seq.Append(rectTransform.DOScaleX(1f, openDuration * 0.3f).SetEase(Ease.InOutSine));
    }

    // 버튼 OnClick 이벤트에서 이 함수를 호출하여 창을 닫습니다.
    public void ClosePanel()
    {
        // 1. '뻔쩍' 닫히는 시퀀스 애니메이션
        Sequence seq = DOTween.Sequence();
        
        // 닫히기 직전에 아주 살짝 커지면서 힘을 모으는 느낌
        seq.Append(rectTransform.DOScaleX(1.05f, closeDuration * 0.3f).SetEase(Ease.OutQuad));
        // 중앙 세로선으로 확 쪼그라듦 (X축 스케일 0)
        seq.Append(rectTransform.DOScaleX(0f, closeDuration * 0.7f).SetEase(Ease.InExpo));

        // 2. 애니메이션이 완전히 끝난 후 오브젝트를 끔
        seq.OnComplete(() =>
        {
            gameObject.SetActive(false);
        });
    }
}