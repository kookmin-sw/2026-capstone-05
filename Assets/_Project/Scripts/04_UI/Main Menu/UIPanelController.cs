using UnityEngine;
using DG.Tweening;

public class UIPanelController : MonoBehaviour
{
    [Header("Animation Speed")]
    public float openDuration = 0.3f;
    public float closeDuration = 0.25f;

    private RectTransform rectTransform;
    private Sequence activeSequence;

    private void Awake()
    {
        EnsureRectTransform();
    }

    public void OpenPanel()
    {
        KillActiveSequence();

        gameObject.SetActive(true);
        EnsureRectTransform();
        rectTransform.localScale = new Vector3(0f, 1f, 1f);

        Sequence seq = DOTween.Sequence().SetTarget(rectTransform);
        activeSequence = seq;
        seq.SetDelay(closeDuration);
        seq.Append(rectTransform.DOScaleX(1.05f, openDuration * 0.7f).SetEase(Ease.OutExpo));
        seq.Append(rectTransform.DOScaleX(1f, openDuration * 0.3f).SetEase(Ease.InOutSine));
        seq.OnKill(() =>
        {
            if (activeSequence == seq)
                activeSequence = null;
        });
    }

    public void ClosePanel()
    {
        KillActiveSequence();
        EnsureRectTransform();

        Sequence seq = DOTween.Sequence().SetTarget(rectTransform);
        activeSequence = seq;
        seq.Append(rectTransform.DOScaleX(1.05f, closeDuration * 0.3f).SetEase(Ease.OutQuad));
        seq.Append(rectTransform.DOScaleX(0f, closeDuration * 0.7f).SetEase(Ease.InExpo));
        seq.OnComplete(() =>
        {
            gameObject.SetActive(false);
        });
        seq.OnKill(() =>
        {
            if (activeSequence == seq)
                activeSequence = null;
        });
    }

    public void ClosePanelImmediate()
    {
        KillActiveSequence();
        EnsureRectTransform();
        rectTransform.localScale = new Vector3(0f, 1f, 1f);
        gameObject.SetActive(false);
    }

    public void SetPanelActiveImmediate(bool isActive)
    {
        KillActiveSequence();
        EnsureRectTransform();
        rectTransform.localScale = isActive ? Vector3.one : new Vector3(0f, 1f, 1f);
        gameObject.SetActive(isActive);
    }

    private void EnsureRectTransform()
    {
        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();
    }

    private void KillActiveSequence()
    {
        if (activeSequence != null && activeSequence.IsActive())
        {
            activeSequence.Kill();
            activeSequence = null;
        }

        EnsureRectTransform();
        if (rectTransform != null)
            rectTransform.DOKill();
    }
}
