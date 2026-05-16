using UnityEngine;
using UnityEngine.InputSystem;
using System.Reflection; // 리플렉션 사용

public class PlayerSensitivityModifier : MonoBehaviour
{
    private PlayerInputHandler inputHandler;
    private InputAction lookAction;
    private int mouseBindingIndex = -1;
    private float currentSensitivity = -1f;

    void Start()
    {
        inputHandler = GetComponent<PlayerInputHandler>();
        if (inputHandler == null) return;

        // 1. 리플렉션을 통해 팀원의 스크립트 안에 숨겨진 'inputActions' 원본 객체를 빼옵니다.
        FieldInfo field = typeof(PlayerInputHandler).GetField("inputActions", BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null) return;

        object actionsObj = field.GetValue(inputHandler);
        if (actionsObj == null) return;

        // 2. 가져온 객체에서 'asset' 프로퍼티를 찾아 원본 인풋 에셋에 접근합니다.
        PropertyInfo assetProp = actionsObj.GetType().GetProperty("asset");
        if (assetProp == null) return;

        InputActionAsset asset = assetProp.GetValue(actionsObj) as InputActionAsset;
        if (asset == null) return;

        // 3. 에셋에서 "Look" 액션을 찾습니다. (유저님이 설정하신 Actions > Look)
        lookAction = asset.FindAction("Look");
        
        if (lookAction != null)
        {
            // 4. 여러 바인딩 중 "Delta[Mouse]"에 해당하는 바인딩이 몇 번째인지 찾습니다.
            for (int i = 0; i < lookAction.bindings.Count; i++)
            {
                string path = lookAction.bindings[i].path.ToLower();
                if (path.Contains("delta") || path.Contains("mouse"))
                {
                    mouseBindingIndex = i;
                    break;
                }
            }
        }
    }

    void Update()
    {
        // 액션이나 마우스 바인딩을 찾지 못했다면 실행하지 않음
        if (lookAction == null || mouseBindingIndex == -1) return;

        // 로컬에 저장된 환경설정 감도 값 불러오기
        float savedSensitivity = PlayerPrefs.GetFloat("MouseSensitivity", 1.0f);

        // 감도가 바뀌었을 때만 한 번씩 인풋 시스템에 적용 (최적화)
        if (Mathf.Abs(currentSensitivity - savedSensitivity) > 0.01f)
        {
            currentSensitivity = savedSensitivity;

            // ★핵심★: 팀원 스크립트가 값을 읽어가기 전에, 인풋 시스템 자체 프로세서에 감도를 곱해버립니다!
            lookAction.ApplyBindingOverride(mouseBindingIndex, new InputBinding
            {
                overrideProcessors = $"scaleVector2(x={currentSensitivity},y={currentSensitivity})"
            });
        }
    }
}