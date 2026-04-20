using UnityEngine;
using Live2D.Cubism.Framework.LookAt;

public class Live2DLookAt : MonoBehaviour, ICubismLookTarget
{
    [Header("UI 세팅")]
    [Tooltip("마스코트 화면이 나오는 RawImage (Mascot RT)")]
    public RectTransform mascotUIRect; 

    private Camera uiCamera; 
    private CubismLookController lookController;
    private Vector3 targetPosition;

    private void Start()
    {
        lookController = GetComponent<CubismLookController>();
        if (lookController != null)
        {
            lookController.Target = this; // 스크립트 스스로를 타겟으로 등록
        }

        // 캔버스의 렌더 모드를 파악하여 적절한 카메라 찾기
        if (mascotUIRect != null)
        {
            Canvas canvas = mascotUIRect.GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                uiCamera = canvas.worldCamera;
            }
        }
    }

    private void Update()
    {
        if (mascotUIRect == null) return;

        Vector2 mousePos = Input.mousePosition;
        Vector2 localPoint;

        // 1. 캔버스 모드에 따라 마우스 화면 좌표를 UI 로컬 좌표로 변환
        if (uiCamera != null) 
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(mascotUIRect, mousePos, uiCamera, out localPoint);
        }
        else 
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(mascotUIRect, mousePos, null, out localPoint);
        }

        // 2. 피벗(Pivot) 위치에 상관없이 정확하게 화면 비율을 구함
        Rect rect = mascotUIRect.rect;
        
        // InverseLerp: localPoint.x가 사각형의 왼쪽 끝(xMin)이면 0, 오른쪽 끝(xMax)이면 1을 반환
        // 그 후 * 2 - 1 을 해주면 0~1 값이 완벽하게 -1 ~ 1 사이의 값으로 변환됨
        float normalizedX = Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x) * 2f - 1f;
        float normalizedY = Mathf.InverseLerp(rect.yMin, rect.yMax, localPoint.y) * 2f - 1f;

        // 3. 눈동자 제한 (부드러운 시선 처리를 위해 필요시 0.8f 정도로 줄여도 좋습니다)
        normalizedX = Mathf.Clamp(normalizedX, -1f, 1f);
        normalizedY = Mathf.Clamp(normalizedY, -1f, 1f);

        targetPosition = new Vector3(normalizedX, normalizedY, 0f);

        // 테스트용 로그 (마우스 움직일 때마다 값이 -1.00 에서 1.00 사이로 잘 변하는지 확인)
        Debug.Log($"최종 타겟 -> X: {targetPosition.x:F2}, Y: {targetPosition.y:F2}");
    }

    public Vector3 GetPosition()
    {
        return targetPosition;
    }

    public bool IsActive()
    {
        return true;
    }
}