using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class PlayerHitEffectUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerCondition player;
    [SerializeField] private float playerSearchInterval = 0.5f;

    [Header("UI Layers (Images)")]
    [Tooltip("피격 베이스 이미지 (화면 테두리 붉은 효과)를 넣어주세요")]
    public Image bloodBaseImage; 
    
    [Tooltip("3개의 핏자국 이미지를 배열에 넣어주세요")]
    public Image[] bloodSplatters; 

    [Header("Effect Settings")]
    public float fadeDuration = 0.8f;       // 효과가 서서히 사라지는 시간
    public float maxBaseAlpha = 0.8f;       // 베이스 효과의 최대 투명도 (1.0 = 완전 불투명)
    public float maxSplatterAlpha = 1.0f;   // 핏자국 효과의 최대 투명도

    private float lastHealth;
    private float playerSearchTimer;

    private void Start()
    {
        TryBindLocalPlayer();

        if (player != null)
        {
            lastHealth = player.health.currentValue;
        }

        // 시작 시 모든 피격 UI 투명하게 초기화
        InitializeUI();
    }

    private void InitializeUI()
    {
        if (bloodBaseImage != null) 
            bloodBaseImage.color = new Color(1, 1, 1, 0);

        foreach (var splatter in bloodSplatters)
        {
            if (splatter != null) 
                splatter.color = new Color(1, 1, 1, 0);
        }
    }

    private void Update()
    {
        if (player == null)
        {
            TryBindLocalPlayerByInterval();
        }

        if (player == null) return;

        float currentHealth = player.health.currentValue;

        // 체력 감소 감지 (피격 판정)
        if (currentHealth < lastHealth)
        {
            PlayHitEffect();
        }

        lastHealth = currentHealth;
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
        lastHealth = player.health.currentValue;
    }

    public void PlayHitEffect()
    {
        // [1] 베이스 이미지(테두리 붉은 효과) 즉시 표시 후 페이드 아웃
        if (bloodBaseImage != null)
        {
            bloodBaseImage.DOKill();
            bloodBaseImage.color = new Color(1, 1, 1, maxBaseAlpha);
            bloodBaseImage.DOFade(0f, fadeDuration).SetEase(Ease.OutQuad);
        }

        // [2] 3개의 핏자국 중 랜덤 1개 선택 후 즉시 표시 및 페이드 아웃 (팝핑 제거)
        if (bloodSplatters != null && bloodSplatters.Length > 0)
        {
            // 이전에 켜져있던 다른 핏자국들 즉시 투명화 및 트윈 종료
            foreach (var splatter in bloodSplatters) 
            { 
                if (splatter != null)
                {
                    splatter.DOKill(); 
                    splatter.color = new Color(1, 1, 1, 0);
                }
            }

            // 랜덤 인덱스 추첨
            int randomIndex = Random.Range(0, bloodSplatters.Length);
            Image selectedSplatter = bloodSplatters[randomIndex];

            if (selectedSplatter != null)
            {
                // 선택된 핏자국 알파값을 최대치로 즉시 켬
                selectedSplatter.color = new Color(1, 1, 1, maxSplatterAlpha);
                
                // 스케일(크기) 조절 없이 오직 알파값 페이드 아웃만 진행
                selectedSplatter.DOFade(0f, fadeDuration).SetEase(Ease.OutQuad);
            }
        }
    }
}
