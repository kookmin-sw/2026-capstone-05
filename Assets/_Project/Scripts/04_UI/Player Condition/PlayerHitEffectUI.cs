using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class PlayerHitEffectUI : MonoBehaviour
{
    [Header("Player Data Source")]
    [SerializeField] private PlayerCondition player;

    [Header("Hit Splatters (Assign 3 Images)")]
    [Tooltip("직접 그리신 3개의 핏자국 이미지를 배열에 넣어주세요")]
    public Image[] bloodSplatters; 

    [Header("Screen Border Effect (이미지 넣지 마세요!)")]
    [Tooltip("Source Image를 비워두면 코드가 알아서 붉은 테두리를 그립니다.")]
    public Image bloodBorder; 

    [Header("Effect Settings")]
    public float fadeDuration = 0.8f;       
    public float maxBorderAlpha = 0.6f;     
    public float splatterScale = 1.2f;      

    private float lastHealth;

    private void Start()
    {
        if (player != null)
        {
            lastHealth = player.health.currentValue;
        }

        // 🌟 핵심: 테두리 이미지가 비어있다면, 스크립트가 직접 텍스처를 생성합니다.
        if (bloodBorder != null && bloodBorder.sprite == null)
        {
            bloodBorder.sprite = GenerateBorderSprite();
        }

        InitializeUI();
    }

    private void InitializeUI()
    {
        foreach (var splatter in bloodSplatters)
        {
            if (splatter != null) splatter.color = new Color(1, 1, 1, 0);
        }
        if (bloodBorder != null) bloodBorder.color = new Color(1, 0, 0, 0);
    }

    private void Update()
    {
        if (player == null) return;

        float currentHealth = player.health.currentValue;

        // 체력 감소 감지 (피격 판정)
        if (currentHealth < lastHealth)
        {
            PlayHitEffect();
        }

        lastHealth = currentHealth;
    }

    public void PlayHitEffect()
    {
        // [1] 코드로 직접 생성한 화면 테두리 붉은색 번쩍임
        if (bloodBorder != null)
        {
            bloodBorder.DOKill();
            bloodBorder.color = new Color(1, 0, 0, maxBorderAlpha); // 빨간색 켜기
            bloodBorder.DOFade(0, fadeDuration).SetEase(Ease.OutQuad);
        }

        // [2] 3개 중 1개 랜덤 핏자국 노출
        if (bloodSplatters.Length > 0)
        {
            foreach (var splatter in bloodSplatters) { splatter.DOKill(); splatter.color = new Color(1,1,1,0); }

            int randomIndex = Random.Range(0, bloodSplatters.Length);
            Image selected = bloodSplatters[randomIndex];

            if (selected != null)
            {
                selected.transform.localScale = Vector3.one * 0.8f;
                selected.color = new Color(1, 1, 1, 1);
                
                selected.transform.DOScale(splatterScale, 0.15f).SetEase(Ease.OutBack);
                selected.DOFade(0, fadeDuration).SetEase(Ease.InExpo).SetDelay(0.1f);
            }
        }
    }


    //  이미지 파일 없이 코드로 테두리(Vignette)를 그리는 함수입니다. 해상도는 256x256으로 고정되어 있지만, 화면에 늘려서 쓰기 때문에 충분합니다.

    private Sprite GenerateBorderSprite()
    {
        int size = 256; // 텍스처 해상도 (어차피 화면에 늘려서 쓰므로 256이면 충분하고 최적화에 좋습니다)
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // 중심으로부터의 거리를 계산
                float distance = Vector2.Distance(center, new Vector2(x, y));
                
                // 수학을 이용한 그라데이션: 중앙은 투명(0)하게, 바깥쪽 가장자리일수록 진하게(1)
                // 3제곱(Pow)을 줘서 시야 중앙은 뻥 뚫리고 모서리에만 피가 맺히도록 만듭니다.
                float alpha = Mathf.Pow(distance / radius, 3f);
                alpha = Mathf.Clamp01(alpha); 

                // 픽셀에 계산된 색상(빨강 + 알파값) 칠하기
                tex.SetPixel(x, y, new Color(1f, 0f, 0f, alpha));
            }
        }
        tex.Apply(); // 텍스처 변경사항 메모리에 적용

        // 유니티 UI에서 사용할 수 있도록 Sprite 객체로 변환하여 반환
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}