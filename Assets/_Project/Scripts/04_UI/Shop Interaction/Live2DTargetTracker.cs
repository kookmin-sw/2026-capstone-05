using UnityEngine;
using Live2D.Cubism.Framework.LookAt;

public class Live2DTargetTracker : MonoBehaviour, ICubismLookTarget
{
    [Header("UI 세팅")]
    [Tooltip("기준점이 될 마스코트 모니터 (RawImage)")]
    public RectTransform mascotRawImage; 
    
    [Tooltip("마우스를 따라다닐 빨간 점 (LookTarget)")]
    public RectTransform lookTargetUI;

    private Camera uiCamera; 
    private CubismLookController lookController;
    private Vector3 finalLookPosition;

    private void Start()
    {
        lookController = GetComponent<CubismLookController>();
        if (lookController != null)
        {
            lookController.Target = this; // 설이에게 타겟은 '나'라고 알려줌
        }

        // UI 카메라 자동 찾기
        if (mascotRawImage != null)
        {
            Canvas canvas = mascotRawImage.GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                uiCamera = canvas.worldCamera;
            }
        }
    }

    private void Update()
    {
        if (mascotRawImage == null || lookTargetUI == null) return;

        // 1. 빨간 점(타겟)을 마우스 위치로 이동시키기
        Vector2 mousePos = Input.mousePosition;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(mascotRawImage, mousePos, uiCamera, out Vector2 localPoint))
        {
            // 타겟이 RawImage의 자식이므로, localPoint를 그대로 넣으면 마우스를 따라갑니다.
            lookTargetUI.localPosition = localPoint;
        }

        // 2. 타겟의 로컬 좌표를 읽어서 시선(-1 ~ 1)으로 변환하기
        // 기준점(RawImage 중심)에서 타겟이 얼마나 떨어져 있는지 계산합니다.
        float normalizedX = lookTargetUI.localPosition.x / (mascotRawImage.rect.width * 0.5f);
        float normalizedY = lookTargetUI.localPosition.y / (mascotRawImage.rect.height * 0.5f);

        // 시선이 화면 밖으로 나가지 않게 -1 ~ 1 로 고정
        normalizedX = Mathf.Clamp(normalizedX, -1f, 1f);
        normalizedY = Mathf.Clamp(normalizedY, -1f, 1f);

        finalLookPosition = new Vector3(normalizedX, normalizedY, 0f);
    }

    // --- Live2D 시선 인터페이스 ---
    public Vector3 GetPosition()
    {
        return finalLookPosition;
    }

    public bool IsActive()
    {
        return true;
    }
}