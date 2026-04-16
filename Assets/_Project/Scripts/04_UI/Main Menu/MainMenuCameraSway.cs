using UnityEngine;

public class MainMenuCameraSway : MonoBehaviour
{
    [Header("시선 흔들림 설정")]
    public float swayAmountX = 1.0f; // 위아래(끄덕임) 흔들림 강도 (각도)
    public float swayAmountY = 1.5f; // 좌우(도리도리) 흔들림 강도 (각도)
    public float swaySpeed = 0.5f;   // 고개를 움직이는 속도

    private Vector3 startRotation;

    void Start()
    {
        // 시작할 때 카메라가 바라보고 있던 원래 각도를 기억합니다.
        startRotation = transform.rotation.eulerAngles;
    }

    void Update()
    {
        // 부드러운 곡선(Sin, Cos)을 이용해 미세하게 변하는 각도를 계산합니다.
        // X축과 Y축의 움직임 주기를 살짝 다르게(Cos에 0.8 곱함) 해서 기계적이지 않게 만듭니다.
        float offsetX = Mathf.Sin(Time.time * swaySpeed) * swayAmountX;
        float offsetY = Mathf.Cos(Time.time * swaySpeed * 0.8f) * swayAmountY; 

        // 계산된 미세한 각도 차이를 원래 각도에 더해서 적용합니다.
        transform.rotation = Quaternion.Euler(startRotation.x + offsetX, startRotation.y + offsetY, startRotation.z);
    }
}