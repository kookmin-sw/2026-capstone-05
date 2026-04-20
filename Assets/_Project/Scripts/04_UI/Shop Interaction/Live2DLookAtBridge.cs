using UnityEngine;
using Live2D.Cubism.Framework.LookAt;

public class Live2DLookAtBridge : MonoBehaviour, ICubismLookTarget
{
    [Header("UI 참조")]
    [Tooltip("기준점이 되는 RT 이미지 (부모)")]
    public RectTransform mascotRawImage; 
    [Tooltip("마우스를 따라다니는 빨간 점 (자식)")]
    public RectTransform lookTargetUI;

    private CubismLookController lookController;
    private Vector3 lookPosition;

    private void Start()
    {
        lookController = GetComponent<CubismLookController>();
        if (lookController != null)
        {
            lookController.Target = this; // 설이의 시선 타겟을 이 스크립트로 지정
        }
    }

    private void Update()
    {
        if (mascotRawImage == null || lookTargetUI == null) return;

        // 빨간 점의 로컬 좌표(RawImage 기준)를 가져와서 -1 ~ 1 사이로 정규화
        // 부모 이미지의 가로/세로 절반 크기로 나누면 정확한 비율이 나옴
        float x = lookTargetUI.localPosition.x / (mascotRawImage.rect.width * 0.5f);
        float y = lookTargetUI.localPosition.y / (mascotRawImage.rect.height * 0.5f);

        // 값 제한 및 저장 (이 값이 실시간으로 모델에 전달됨)
        lookPosition = new Vector3(Mathf.Clamp(x, -1f, 1f), Mathf.Clamp(y, -1f, 1f), 0f);
    }

    // Live2D SDK가 "지금 어디 봐야 해?"라고 물어볼 때 답해주는 함수
    public Vector3 GetPosition()
    {
        return lookPosition;
    }

    public bool IsActive()
    {
        return true;
    }
}