using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class TitleButtonUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private RectTransform rectTransform;
    private Text buttonText; 
    private Vector3 originalScale;

    void Awake() 
    {
        rectTransform = GetComponent<RectTransform>();
        buttonText = GetComponentInChildren<Text>(); 
        originalScale = rectTransform.localScale;
    }

    // 마우스를 올렸을 때
    public void OnPointerEnter(PointerEventData eventData)
    {
        // 1.1f(10% 확대) -> 1.05f(5% 확대)로 변경하여 과하지 않게 조절
        rectTransform.DOScale(originalScale * 1.05f, 0.2f).SetEase(Ease.OutBack);
        
        if (buttonText != null) 
            buttonText.DOColor(new Color(0.6f, 0.8f, 1f), 0.2f); // 텍스트 푸른빛
    }

    // 마우스를 뗐을 때
    public void OnPointerExit(PointerEventData eventData)
    {
        rectTransform.DOScale(originalScale, 0.2f).SetEase(Ease.OutQuad);
        
        if (buttonText != null) 
            buttonText.DOColor(Color.white, 0.2f); // 텍스트 원래 색상으로
    }
}