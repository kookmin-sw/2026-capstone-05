using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class NoiseUIController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerNoiseEmitter noiseEmitter;
    [SerializeField] private Image gaugeBar; 
    
    [Header("UI Effects (New)")]
    [SerializeField] private CanvasGroup canvasGroup; // 캔버스 그룹 연결
    [SerializeField] private float fadeSpeed = 5f;    // 투명도 변하는 속도
    [SerializeField] private float minAlpha = 0.2f;   // 가만히 있을 때 최소 투명도
    [SerializeField] private float dangerThreshold = 0.7f; // 붉은색 도달 기준치 (70%)

    [Header("Settings")]
    [SerializeField] private float smoothSpeed = 8f; 
    
    private CharacterController controller; 
    private Transform localPlayerTransform;
    private float lastNoiseIntensity;
    [SerializeField] private float noiseDecaySpeed = 1.6f;
    private float targetFill = 0f;          
    private bool isDangerPulsing = false;   
    private Sequence dangerSequence;

    private void Start()
    {
        ResolveLocalPlayer();

        if (NoiseManager.Instance != null)
        {
            NoiseManager.Instance.OnNoiseGenerated += HandleNoiseGenerated;
        }
        
        // 시작할 때 투명하게 세팅
        if (canvasGroup != null) canvasGroup.alpha = minAlpha;
    }

    private void Update()
    {
        UpdateNoiseLogic();
        
        // 1. 부드럽게 게이지 조절
        gaugeBar.fillAmount = Mathf.Lerp(gaugeBar.fillAmount, targetFill, Time.deltaTime * smoothSpeed);

        // 2. 투명도 자동 조절
        if (canvasGroup != null)
        {
            float targetAlpha = (targetFill > 0.05f) ? 1f : minAlpha;
            canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, targetAlpha, Time.deltaTime * fadeSpeed);
        }

        // 3. 위험 구역 쿵쾅거림 효과
        // 수정: 들쭉날쭉한 targetFill이 아니라, 스무스하게 쫓아가는 gaugeBar.fillAmount를 기준으로 체크합니다.
        HandleDangerPulse(gaugeBar.fillAmount); 
    }

    private void UpdateNoiseLogic()
    {
        if (controller == null)
        {
            ResolveLocalPlayer();
            if (controller == null) return;
        }

        lastNoiseIntensity = Mathf.Max(0f, lastNoiseIntensity - Time.deltaTime * noiseDecaySpeed);

        // 수평 속도 계산
        Vector3 horizontalVel = new Vector3(controller.velocity.x, 0, controller.velocity.z);
        float speed = horizontalVel.magnitude;

        // 이동 상태에 따른 기본 수치 (최대 100 기준 비율)
        if (speed > 0.1f)
        {
            // 속도에 비례하여 0.2 ~ 0.8 사이로 타겟 설정
            targetFill = Mathf.Clamp(speed / 7f, 0.1f, 0.9f);
        }
        else
        {
            targetFill = 0f;
        }

        // 점프/낙하 중일 때 소음 강조
        if (Mathf.Abs(controller.velocity.y) > 0.5f)
        {
            targetFill = Mathf.Max(targetFill, 0.7f);
        }

        targetFill = Mathf.Clamp01(Mathf.Max(targetFill, lastNoiseIntensity));
    }

    private void ResolveLocalPlayer()
    {
        if (noiseEmitter != null)
        {
            controller = noiseEmitter.GetComponent<CharacterController>();
            localPlayerTransform = noiseEmitter.transform;
            return;
        }

        PlayerController[] controllers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var pc in controllers)
        {
            if (pc != null && pc.IsLocalPlayer)
            {
                noiseEmitter = pc.GetComponent<PlayerNoiseEmitter>();
                controller = pc.GetComponent<CharacterController>();
                localPlayerTransform = pc.transform;
                break;
            }
        }
    }

    private void HandleNoiseGenerated(Vector3 position, NoiseData.NoiseType noiseType, float decibel, float radius)
    {
        if (localPlayerTransform == null)
            return;

        float distance = Vector3.Distance(localPlayerTransform.position, position);
        if (distance > radius)
            return;

        float normalized = Mathf.Clamp01(decibel / 100f);
        float attenuation = 1f - Mathf.Clamp01(distance / Mathf.Max(radius, 0.01f));
        lastNoiseIntensity = Mathf.Max(lastNoiseIntensity, normalized * attenuation);
    }

    private void OnDestroy()
    {
        if (NoiseManager.Instance != null)
        {
            NoiseManager.Instance.OnNoiseGenerated -= HandleNoiseGenerated;
        }
    }

    private void HandleDangerPulse(float currentFill)
    {
        if (currentFill >= dangerThreshold)
        {
            if (!isDangerPulsing)
            {
                isDangerPulsing = true;
                
                if (canvasGroup != null)
                {
                    // 혹시 실행 중인 다른 애니메이션이 있다면 깔끔하게 정리
                    canvasGroup.transform.DOKill();
                    if (dangerSequence != null) dangerSequence.Kill();

                    // 새로운 애니메이션 시퀀스(순서도) 만들기
                    dangerSequence = DOTween.Sequence();

                    // 1. '두' (빠르고 살짝 커짐)
                    dangerSequence.Append(canvasGroup.transform.DOScale(1.01f, 0.05f).SetEase(Ease.OutQuad));
                    dangerSequence.Append(canvasGroup.transform.DOScale(1.0f, 0.05f).SetEase(Ease.InQuad));
                    
                    // 2. '둥' (조금 더 크고 여운 있게 커짐)
                    dangerSequence.Append(canvasGroup.transform.DOScale(1.02f, 0.05f).SetEase(Ease.OutQuad));
                    dangerSequence.Append(canvasGroup.transform.DOScale(1.0f, 0.05f).SetEase(Ease.InQuad));

                    // 3. '(진동/휴식)' (0.6초 동안 가만히 대기)
                    dangerSequence.AppendInterval(0.3f);

                    // 4. 이 순서를 무한 반복!
                    dangerSequence.SetLoops(-1);
                }
            }
        }
        else
        {
            // 위험 구역에서 벗어났을 때
            if (isDangerPulsing)
            {
                isDangerPulsing = false;
                
                if (canvasGroup != null)
                {
                    // 진행 중인 심장박동 시퀀스를 즉시 파괴
                    if (dangerSequence != null) dangerSequence.Kill();
                    canvasGroup.transform.DOKill(); 
                    
                    // 원래 크기로 자연스럽게 복구
                    canvasGroup.transform.DOScale(1f, 0.2f); 
                }
            }
        }
    }
}