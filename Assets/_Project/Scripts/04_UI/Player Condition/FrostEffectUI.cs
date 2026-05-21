using UnityEngine;
using UnityEngine.UI;

public class FrostEffectUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerCondition player;   // 플레이어 상태 스크립트
    [SerializeField] private float playerSearchInterval = 0.5f;
    [Tooltip("일러스트레이터로 만든 서리 이미지를 넣으세요")]
    public Image frostImage;                           

    [Header("Coldness Settings")]
    public float maxColdness = 100f;   // 최대 추위 수치
    [Tooltip("추위 수치가 이 값 이상일 때부터 서리가 보이기 시작합니다.")]
    public float showThreshold = 20f;  // 예: 추위가 20 이상일 때부터 서리 발생
    [SerializeField, Range(0f, 1f)] private float maxFrostAlpha = 0.68f;

    [Header("Shivering (진동) Settings")]
    [Tooltip("떨림의 속도 (높을수록 더 바들바들 떱니다)")]
    public float shiverSpeed = 1f;    
    [Tooltip("떨림의 폭 (기본 알파값에서 얼마나 위아래로 흔들릴지)")]
    public float shiverAmount = 0.08f; 
    private float playerSearchTimer;

    private void Start()
    {
        TryBindLocalPlayer();

        // 시작 시 서리 이미지를 완전 투명하게 초기화
        if (frostImage != null)
        {
            frostImage.color = new Color(1f, 1f, 1f, 0f);
        }
    }

    private void Update()
    {
        if (player == null)
        {
            TryBindLocalPlayerByInterval();
        }

        // 플레이어나 이미지가 연결되지 않았다면 작동 중지
        if (player == null || frostImage == null) return;

        // 1. 현재 추위 수치 가져오기 (0 ~ 100)
        float currentCold = player.coldness.currentValue;

        // 2. 추위 수치가 임계값(Threshold) 미만이면 서리를 끄고 함수 종료
        if (currentCold <= showThreshold)
        {
            frostImage.color = new Color(1f, 1f, 1f, 0f);
            return;
        }

        // 3. 기본 알파값 계산 (임계값 ~ 최대추위 사이를 0.0 ~ 1.0의 비율로 변환)
        // 예: 추위가 60이면 50% 투명도, 100이면 100% 투명도
        float baseAlpha = Mathf.InverseLerp(showThreshold, maxColdness, currentCold);

        // 4. 알파값 진동 효과 (Mathf.Sin 활용)
        // Time.time이 계속 증가하므로, 파도처럼 값이 위아래로 부드럽게 요동칩니다.
        float vibration = Mathf.Sin(Time.time * shiverSpeed) * shiverAmount;

        // 5. 최종 알파값 적용 (비례 알파값 + 진동값)
        // Mathf.Clamp01을 써서 혹시라도 계산된 알파값이 0 미만이거나 1을 초과하는 것을 막아줍니다.
        float finalAlpha = Mathf.Clamp(baseAlpha + vibration, 0f, maxFrostAlpha);

        // 이미지에 색상(알파값) 반영
        frostImage.color = new Color(1f, 1f, 1f, finalAlpha);
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
        if (LocalPlayerReferenceResolver.TryGetLocalCondition(out PlayerCondition localCondition))
        {
            player = localCondition;
        }
    }
}
