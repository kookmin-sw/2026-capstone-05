using UnityEngine;
using UnityEngine.UI;

public class NoiseBarController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerNoiseEmitter noiseEmitter;
    [SerializeField] private Image gaugeBar; // 투톤 그라데이션 막대

    [Header("Settings")]
    [SerializeField] private float smoothSpeed = 8f;
    
    private CharacterController controller;
    private float targetFill = 0f;

    private void Start()
    {
        // Emitter가 붙은 오브젝트에서 CharacterController를 가져옵니다.
        controller = noiseEmitter.GetComponent<CharacterController>();
    }

    private void Update()
    {
        UpdateNoiseLogic();
        
        // 부드럽게 게이지 조절
        gaugeBar.fillAmount = Mathf.Lerp(gaugeBar.fillAmount, targetFill, Time.deltaTime * smoothSpeed);
    }

    private void UpdateNoiseLogic()
    {
        if (controller == null) return;

        // 수평 속도 계산
        Vector3 horizontalVel = new Vector3(controller.velocity.x, 0, controller.velocity.z);
        float speed = horizontalVel.magnitude;

        // 1. 이동 상태에 따른 기본 수치 (최대 100 기준 비율)
        if (speed > 0.1f)
        {
            // 속도에 비례하여 0.2 ~ 0.8 사이로 타겟 설정
            targetFill = Mathf.Clamp(speed / 7f, 0.1f, 0.9f);
        }
        else
        {
            targetFill = 0f;
        }

        // 2. 점프/낙하 중일 때 소음 강조
        if (Mathf.Abs(controller.velocity.y) > 0.5f)
        {
            targetFill = Mathf.Max(targetFill, 0.7f);
        }
    }
}