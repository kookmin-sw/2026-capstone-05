using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class FrostShimmer : MonoBehaviour
{
    [Header("얼음 레이어들")]
    // Inspector에서 겹쳐둔 얼음 이미지들을 넣을 배열입니다.
    public Image[] frostLayers; 

    [Header("일렁임 세팅")]
    [Tooltip("가장 옅어질 때의 투명도 (0.0 ~ 1.0)")]
    public float minAlpha = 0.2f; 
    
    [Tooltip("가장 짙어질 때의 투명도 (0.0 ~ 1.0)")]
    public float maxAlpha = 0.8f; 
    
    [Tooltip("깜빡이는 데 걸리는 최소 시간")]
    public float minDuration = 0.5f; 
    
    [Tooltip("깜빡이는 데 걸리는 최대 시간")]
    public float maxDuration = 1.5f; 

    void Start()
    {
        // 게임이 시작되면 배열에 있는 모든 얼음 레이어에 애니메이션을 시작합니다.
        foreach (Image layer in frostLayers)
        {
            // 각 레이어의 초기 투명도를 0으로 설정
            layer.color = new Color(layer.color.r, layer.color.g, layer.color.b, 0f);
            
            // 일렁임 시작!
            AnimateLayer(layer);
        }
    }

    // 🌟 핵심 함수: 스스로를 무한히 호출하며 투명도를 바꿈
    void AnimateLayer(Image img)
    {
        // 1. 랜덤한 목표 투명도(Alpha)와 변환에 걸릴 시간(Duration)을 뽑아냅니다.
        float targetAlpha = Random.Range(minAlpha, maxAlpha);
        float duration = Random.Range(minDuration, maxDuration);

        // 2. DOTween을 이용해 해당 투명도까지 부드럽게 변환합니다.
        img.DOFade(targetAlpha, duration)
            .SetEase(Ease.InOutSine) // InOutSine은 빛이 부드럽게 켜졌다 꺼지는 느낌을 줍니다.
            .OnComplete(() => 
            {
                // 3. 목표 투명도에 도달(완료)하면, 다시 이 함수를 불러옵니다. (무한 반복)
                AnimateLayer(img);
            });
    }

    void OnDestroy()
    {
        // 오브젝트가 사라질 때 진행 중인 애니메이션들을 깔끔하게 꺼줍니다.
        foreach (Image layer in frostLayers)
        {
            layer.DOKill();
        }
    }
}