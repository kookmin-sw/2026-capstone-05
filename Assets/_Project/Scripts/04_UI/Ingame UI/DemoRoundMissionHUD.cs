using TMPro;
using Systems.GridInventory;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class DemoRoundMissionHUD : MonoBehaviour
{
    private const string DemoScenePath = "Assets/_Project/Scenes/00_General/SingleDemoScene_StaticMap.unity";
    private const int MissionTargetKills = 3;
    private const int QuickslotCount = 4;
    private const int CanvasSortingOrder = -1;
    private const float TextRefreshInterval = 0.1f;
    private const float TextOutlineWidth = 0.2f;
    private const string MissionCompleteMessage = "맵을 탐험하며 계속해서 생존하세요.";
    private const string QuestLeaveBunkerMessage = "퀘스트 1: 벙커 밖으로 나가기";
    private const string QuestAcquireFirearmMessage = "퀘스트 2: 총 획득하기";
    private const string QuestKillMonstersMessage = "퀘스트 3: 몬스터를 3마리 이상 처치하세요";
    private const string QuestReturnToBaseMessage = "퀘스트 4: 제한 시간 내에 기지로 복귀해 물품을 보관하세요";

    private static DemoRoundMissionHUD instance;

    private enum QuestStep
    {
        LeaveBunker,
        AcquireFirearm,
        KillMonsters,
        ReturnToBase
    }

    private CanvasGroup canvasGroup;
    private TextMeshProUGUI timerText;
    private TextMeshProUGUI missionText;
    private TextMeshProUGUI hintText;
    private TMP_FontAsset sceneFont;
    private int defeatedMonsterCount;
    private QuestStep questStep;
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
        UpdateQuestProgress();
        UpdateMissionText();
        UpdateQuestHintText();
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
        UpdateQuestProgress();
        UpdateMissionText();
        UpdateQuestHintText();
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
        hintText.text = QuestLeaveBunkerMessage;
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
            return;
        }

        missionText.text = $"미션: 몬스터 3마리 처치하기({defeatedMonsterCount}/{MissionTargetKills})";
    }

    private void UpdateQuestProgress()
    {
        if (questStep == QuestStep.LeaveBunker && IsLocalPlayerOutsideBunker())
        {
            questStep = QuestStep.AcquireFirearm;
        }

        if (questStep == QuestStep.AcquireFirearm && LocalPlayerHasFirearm())
        {
            questStep = QuestStep.KillMonsters;
        }

        if (questStep == QuestStep.KillMonsters && defeatedMonsterCount >= MissionTargetKills)
        {
            questStep = QuestStep.ReturnToBase;
        }
    }

    private void UpdateQuestHintText()
    {
        if (hintText == null)
        {
            return;
        }

        hintText.text = questStep switch
        {
            QuestStep.AcquireFirearm => QuestAcquireFirearmMessage,
            QuestStep.KillMonsters => QuestKillMonstersMessage,
            QuestStep.ReturnToBase => QuestReturnToBaseMessage,
            _ => QuestLeaveBunkerMessage
        };
    }

    private static bool IsLocalPlayerOutsideBunker()
    {
        return LocalPlayerReferenceResolver.TryGetLocalCondition(out PlayerCondition condition)
            && condition != null
            && !condition.IsInBunker;
    }

    private static bool LocalPlayerHasFirearm()
    {
        if (QuickslotHasFirearm())
        {
            return true;
        }

        GridInventoryModel model = GridInventory.Instance?.Controller?.Model;
        if (model == null)
        {
            return false;
        }

        for (int i = 0; i < model.Items.Length; i++)
        {
            if (IsFirearm(model.Get(i)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool QuickslotHasFirearm()
    {
        if (QuickslotUIController.Instance == null)
        {
            return false;
        }

        for (int i = 0; i < QuickslotCount; i++)
        {
            if (IsFirearm(QuickslotUIController.Instance.GetItem(i)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsFirearm(ItemInstance item)
    {
        return item?.Data is FirearmItemData;
    }

    private void ResetMissionProgress()
    {
        defeatedMonsterCount = 0;
        questStep = QuestStep.LeaveBunker;
        UpdateMissionText();
        UpdateQuestHintText();
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
