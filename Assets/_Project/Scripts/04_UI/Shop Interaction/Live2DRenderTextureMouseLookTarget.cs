using UnityEngine;
using UnityEngine.UI;
using Live2D.Cubism.Framework.LookAt;

public class Live2DRenderTextureMouseLookTarget : MonoBehaviour, ICubismLookTarget
{
    [Header("=== UI ===")]
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private RawImage targetRawImage;
    [SerializeField] private Camera uiCamera; // Overlay면 비워둬도 됨

    [Header("=== Look Target (World) ===")]
    [SerializeField] private Transform lookTarget;         // 실제로 움직일 더미 오브젝트
    [SerializeField] private Transform targetPlaneCenter;  // 시선이 움직일 기준 평면의 중심
    [SerializeField] private float targetPlaneWidth = 2.0f;
    [SerializeField] private float targetPlaneHeight = 2.0f;

    [Header("=== Options ===")]
    [SerializeField] private bool clampInsideRawImage = true;
    [SerializeField] private bool hideWhenMouseOutside = false;
    [SerializeField] private bool invertX = false;
    [SerializeField] private bool invertY = false;
    [SerializeField] private bool debugLog = false;

    private bool _isActive = true;

    /// <summary>
    /// CubismLookController가 참조하는 타깃 위치
    /// </summary>
    public Vector3 GetPosition()
    {
        if (lookTarget == null)
            return Vector3.zero;

        return lookTarget.position;
    }

    /// <summary>
    /// CubismLookController가 참조하는 활성 여부
    /// </summary>
    public bool IsActive()
    {
        return _isActive;
    }

    private void Reset()
    {
        targetCanvas = GetComponentInParent<Canvas>();
    }

    private void LateUpdate()
    {
        if (targetCanvas == null || targetRawImage == null || lookTarget == null || targetPlaneCenter == null)
            return;

        RectTransform rectTransform = targetRawImage.rectTransform;

        Camera eventCamera = GetEventCamera(targetCanvas);

        Vector2 localPoint;
        bool success = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform,
            Input.mousePosition,
            eventCamera,
            out localPoint
        );

        if (!success)
        {
            _isActive = !hideWhenMouseOutside;
            return;
        }

        Rect rect = rectTransform.rect;

        // rect 기준 0~1 비율로 변환
        float x01 = Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x);
        float y01 = Mathf.InverseLerp(rect.yMin, rect.yMax, localPoint.y);

        bool isInside = (x01 >= 0f && x01 <= 1f && y01 >= 0f && y01 <= 1f);

        if (!isInside && hideWhenMouseOutside)
        {
            _isActive = false;
            return;
        }

        _isActive = true;

        if (clampInsideRawImage)
        {
            x01 = Mathf.Clamp01(x01);
            y01 = Mathf.Clamp01(y01);
        }

        if (invertX) x01 = 1f - x01;
        if (invertY) y01 = 1f - y01;

        // 0~1 -> -0.5~0.5 -> 월드 평면 크기로 변환
        float offsetX = (x01 - 0.5f) * targetPlaneWidth;
        float offsetY = (y01 - 0.5f) * targetPlaneHeight;

        Vector3 worldPos =
            targetPlaneCenter.position +
            targetPlaneCenter.right * offsetX +
            targetPlaneCenter.up * offsetY;

        lookTarget.position = worldPos;

        if (debugLog)
        {
            Debug.Log(
                $"[Live2DLookTarget] mouse={Input.mousePosition}, local={localPoint}, " +
                $"x01={x01:F3}, y01={y01:F3}, inside={isInside}, world={worldPos}"
            );
        }
    }

    private Camera GetEventCamera(Canvas canvas)
    {
        // Unity 문서 기준:
        // Screen Space - Overlay => null
        // Screen Space - Camera / World Space => 해당 Canvas 카메라 사용
        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;

        if (canvas.worldCamera != null)
            return canvas.worldCamera;

        return uiCamera;
    }
}