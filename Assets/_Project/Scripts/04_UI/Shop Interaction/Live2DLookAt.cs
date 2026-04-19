using UnityEngine;
using Live2D.Cubism.Framework.LookAt;

[RequireComponent(typeof(CubismLookController))]
[RequireComponent(typeof(Animator))]
public class Live2DLookAt : MonoBehaviour, ICubismLookTarget
{
    [Header("시선 추적 세팅")]
    [Range(0.1f, 10f)] public float damping = 5f; // 고개 돌아가는 속도
    private CubismLookController lookController;
    private Vector3 targetPos;

    [Header("애니메이션 세팅")]
    private Animator mascotAnimator;

    void Awake()
    {
        lookController = GetComponent<CubismLookController>();
        mascotAnimator = GetComponent<Animator>();
        
        // 룩 컨트롤러에게 내가 타겟이라고 알려줌
        if (lookController != null) lookController.Target = this;
    }

    // --- 애니메이션 이벤트 구독 ---
    private void OnEnable()
    {
        MascotEventManager.OnGreeting += PlayGreeting;
        MascotEventManager.OnThankYou += PlayThankYou;
        MascotEventManager.OnSurprise += PlaySurprise;
        MascotEventManager.OnLaugh    += PlayLaugh;
        MascotEventManager.OnReject   += PlayReject;
    }

    private void OnDisable()
    {
        MascotEventManager.OnGreeting -= PlayGreeting;
        MascotEventManager.OnThankYou -= PlayThankYou;
        MascotEventManager.OnSurprise -= PlaySurprise;
        MascotEventManager.OnLaugh    -= PlayLaugh;
        MascotEventManager.OnReject   -= PlayReject;
    }

    // --- 마우스 추적 (Render Texture UI 환경에 완벽 최적화) ---
    void Update()
    {
        // 화면 전체에서 마우스가 어디 있는지 비율(-1 ~ 1)로 정확히 계산
        float x = (Input.mousePosition.x / Screen.width) * 2f - 1f;
        float y = (Input.mousePosition.y / Screen.height) * 2f - 1f;

        Vector3 newTarget = new Vector3(x, y, 0);
        
        // 부드러운 시선 이동 (Lerp)
        targetPos = Vector3.Lerp(targetPos, newTarget, Time.deltaTime * damping);
    }

    // Live2D 엔진이 이 함수를 통해 targetPos 값을 가져감
    public Vector3 GetPosition() => targetPos;
    public bool IsActive() => true;

    // --- 애니메이션 실행 함수들 ---
    private void PlayGreeting()  { mascotAnimator.SetTrigger("PlayGreeting"); }
    private void PlayThankYou()  { mascotAnimator.SetTrigger("PlayThankYou"); }
    private void PlaySurprise()  { mascotAnimator.SetTrigger("PlaySurprise"); }
    private void PlayLaugh()     { mascotAnimator.SetTrigger("PlaySmile"); }
    private void PlayReject()    { mascotAnimator.SetTrigger("PlayReject"); }
}