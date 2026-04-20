using UnityEngine;
using UnityEngine.UI;
using Live2D.Cubism.Framework.LookAt;

public class Live2DRenderTextureMouseLookTarget1 : MonoBehaviour, ICubismLookTarget
{
    [Header("=== UI ===")]
    [Tooltip("마스코트를 띄우고 있는 캔버스")]
    [SerializeField] private Canvas targetCanvas;
    [Tooltip("설이 화면이 나오는 RawImage")]
    [SerializeField] private RawImage targetRawImage;

    [Header("=== Look Target (World) ===")]
    [Tooltip("마우스를 따라다닐 더미 오브젝트(빨간점 등)")]
    [SerializeField] private Transform lookTarget;         
    [Tooltip("시선이 움직일 기준점(보통 설이 얼굴 근처)")]
    [SerializeField] private Transform targetPlaneCenter;  
    
    // 시선 가동 범위 (너무 크면 눈이 뒤집힘)
    [SerializeField] private float targetPlaneWidth = 1.5f;
    [SerializeField] private float targetPlaneHeight = 1.5f;

    private bool _isActive = true;
    private Camera uiCamera;

    private void Start()
    {
        // 캔버스의 카메라를 확실하게 찾아옵니다.
        if (targetCanvas != null)
        {
            if (targetCanvas.renderMode == RenderMode.ScreenSpaceCamera || targetCanvas.renderMode == RenderMode.WorldSpace)
            {
                uiCamera = targetCanvas.worldCamera;
            }
            else // Overlay 모드일 때는 카메라가 필요 없음!
            {
                uiCamera = null; 
            }
        }
    }

    /// <summary>
    /// Live2D 컨트롤러가 이 함수를 통해 타겟의 위치를 가져갑니다. (절대 지우면 안 됨)
    /// </summary>
    public Vector3 GetPosition()
    {
        if (lookTarget == null) return Vector3.zero;
        return lookTarget.position;
    }

    /// <summary>
    /// 시선 추적을 할지 말지 결정합니다. (절대 지우면 안 됨)
    /// </summary>
    public bool IsActive()
    {
        return _isActive;
    }

    // 시선 처리는 카메라 이동 등과 겹치지 않게 LateUpdate에서 하는 것이 좋습니다.
    private void LateUpdate()
    {
        if (targetCanvas == null || targetRawImage == null || lookTarget == null || targetPlaneCenter == null)
            return;

        // 💡 1. 마우스 왼쪽 버튼(0)을 누르고 있지 않다면 정면 응시
        if (!Input.GetMouseButton(0))
        {
            // 타겟을 정중앙(정면)으로 되돌림
            lookTarget.position = Vector3.Lerp(lookTarget.position, targetPlaneCenter.position, Time.deltaTime * 10f); 
            _isActive = false; // 시선 추적 기능을 끔 (자연스럽게 앞을 봄)
            return; 
        }

        // 💡 2. 누르고 있을 때 시선 추적 활성화
        _isActive = true;

        Vector2 localPoint;
        // 핵심: uiCamera가 null이면 Overlay 모드에 맞게, 아니면 Camera 모드에 맞게 완벽 계산!
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            targetRawImage.rectTransform,
            Input.mousePosition,
            uiCamera,
            out localPoint
        );

        // 이미지 크기 대비 현재 마우스의 위치를 0.0 ~ 1.0 비율로 계산
        Rect rect = targetRawImage.rectTransform.rect;
        float x01 = Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x);
        float y01 = Mathf.InverseLerp(rect.yMin, rect.yMax, localPoint.y);

        // 시선이 화면 밖으로 나가지 않게 자름
        x01 = Mathf.Clamp01(x01);
        y01 = Mathf.Clamp01(y01);

        // 0~1 비율을 -0.5 ~ +0.5 비율로 바꾸고 평면 크기를 곱함
        float offsetX = (x01 - 0.5f) * targetPlaneWidth;
        float offsetY = (y01 - 0.5f) * targetPlaneHeight;

        // 타겟 오브젝트(빨간 점) 위치 이동
        Vector3 worldPos = targetPlaneCenter.position + (targetPlaneCenter.right * offsetX) + (targetPlaneCenter.up * offsetY);
        
        // 부드럽게 시선 이동 (Lerp 적용)
        lookTarget.position = Vector3.Lerp(lookTarget.position, worldPos, Time.deltaTime * 15f);
    }
}