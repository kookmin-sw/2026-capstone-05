using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoundTeleportFade : MonoBehaviour
{
    private const int SortingOrder = 10000;

    private static RoundTeleportFade instance;

    private CanvasGroup canvasGroup;
    private TextMeshProUGUI messageText;
    private Coroutine fadeRoutine;

    public static void Play(float fadeInSeconds, float holdSeconds, float fadeOutSeconds)
    {
        Play(fadeInSeconds, holdSeconds, fadeOutSeconds, string.Empty);
    }

    public static void Play(float fadeInSeconds, float holdSeconds, float fadeOutSeconds, string message)
    {
        EnsureInstance().PlayInternal(fadeInSeconds, holdSeconds, fadeOutSeconds, message);
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

        GameObject textObject = new("FadeMessageText");
        textObject.transform.SetParent(transform, false);

        messageText = textObject.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset sceneFont = ResolveSceneFont();
        if (sceneFont != null)
        {
            messageText.font = sceneFont;
        }

        messageText.alignment = TextAlignmentOptions.Center;
        messageText.color = Color.white;
        messageText.fontSize = 42f;
        messageText.fontStyle = FontStyles.Bold;
        messageText.fontWeight = FontWeight.Black;
        messageText.enableWordWrapping = false;
        messageText.overflowMode = TextOverflowModes.Overflow;
        messageText.outlineColor = Color.black;
        messageText.outlineWidth = 0.2f;
        messageText.raycastTarget = false;
        messageText.text = string.Empty;

        RectTransform textRect = messageText.rectTransform;
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = new Vector2(1200f, 120f);
    }

    private void PlayInternal(float fadeInSeconds, float holdSeconds, float fadeOutSeconds, string message)
    {
        CreateUi();

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }

        if (messageText != null)
        {
            TMP_FontAsset sceneFont = ResolveSceneFont();
            if (sceneFont != null)
            {
                messageText.font = sceneFont;
            }

            messageText.text = message ?? string.Empty;
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
        if (messageText != null)
        {
            messageText.text = string.Empty;
        }

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

    private static TMP_FontAsset ResolveSceneFont()
    {
        TMP_Text[] texts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        TMP_FontAsset fallback = null;

        foreach (TMP_Text text in texts)
        {
            if (text == null || text.font == null)
            {
                continue;
            }

            if (fallback == null)
            {
                fallback = text.font;
            }

            if (text.font.name.Contains("NEXON"))
            {
                return text.font;
            }
        }

        return fallback;
    }
}
