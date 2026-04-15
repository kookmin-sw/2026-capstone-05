using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class AuthButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private RectTransform targetRect;
    [SerializeField] private TMP_Text targetTmpText;
    [SerializeField] private Text targetLegacyText;
    [SerializeField] private Image targetImage;
    [SerializeField] private float hoverScaleMultiplier = 1.05f;
    [SerializeField] private float hoverDuration = 0.2f;

    private Vector3 _originScale;
    private Color _originTmpColor;
    private Color _originLegacyColor;
    private Color _originImageColor;
    private bool _initialized;

    private void Awake()
    {
        targetRect ??= GetComponent<RectTransform>();
        targetTmpText ??= GetComponentInChildren<TMP_Text>(true);
        targetLegacyText ??= GetComponentInChildren<Text>(true);
        targetImage ??= GetComponent<Image>();

        if (targetRect == null)
            return;

        _originScale = targetRect.localScale;
        _originTmpColor = targetTmpText != null ? targetTmpText.color : Color.white;
        _originLegacyColor = targetLegacyText != null ? targetLegacyText.color : Color.white;
        _originImageColor = targetImage != null ? targetImage.color : Color.white;
        _initialized = true;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!_initialized)
            return;

        targetRect.DOScale(_originScale * hoverScaleMultiplier, hoverDuration).SetEase(Ease.OutBack);

        if (targetTmpText != null)
            targetTmpText.DOColor(new Color(0.6f, 0.8f, 1f), hoverDuration);

        if (targetLegacyText != null)
            targetLegacyText.DOColor(new Color(0.6f, 0.8f, 1f), hoverDuration);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!_initialized)
            return;

        targetRect.DOScale(_originScale, hoverDuration).SetEase(Ease.OutQuad);

        if (targetTmpText != null)
            targetTmpText.DOColor(_originTmpColor, hoverDuration);

        if (targetLegacyText != null)
            targetLegacyText.DOColor(_originLegacyColor, hoverDuration);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!_initialized || targetImage == null)
            return;

        targetImage.DOKill();
        targetImage.color = _originImageColor;
        targetImage.DOColor(_originImageColor * 0.9f, 0.05f)
            .OnComplete(() => targetImage.DOColor(_originImageColor, 0.1f));
    }

    private void OnDestroy()
    {
        if (targetRect != null)
            targetRect.DOKill();

        if (targetTmpText != null)
            targetTmpText.DOKill();

        if (targetLegacyText != null)
            targetLegacyText.DOKill();

        if (targetImage != null)
            targetImage.DOKill();
    }
}
