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
    private const string QuestOpenBunkerMessage = "퀘스트 1. 책상과 상호작용 완료 후 벙커 문을 여세요.";
    private const string QuestAcquireFirearmMessage = "퀘스트 2. 몬스터를 처치하기 위해 총과 총알을 획득하세요.";
    private const string QuestReturnToBaseMessage = "퀘스트 4. 제한 시간 내에 탐험을 마치고 기지로 복귀하세요.";
    private const string QuestCompletedMessage = "퀘스트 완료!";
    private const string QuestFailedMessage = "퀘스트 실패!";

    private static DemoRoundMissionHUD instance;

    private enum QuestStep
    {
        LeaveBunker,
        AcquireFirearmAndAmmo,
        KillMonsters,
        ReturnToBase,
        Completed,
        Failed
    }

    private CanvasGroup canvasGroup;
    private TextMeshProUGUI timerText;
    private TextMeshProUGUI missionText;
    private TMP_FontAsset sceneFont;
    private int defeatedMonsterCount;
    private QuestStep questStep;
    private bool wasRoundRunning;
    private int lastRoundNumber = -1;
    private float nextTextRefreshTime;
    private bool hasReportedQuest3Completed;

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
        if (wasRoundRunning && !isRoundRunning)
        {
            FailQuestIfIncomplete();
        }

        if (isRoundRunning && (!wasRoundRunning || currentRoundNumber != lastRoundNumber))
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
        nextTextRefreshTime = Time.unscaledTime + TextRefreshInterval;
    }

    private void HandleEnemyDied(EnemyHealth enemy, GameObject attacker)
    {
        BackendRoundManager roundManager = BackendRoundManager.Instance;
        if (roundManager == null || !roundManager.IsRoundRunning)
        {
            return;
        }

        UpdateQuestProgress();
        if (questStep != QuestStep.KillMonsters ||
            defeatedMonsterCount >= MissionTargetKills ||
            !IsLocalPlayerAttacker(attacker))
        {
            return;
        }

        defeatedMonsterCount = Mathf.Min(MissionTargetKills, defeatedMonsterCount + 1);
        UpdateQuestProgress();
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
        missionRect.sizeDelta = new Vector2(960f, 48f);
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
        missionText.text = questStep switch
        {
            QuestStep.AcquireFirearmAndAmmo => QuestAcquireFirearmMessage,
            QuestStep.KillMonsters => BuildKillMonsterQuestMessage(),
            QuestStep.ReturnToBase => QuestReturnToBaseMessage,
            QuestStep.Completed => QuestCompletedMessage,
            QuestStep.Failed => QuestFailedMessage,
            _ => QuestOpenBunkerMessage
        };
    }

    private void UpdateQuestProgress()
    {
        if (questStep == QuestStep.LeaveBunker && HasAnyPlayerOutsideBunker())
        {
            questStep = QuestStep.AcquireFirearmAndAmmo;
        }

        if (questStep == QuestStep.AcquireFirearmAndAmmo && LocalPlayerHasFirearmAndAmmo())
        {
            questStep = QuestStep.KillMonsters;
        }

        if (questStep == QuestStep.KillMonsters && defeatedMonsterCount >= MissionTargetKills)
        {
            ReportQuest3Completed();
            questStep = QuestStep.ReturnToBase;
        }

        if (questStep == QuestStep.ReturnToBase && IsLocalPlayerInBunker())
        {
            CompleteQuest();
        }
    }

    private string BuildKillMonsterQuestMessage()
    {
        return $"퀘스트 3. 제한 시간 내에 몬스터를 처치하세요. ({defeatedMonsterCount}/{MissionTargetKills})";
    }

    private static bool HasAnyPlayerOutsideBunker()
    {
        PlayerCondition[] playerConditions = FindObjectsByType<PlayerCondition>(FindObjectsSortMode.None);
        foreach (PlayerCondition condition in playerConditions)
        {
            if (condition != null && !condition.IsInBunker)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsLocalPlayerInBunker()
    {
        return LocalPlayerReferenceResolver.TryGetLocalCondition(out PlayerCondition condition) &&
            condition != null &&
            condition.IsInBunker;
    }

    private static bool LocalPlayerHasFirearmAndAmmo()
    {
        if (QuickslotHasFirearmWithAmmo())
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
            if (HasRequiredAmmo(IsFirearm(model.Get(i))))
            {
                return true;
            }
        }

        return false;
    }

    private static bool QuickslotHasFirearmWithAmmo()
    {
        if (QuickslotUIController.Instance == null)
        {
            return false;
        }

        for (int i = 0; i < QuickslotCount; i++)
        {
            if (HasRequiredAmmo(IsFirearm(QuickslotUIController.Instance.GetItem(i))))
            {
                return true;
            }
        }

        return false;
    }

    private static FirearmItemData IsFirearm(ItemInstance item)
    {
        return item?.Data as FirearmItemData;
    }

    private static bool HasRequiredAmmo(FirearmItemData firearm)
    {
        return firearm != null &&
            firearm.requiredAmmoType != null &&
            PlayerItemInventoryQuery.HasItem(firearm.requiredAmmoType);
    }

    private void ResetMissionProgress()
    {
        defeatedMonsterCount = 0;
        hasReportedQuest3Completed = false;
        questStep = QuestStep.LeaveBunker;
        UpdateMissionText();
    }

    private void ReportQuest3Completed()
    {
        if (hasReportedQuest3Completed)
        {
            return;
        }

        hasReportedQuest3Completed = true;
        BackendRoundManager.Instance?.MarkLocalPlayerQuest3Completed();
    }

    private void CompleteQuest()
    {
        if (questStep == QuestStep.Completed)
        {
            return;
        }

        questStep = QuestStep.Completed;
        UpdateMissionText();
    }

    private void FailQuestIfIncomplete()
    {
        if (questStep == QuestStep.Completed)
        {
            return;
        }

        questStep = QuestStep.Failed;
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
