using UnityEngine;

public class Live2DController : MonoBehaviour
{
    [Header("Live2D 모델의 Animator")]
    public Animator mascotAnimator;

    private void OnEnable()
    {
        // 5가지 신호 구독 시작!
        MascotEventManager.OnGreeting  += PlayGreetingAnimation;
        MascotEventManager.OnThankYou  += PlayThankYouAnimation;
        MascotEventManager.OnSurprise  += PlaySurpriseAnimation;
        MascotEventManager.OnLaugh     += PlayLaughAnimation;
        MascotEventManager.OnReject    += PlayRejectAnimation;
    }

    private void OnDisable()
    {
        // 구독 해제 (이 오브젝트가 꺼지거나 파괴될 때)
        MascotEventManager.OnGreeting  -= PlayGreetingAnimation;
        MascotEventManager.OnThankYou  -= PlayThankYouAnimation;
        MascotEventManager.OnSurprise  -= PlaySurpriseAnimation;
        MascotEventManager.OnLaugh     -= PlayLaughAnimation;
        MascotEventManager.OnReject    -= PlayRejectAnimation;
    }

    // --- 각 상황에 맞는 애니메이션 실행 함수 ---

    private void PlayGreetingAnimation()
    {
        if (mascotAnimator != null) mascotAnimator.SetTrigger("PlayGreeting"); // Trigger 이름 확인!
        Debug.Log("설이: 어서오세요! (인사)");
    }

    private void PlayThankYouAnimation()
    {
        if (mascotAnimator != null) mascotAnimator.SetTrigger("PlayThankYou");
        Debug.Log("설이: 감사합니다! (감사)");
    }

    private void PlaySurpriseAnimation()
    {
        if (mascotAnimator != null) mascotAnimator.SetTrigger("PlaySurprise");
        Debug.Log("설이: 앗 깜짝이야! (놀람)");
    }

    private void PlayLaughAnimation()
    {
        if (mascotAnimator != null) mascotAnimator.SetTrigger("PlaySmile");
        Debug.Log("설이: 헤헷 (웃음)");
    }

    private void PlayRejectAnimation()
    {
        if (mascotAnimator != null) mascotAnimator.SetTrigger("PlayReject"); // 예를 들어 고개를 젓는 모션
        Debug.Log("설이: 으음... 안돼요! (거부)");
    }
}