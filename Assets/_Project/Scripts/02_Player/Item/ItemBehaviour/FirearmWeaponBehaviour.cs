using FMODUnity;
using Systems.GridInventory;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FirearmWeaponBehaviour : EquippedItemBehaviour
{
    [SerializeField] private Transform muzzlePoint; // 총구 위치

    public override bool Use()
    {
        FirearmItemData data = itemInstance.Data as FirearmItemData;
        if (data == null)
        {
            return false;
        }

        // 총알 확인
        if (GridInventory.Instance != null)
        {
            if (!GridInventory.Instance.HasItem(data.requiredAmmoType))
            {
                Debug.Log("No ammo to shoot!");
                RuntimeManager.PlayOneShot(data.dryFireSound, transform.position);
                NoiseManager.Instance.GenerateNoise(transform.position, data.dryFireNoiseType);
                CenterScreenFeedbackUI.Show("총알이 없습니다!", new Color(1f, 0.35f, 0.35f));
                return false;
            }
        }

        if (!base.Use())
        {
            return false;
        }

        Shoot(data);

        player.AddCameraRecoil(data.recoilForce, new Vector3(0, 0, -0.05f), new Vector3(-5f, 0, 0));

        return true;
    }

    private void Shoot(FirearmItemData data)
    {
        // 총알 감소 로직
        if (GridInventory.Instance != null)
        {
            GridInventory.Instance.ConsumeItem(data.requiredAmmoType);
        }

        //if (data.muzzleFlashPrefab != null && muzzlePoint != null)
        //{
        //    Instantiate(data.muzzleFlashPrefab, muzzlePoint.position, muzzlePoint.rotation, muzzlePoint);
        //}

        Transform originTransform = player.CameraTransform != null ? player.CameraTransform : Camera.main.transform;
        Ray ray = new Ray(originTransform.position, originTransform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, data.attackRange, player.Equipment.hitLayerMask))
        {
            Debug.Log($"Hit: {hit.collider.name}");
            DamageInfo damageInfo = new DamageInfo
            {
                damageAmount = data.damage,
                hitPoint = hit.point,
                hitNormal = hit.normal,
                attacker = player.gameObject
            };

            if (hit.collider.GetComponentInParent<EnemyHealth>() is EnemyHealth enemyHealth)
            {
                enemyHealth.RequestDamage(damageInfo);
            }
            else if (hit.collider.GetComponentInParent<IDamageable>() is IDamageable target)
            {
                target.TakeDamage(damageInfo);
            }

            // TODO: 피격 이펙트(피, 불꽃, 흙먼지 등) 스폰
        }

        NoiseManager.Instance.GenerateNoise(transform.position, data.shootNoiseType);
        RuntimeManager.PlayOneShot(data.shootSound, transform.position);
    }

    /// <summary>
    /// R키 등을 눌렀을 때 PlayerEquipment 등에서 호출
    /// </summary>
    public void Reload()
    {
        // 이미 꽉 찼거나 인벤토리에 여유 탄약이 없으면 Return하는 로직 필요
        //player.Animator.PlayUseItemAnimation(ItemUseAnimationType.Reload); // 장전 애니메이션
    }

    /// <summary>
    /// 장전 애니메이션 도중 탄창이 결합되는(찰칵) 프레임에서 호출됨
    /// </summary>
    public override void OnAnimationEventTriggered()
    {
        FirearmItemData data = itemInstance.Data as FirearmItemData;
        if (data == null)
        {
            return;
        }

        // 탄약 보충 로직 (인벤토리에서 총알을 빼서 currentAmmo에 넣기)
        // itemInstance.currentAmmo = data.maxAmmo; 

        Debug.Log("Reload Complete!");
    }
}

public sealed class CenterScreenFeedbackUI : MonoBehaviour
{
    private const int CanvasSortingOrder = 200;
    private const float FadeOutSeconds = 0.25f;
    private const float OutlineWidth = 0.18f;

    private static CenterScreenFeedbackUI instance;

    private CanvasGroup canvasGroup;
    private TextMeshProUGUI messageText;
    private float visibleUntil;
    private float fadeOutStartTime;

    public static void Show(string message, Color color, float duration = 0.9f)
    {
        EnsureInstance();
        instance.ShowMessage(message, color, duration);
    }

    private static void EnsureInstance()
    {
        if (instance != null)
            return;

        GameObject overlay = new(nameof(CenterScreenFeedbackUI));
        instance = overlay.AddComponent<CenterScreenFeedbackUI>();
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

    private void Update()
    {
        if (canvasGroup == null || Time.unscaledTime < fadeOutStartTime)
            return;

        float fadeProgress = Mathf.InverseLerp(fadeOutStartTime, visibleUntil, Time.unscaledTime);
        canvasGroup.alpha = 1f - fadeProgress;

        if (Time.unscaledTime >= visibleUntil)
        {
            canvasGroup.alpha = 0f;
        }
    }

    private void ShowMessage(string message, Color color, float duration)
    {
        if (messageText == null || canvasGroup == null)
            return;

        float visibleSeconds = Mathf.Max(FadeOutSeconds, duration);
        messageText.text = message;
        messageText.color = color;
        canvasGroup.alpha = 1f;
        visibleUntil = Time.unscaledTime + visibleSeconds;
        fadeOutStartTime = visibleUntil - FadeOutSeconds;
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
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        GameObject textObject = new("CenterFeedbackText");
        textObject.transform.SetParent(transform, false);

        TMP_FontAsset sceneFont = ResolveSceneFont();
        messageText = textObject.AddComponent<TextMeshProUGUI>();
        if (sceneFont != null)
        {
            messageText.font = sceneFont;
        }

        messageText.alignment = TextAlignmentOptions.Center;
        messageText.fontSize = 38f;
        messageText.fontStyle = FontStyles.Bold;
        messageText.fontWeight = FontWeight.Bold;
        messageText.textWrappingMode = TextWrappingModes.NoWrap;
        messageText.overflowMode = TextOverflowModes.Overflow;
        messageText.raycastTarget = false;
        messageText.outlineColor = Color.black;
        messageText.outlineWidth = OutlineWidth;

        UnityEngine.UI.Outline outline = textObject.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.7f);
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = true;

        UnityEngine.UI.Shadow shadow = textObject.AddComponent<UnityEngine.UI.Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
        shadow.effectDistance = new Vector2(2f, -2f);
        shadow.useGraphicAlpha = true;

        RectTransform rect = messageText.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 70f);
        rect.sizeDelta = new Vector2(680f, 72f);
    }

    private static TMP_FontAsset ResolveSceneFont()
    {
        TMP_Text[] sceneTexts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (TMP_Text sceneText in sceneTexts)
        {
            if (sceneText != null && sceneText.font != null)
            {
                return sceneText.font;
            }
        }

        return null;
    }
}
