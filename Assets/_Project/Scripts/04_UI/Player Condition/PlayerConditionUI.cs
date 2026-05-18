using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class PlayerConditionUI : MonoBehaviour
{
    private enum StaminaState { Full, Recovering, Using, Idle }

    [SerializeField] private PlayerCondition player;
    [SerializeField] private float playerSearchInterval = 0.5f;

    [Header("Circular Gauges (Max 100)")]
    public Image healthFill;
    public Image satietyFill;
    public Image coldnessFill;

    [Header("Stamina Slider")]
    public Image staminaFillLeft; 
    public Image staminaFillRight; 

    [Header("Percentage Texts")]
    public TextMeshProUGUI healthText;
    public TextMeshProUGUI satietyText;
    public TextMeshProUGUI coldnessText;

    [Header("Warning Effect Controllers")]
    public StatWarningUI healthWarning;
    public StatWarningUI satietyWarning;
    public StatWarningUI coldnessWarning;

    [Header("UI Animation Settings")]
    public float fadeDuration = 0.3f; // 스태미나 투명도 변하는 속도
    [Tooltip("게이지가 부드럽게 깎이는 속도 (높을수록 빨리 따라갑니다)")]
    public float fillSpeed = 5f;      // 게이지 증감 속도

    private float previousStamina;
    private StaminaState currentStaminaState = StaminaState.Idle;
    private float playerSearchTimer;
    private int displayedHealth = -1;
    private int displayedSatiety = -1;
    private int displayedColdness = -1;

    private void Start()
    {
        TryBindLocalPlayer();

        if (player != null)
        {
            previousStamina = player.stamina.currentValue;
        }
    }

    private void Update()
    {
        if (player == null)
        {
            TryBindLocalPlayerByInterval();
            if (player == null) return;
        }

        // 1. 원형 UI 게이지 업데이트 (부드러운 감쇠 적용)
        UpdateCircle(healthFill, healthText, player.health.currentValue, ref displayedHealth);
        UpdateCircle(satietyFill, satietyText, player.satiety.currentValue, ref displayedSatiety);
        UpdateCircle(coldnessFill, coldnessText, player.coldness.currentValue, ref displayedColdness);

        // 2. 위험 수치 경고 이펙트 업데이트
        if(healthWarning != null) healthWarning.UpdateStatWarning(player.health.currentValue);
        if(satietyWarning != null) satietyWarning.UpdateStatWarning(player.satiety.currentValue);
        if(coldnessWarning != null) coldnessWarning.UpdateStatWarning(player.coldness.currentValue);

        // 3. 스태미나 게이지 업데이트 및 페이드(투명도) 로직 처리
        UpdateStamina();
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
        if (!LocalPlayerReferenceResolver.TryGetLocalCondition(out PlayerCondition localCondition)) return;

        player = localCondition;
        previousStamina = player.stamina.currentValue;
    }

    private void UpdateStamina()
    {
        float currentStamina = player.stamina.currentValue;
        float targetStaminaRatio = currentStamina / 100f;

        // 스태미나 게이지도 부드럽게 채워지거나 깎이도록 Lerp 적용
        if (staminaFillLeft != null) 
            staminaFillLeft.fillAmount = Mathf.Lerp(staminaFillLeft.fillAmount, targetStaminaRatio, Time.deltaTime * fillSpeed);
        if (staminaFillRight != null) 
            staminaFillRight.fillAmount = Mathf.Lerp(staminaFillRight.fillAmount, targetStaminaRatio, Time.deltaTime * fillSpeed);

        // --- 스태미나 투명도 판단 로직 ---
        StaminaState newState = currentStaminaState;

        if (currentStamina >= 99.9f)
        {
            newState = StaminaState.Full;
        }
        else if (currentStamina < previousStamina)
        {
            newState = StaminaState.Using;
        }
        else if (currentStamina > previousStamina)
        {
            newState = StaminaState.Recovering;
        }

        if (newState != currentStaminaState)
        {
            currentStaminaState = newState;
            ApplyStaminaAlpha(newState);
        }

        previousStamina = currentStamina;
    }

    private void ApplyStaminaAlpha(StaminaState state)
    {
        float targetAlpha = 0f;

        switch (state)
        {
            case StaminaState.Full: targetAlpha = 0f; break;
            case StaminaState.Recovering: targetAlpha = 0.5f; break;
            case StaminaState.Using: targetAlpha = 1f; break;
        }

        if (staminaFillLeft != null)
        {
            staminaFillLeft.DOKill();
            staminaFillLeft.DOFade(targetAlpha, fadeDuration).SetEase(Ease.OutCubic);
        }

        if (staminaFillRight != null)
        {
            staminaFillRight.DOKill();
            staminaFillRight.DOFade(targetAlpha, fadeDuration).SetEase(Ease.OutCubic);
        }
    }

    // 핵심: 게이지가 서서히 줄어드는 애니메이션 함수
    private void UpdateCircle(Image img, TextMeshProUGUI txt, float currentVal, ref int displayedValue)
    {
        if (img != null)
        {
            // 목표치 계산[cite: 1]
            float targetRatio = currentVal / 100f;
            
            // 현재 게이지의 fillAmount에서 목표치(targetRatio)까지 Time.deltaTime을 이용해 부드럽게 추적
            img.fillAmount = Mathf.Lerp(img.fillAmount, targetRatio, Time.deltaTime * fillSpeed);
            
            if (txt != null)
            {
                // 텍스트 숫자도 실제 데이터(currentVal)가 아니라 '시각적인 게이지의 비율(img.fillAmount)'을 따라가도록 설정
                // 이렇게 하면 게이지가 내려가는 동안 숫자도 다라라락~ 하고 자연스럽게 내려갑니다.
                int nextValue = Mathf.RoundToInt(img.fillAmount * 100f);
                if (nextValue != displayedValue)
                {
                    displayedValue = nextValue;
                    txt.SetText("{0}%", displayedValue);
                }
            }
        }
    }
}
