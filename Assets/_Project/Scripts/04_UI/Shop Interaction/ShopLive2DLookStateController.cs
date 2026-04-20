using UnityEngine;

public class ShopLive2DLookStateController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MonoBehaviour mouseTargetMover; // 기존 마우스 추적 스크립트
    [SerializeField] private Transform lookTarget;           // 실제 LookAt 대상
    [SerializeField] private Transform frontLookPoint;       // 정면을 바라볼 기준 위치

    [Header("State")]
    [SerializeField] private bool isEmotionPlaying = false;  // 감정 애니메이션 재생 여부

    private void LateUpdate()
    {
        bool shouldLookAtMouse = !isEmotionPlaying && Input.GetMouseButton(0);

        if (shouldLookAtMouse)
        {
            if (mouseTargetMover != null && !mouseTargetMover.enabled)
                mouseTargetMover.enabled = true;
        }
        else
        {
            if (mouseTargetMover != null && mouseTargetMover.enabled)
                mouseTargetMover.enabled = false;

            if (lookTarget != null && frontLookPoint != null)
                lookTarget.position = frontLookPoint.position;
        }
    }

    public void BeginEmotion()
    {
        isEmotionPlaying = true;

        if (mouseTargetMover != null)
            mouseTargetMover.enabled = false;

        if (lookTarget != null && frontLookPoint != null)
            lookTarget.position = frontLookPoint.position;
    }

    public void EndEmotion()
    {
        isEmotionPlaying = false;
    }
}
