using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class InteractionCursorController : MonoBehaviour
{
    [Header("Cursor Settings")]
    [Tooltip("Default custom cursor sprite shown while a UI is open.")]
    public Sprite defaultCursorSprite;

    [Tooltip("Optional custom cursor sprite shown when hovering an interactable target.")]
    public Sprite interactCursorSprite;

    [Header("Visual Effects")]
    public float defaultScale = 1.0f;
    public float interactScale = 1.2f;
    public float scaleSpeed = 10f;

    private Image cursorImage;
    private RectTransform rectTransform;
    private Canvas parentCanvas;
    private bool isHoveringInteractable;

    private void Awake()
    {
        cursorImage = GetComponent<Image>();
        rectTransform = GetComponent<RectTransform>();
        parentCanvas = GetComponentInParent<Canvas>();

        cursorImage.raycastTarget = false;
        cursorImage.enabled = false;

        if (defaultCursorSprite != null)
        {
            cursorImage.sprite = defaultCursorSprite;
        }
    }

    private void LateUpdate()
    {
        bool shouldShowCustomCursor = ShouldShowCustomCursor();
        cursorImage.enabled = shouldShowCustomCursor;

        // Keep the OS cursor hidden whenever this controller exists, so UI screens
        // do not show both the custom cursor and the Windows cursor.
        Cursor.visible = false;

        if (!shouldShowCustomCursor)
        {
            return;
        }

        UpdateCursorPosition();

        float targetScale = isHoveringInteractable ? interactScale : defaultScale;
        Vector3 targetScaleVector = new Vector3(targetScale, targetScale, 1f);
        rectTransform.localScale = Vector3.Lerp(
            rectTransform.localScale,
            targetScaleVector,
            Time.unscaledDeltaTime * scaleSpeed);
    }

    public void SetInteractState(bool isHovering)
    {
        if (isHoveringInteractable == isHovering)
        {
            return;
        }

        isHoveringInteractable = isHovering;

        if (interactCursorSprite != null && defaultCursorSprite != null)
        {
            cursorImage.sprite = isHovering ? interactCursorSprite : defaultCursorSprite;
        }
    }

    private bool ShouldShowCustomCursor()
    {
        if (PauseMenuManager.Instance != null)
        {
            return PauseMenuManager.IsAnyUIOpen();
        }

        return Cursor.lockState != CursorLockMode.Locked;
    }

    private void UpdateCursorPosition()
    {
        if (parentCanvas == null || parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            rectTransform.position = Input.mousePosition;
            return;
        }

        RectTransform parentRect = rectTransform.parent as RectTransform;
        if (parentRect == null)
        {
            rectTransform.position = Input.mousePosition;
            return;
        }

        Camera canvasCamera = parentCanvas.worldCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect,
                Input.mousePosition,
                canvasCamera,
                out Vector2 localPoint))
        {
            rectTransform.localPosition = localPoint;
        }
    }
}
