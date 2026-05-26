using Systems.GridInventory;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class AmmoCountUI : MonoBehaviour
{
    private const int CanvasSortingOrder = 25;
    private const float RefreshInterval = 0.1f;
    private const float TextOutlineWidth = 0.18f;

    private static readonly Color NormalColor = Color.white;
    private static readonly Color EmptyColor = new(1f, 0.32f, 0.32f);

    private static AmmoCountUI instance;

    private CanvasGroup canvasGroup;
    private TextMeshProUGUI ammoText;
    private TMP_FontAsset sceneFont;
    private float nextRefreshTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        EnsureInstance();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureInstance();
    }

    private static void EnsureInstance()
    {
        if (instance != null)
        {
            return;
        }

        new GameObject(nameof(AmmoCountUI)).AddComponent<AmmoCountUI>();
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
        SetVisible(false);
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefreshTime)
        {
            return;
        }

        RefreshAmmoText();
        nextRefreshTime = Time.unscaledTime + RefreshInterval;
    }

    private void RefreshAmmoText()
    {
        if (!TryGetEquippedFirearm(out FirearmItemData firearm) || firearm.requiredAmmoType == null)
        {
            SetVisible(false);
            return;
        }

        int ammoCount = PlayerItemInventoryQuery.CountItem(firearm.requiredAmmoType);
        ammoText.text = $"총알 {ammoCount}";
        ammoText.color = ammoCount > 0 ? NormalColor : EmptyColor;
        SetVisible(true);
    }

    private static bool TryGetEquippedFirearm(out FirearmItemData firearm)
    {
        firearm = null;

        if (!LocalPlayerReferenceResolver.TryGetLocalPlayer(out PlayerController player) ||
            player == null ||
            player.Equipment == null)
        {
            return false;
        }

        firearm = player.Equipment.CurrentItemInstance?.Data as FirearmItemData;
        return firearm != null;
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

        GameObject textObject = new("AmmoCountText");
        textObject.transform.SetParent(transform, false);

        ammoText = textObject.AddComponent<TextMeshProUGUI>();
        if (sceneFont != null)
        {
            ammoText.font = sceneFont;
        }

        ammoText.alignment = TextAlignmentOptions.Right;
        ammoText.color = NormalColor;
        ammoText.fontSize = 34f;
        ammoText.fontStyle = FontStyles.Bold;
        ammoText.fontWeight = FontWeight.Black;
        ammoText.textWrappingMode = TextWrappingModes.NoWrap;
        ammoText.overflowMode = TextOverflowModes.Overflow;
        ammoText.raycastTarget = false;
        ammoText.outlineColor = Color.black;
        ammoText.outlineWidth = TextOutlineWidth;

        UnityEngine.UI.Outline outline = textObject.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.75f);
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = true;

        Shadow shadow = textObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
        shadow.effectDistance = new Vector2(2f, -2f);
        shadow.useGraphicAlpha = true;

        RectTransform rect = ammoText.rectTransform;
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-44f, 42f);
        rect.sizeDelta = new Vector2(320f, 56f);
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
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts/NEXON Lv2 Gothic OTF Bold SDF");

        if (font == null)
        {
            Debug.LogWarning("[AmmoCountUI] Font not found at Resources/Fonts/NEXON Lv2 Gothic OTF Bold SDF.");
        }

        return font;
    }
}
