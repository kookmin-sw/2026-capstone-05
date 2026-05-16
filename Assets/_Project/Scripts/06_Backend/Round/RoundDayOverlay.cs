using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoundDayOverlay : MonoBehaviour
{
    private const float DisplaySeconds = 2f;

    private static RoundDayOverlay instance;

    private CanvasGroup canvasGroup;
    private TextMeshProUGUI dayText;
    private Coroutine displayRoutine;

    public static void Show(int day)
    {
        EnsureInstance().ShowInternal(day);
    }

    private static RoundDayOverlay EnsureInstance()
    {
        if (instance != null)
        {
            return instance;
        }

        GameObject overlayObject = new("RoundDayOverlay");
        DontDestroyOnLoad(overlayObject);
        RoundDayOverlay overlay = overlayObject.AddComponent<RoundDayOverlay>();
        instance = overlay;
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
        if (canvasGroup == null)
        {
            CreateUi();
        }
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
        if (canvasGroup != null && dayText != null)
        {
            return;
        }

        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = gameObject.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        if (GetComponent<GraphicRaycaster>() == null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
        }

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        Transform textTransform = transform.Find("DayText");
        GameObject textObject = textTransform != null ? textTransform.gameObject : new GameObject("DayText");
        textObject.transform.SetParent(transform, false);

        dayText = textObject.GetComponent<TextMeshProUGUI>();
        if (dayText == null)
        {
            dayText = textObject.AddComponent<TextMeshProUGUI>();
        }

        dayText.alignment = TextAlignmentOptions.Center;
        dayText.color = Color.white;
        dayText.fontSize = 92f;
        dayText.fontStyle = FontStyles.Bold;
        dayText.raycastTarget = false;

        RectTransform rect = dayText.rectTransform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void ShowInternal(int day)
    {
        if (dayText == null || canvasGroup == null)
        {
            CreateUi();
        }

        dayText.text = $"Day {Mathf.Max(1, day)}";

        if (displayRoutine != null)
        {
            StopCoroutine(displayRoutine);
        }

        displayRoutine = StartCoroutine(DisplayRoutine());
    }

    private IEnumerator DisplayRoutine()
    {
        canvasGroup.alpha = 1f;

        float fadeStartTime = 1.6f;
        float elapsed = 0f;

        while (elapsed < DisplaySeconds)
        {
            elapsed += Time.unscaledDeltaTime;

            if (elapsed > fadeStartTime)
            {
                float fadeProgress = Mathf.InverseLerp(fadeStartTime, DisplaySeconds, elapsed);
                canvasGroup.alpha = Mathf.Lerp(1f, 0f, fadeProgress);
            }

            yield return null;
        }

        canvasGroup.alpha = 0f;
        displayRoutine = null;
    }
}
