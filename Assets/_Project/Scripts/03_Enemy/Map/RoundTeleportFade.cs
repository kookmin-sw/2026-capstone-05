using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class RoundTeleportFade : MonoBehaviour
{
    private const int SortingOrder = 10000;

    private static RoundTeleportFade instance;

    private CanvasGroup canvasGroup;
    private Coroutine fadeRoutine;

    public static void Play(float fadeInSeconds, float holdSeconds, float fadeOutSeconds)
    {
        EnsureInstance().PlayInternal(fadeInSeconds, holdSeconds, fadeOutSeconds);
    }

    private static RoundTeleportFade EnsureInstance()
    {
        if (instance != null)
        {
            return instance;
        }

        GameObject fadeObject = new("RoundTeleportFade");
        DontDestroyOnLoad(fadeObject);
        instance = fadeObject.AddComponent<RoundTeleportFade>();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        CreateUi();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void CreateUi()
    {
        if (canvasGroup != null)
        {
            return;
        }

        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        GameObject imageObject = new("FadeImage");
        imageObject.transform.SetParent(transform, false);

        Image image = imageObject.AddComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;

        RectTransform rectTransform = image.rectTransform;
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private void PlayInternal(float fadeInSeconds, float holdSeconds, float fadeOutSeconds)
    {
        CreateUi();

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }

        fadeRoutine = StartCoroutine(FadeRoutine(
            Mathf.Max(0f, fadeInSeconds),
            Mathf.Max(0f, holdSeconds),
            Mathf.Max(0f, fadeOutSeconds)));
    }

    private IEnumerator FadeRoutine(float fadeInSeconds, float holdSeconds, float fadeOutSeconds)
    {
        canvasGroup.blocksRaycasts = true;
        yield return FadeTo(1f, fadeInSeconds);

        if (holdSeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(holdSeconds);
        }

        yield return FadeTo(0f, fadeOutSeconds);

        canvasGroup.blocksRaycasts = false;
        fadeRoutine = null;
    }

    private IEnumerator FadeTo(float targetAlpha, float duration)
    {
        float startAlpha = canvasGroup.alpha;

        if (duration <= 0f)
        {
            canvasGroup.alpha = targetAlpha;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, progress);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
    }
}
