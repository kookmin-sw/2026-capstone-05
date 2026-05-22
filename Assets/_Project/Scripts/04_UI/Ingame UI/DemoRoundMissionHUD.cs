using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class DemoRoundMissionHUD : MonoBehaviour
{
    private const string DemoScenePath = "Assets/_Project/Scenes/00_General/SingleDemoScene_StaticMap.unity";
    private const int MissionTargetKills = 3;
    private const int CanvasSortingOrder = 30;
    private const float TextRefreshInterval = 0.1f;
    private const float TextOutlineWidth = 0.2f;
    private const string MissionHintMessage = "힌트: 기지 밖으로 나가 총을 획득해보세요.";
    private const string MissionCompleteMessage = "맵을 탐험하며 계속해서 생존하세요.";
    private const string MissionCompleteHintMessage = "제한시간 내에 기지로 복귀하세요.";

    private static DemoRoundMissionHUD instance;

    private CanvasGroup canvasGroup;
    private TextMeshProUGUI timerText;
    private TextMeshProUGUI missionText;
    private TextMeshProUGUI hintText;
    private TMP_FontAsset sceneFont;
    private int defeatedMonsterCount;
    private bool wasRoundRunning;
    private int lastRoundNumber = -1;
    private float nextTextRefreshTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        EnsureForScene(SceneManager.GetActiveScene());
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureForScene(scene);
    }

    private static void EnsureForScene(Scene scene)
    {
        if (!IsDemoScene(scene))
        {
            if (instance != null)
            {
                Destroy(instance.gameObject);
            }

            return;
        }

        if (instance != null)
        {
            return;
        }

        new GameObject(nameof(DemoRoundMissionHUD)).AddComponent<DemoRoundMissionHUD>();
    }

    private static bool IsDemoScene(Scene scene)
    {
        string path = scene.path.Replace('\\', '/');
        return path == DemoScenePath || path.EndsWith("/00_General/SingleDemoScene_StaticMap.unity");
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        sceneFont = ResolveSceneFont();
        CreateUi();
        ResetMissionProgress();
        EnemyHealth.AnyDied += HandleEnemyDied;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }

        EnemyHealth.AnyDied -= HandleEnemyDied;
    }

    private void Update()
    {
        BackendRoundManager roundManager = BackendRoundManager.Instance;
        if (roundManager == null)
        {
            SetVisible(false);
            return;
        }

        SetVisible(true);

        bool isRoundRunning = roundManager.IsRoundRunning;
        int currentRoundNumber = roundManager.CurrentRoundNumber;
        if ((isRoundRunning && !wasRoundRunning) || currentRoundNumber != lastRoundNumber)
        {
            ResetMissionProgress();
        }

        wasRoundRunning = isRoundRunning;
        lastRoundNumber = currentRoundNumber;

        if (Time.unscaledTime < nextTextRefreshTime)
        {
            return;
        }

        UpdateTimerText(roundManager);
        UpdateMissionText();
        nextTextRefreshTime = Time.unscaledTime + TextRefreshInterval;
    }

    private void HandleEnemyDied(EnemyHealth enemy, GameObject attacker)
    {
        BackendRoundManager roundManager = BackendRoundManager.Instance;
        if (roundManager == null || !roundManager.IsRoundRunning)
        {
            return;
        }

        if (defeatedMonsterCount >= MissionTargetKills || !IsLocalPlayerAttacker(attacker))
        {
            return;
        }

        defeatedMonsterCount = Mathf.Min(MissionTargetKills, defeatedMonsterCount + 1);
        UpdateMissionText();
    }

    private static bool IsLocalPlayerAttacker(GameObject attacker)
    {
        if (attacker == null)
        {
            return false;
        }

        PlayerController attackerPlayer = attacker.GetComponent<PlayerController>()
            ?? attacker.GetComponentInParent<PlayerController>()
            ?? attacker.GetComponentInChildren<PlayerController>();

        if (attackerPlayer != null)
        {
            return attackerPlayer.IsLocalPlayer;
        }

        BackendPlayerNetworkSync localNetworkSync = BackendPlayerNetworkSync.LocalInstance;
        if (localNetworkSync == null)
        {
            return false;
        }

        return attacker == localNetworkSync.gameObject
            || attacker.transform.IsChildOf(localNetworkSync.transform)
            || localNetworkSync.transform.IsChildOf(attacker.transform);
    }

    private void CreateUi()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = CanvasSortingOrder;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        timerText = CreateText("RoundTimeText", 34f, TextAlignmentOptions.Center);
        RectTransform timerRect = timerText.rectTransform;
        timerRect.anchorMin = new Vector2(0.5f, 1f);
        timerRect.anchorMax = new Vector2(0.5f, 1f);
        timerRect.pivot = new Vector2(0.5f, 1f);
        timerRect.anchoredPosition = new Vector2(0f, -26f);
        timerRect.sizeDelta = new Vector2(420f, 54f);

        missionText = CreateText("MissionText", 27f, TextAlignmentOptions.Right);
        RectTransform missionRect = missionText.rectTransform;
        missionRect.anchorMin = new Vector2(1f, 1f);
        missionRect.anchorMax = new Vector2(1f, 1f);
        missionRect.pivot = new Vector2(1f, 1f);
        missionRect.anchoredPosition = new Vector2(-34f, -32f);
        missionRect.sizeDelta = new Vector2(640f, 48f);

        hintText = CreateText("MissionHintText", 22f, TextAlignmentOptions.Right);
        RectTransform hintRect = hintText.rectTransform;
        hintRect.anchorMin = new Vector2(1f, 1f);
        hintRect.anchorMax = new Vector2(1f, 1f);
        hintRect.pivot = new Vector2(1f, 1f);
        hintRect.anchoredPosition = new Vector2(-34f, -76f);
        hintRect.sizeDelta = new Vector2(720f, 40f);
        hintText.text = MissionHintMessage;
    }

    private TextMeshProUGUI CreateText(string objectName, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject textObject = new(objectName);
        textObject.transform.SetParent(transform, false);

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        if (sceneFont != null)
        {
            text.font = sceneFont;
        }

        text.alignment = alignment;
        text.color = Color.white;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.fontWeight = FontWeight.Black;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        text.outlineColor = Color.black;
        text.outlineWidth = TextOutlineWidth;

        UnityEngine.UI.Outline outline = textObject.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.75f);
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = true;

        Shadow shadow = textObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
        shadow.effectDistance = new Vector2(2f, -2f);
        shadow.useGraphicAlpha = true;

        return text;
    }

    private void UpdateTimerText(BackendRoundManager roundManager)
    {
        float remainingSeconds = roundManager.IsRoundRunning ? roundManager.RoundTimeRemainingSeconds : 0f;
        int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(remainingSeconds));
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        timerText.text = $"{minutes:00}:{seconds:00}";
    }

    private void UpdateMissionText()
    {
        if (defeatedMonsterCount >= MissionTargetKills)
        {
            missionText.text = MissionCompleteMessage;
            if (hintText != null)
            {
                hintText.text = MissionCompleteHintMessage;
            }

            return;
        }

        missionText.text = $"미션: 몬스터 3마리 처치하기({defeatedMonsterCount}/{MissionTargetKills})";
        if (hintText != null)
        {
            hintText.text = MissionHintMessage;
        }
    }

    private void ResetMissionProgress()
    {
        defeatedMonsterCount = 0;
        UpdateMissionText();
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
        }
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
