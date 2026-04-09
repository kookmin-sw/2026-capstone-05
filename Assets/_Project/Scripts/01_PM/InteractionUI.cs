using UnityEngine;
using UnityEngine.UIElements;

public class InteractionUI : MonoBehaviour
{
    public static InteractionUI Instance { get; private set; }

    [Header("UI Components")]
    [SerializeField] private UIDocument uiDocument;

    private VisualElement container;
    private Label objectNameLabel;
    private Label interactionPromptLabel;

    private Camera mainCamera;
    private Transform targetTransform;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        mainCamera = Camera.main;
    }

    private void OnEnable()
    {
        if (uiDocument != null && uiDocument.rootVisualElement != null)
        {
            var root = uiDocument.rootVisualElement;

            container = root.Q<VisualElement>(className: "interaction-container");
            objectNameLabel = root.Q<Label>("ObjectName");
            interactionPromptLabel = root.Q<Label>("InteractionPrompt");

            Hide(); // 처음에 숨기기
        }
    }

    private void Update()
    {
        // 컨테이너가 없거나 숨겨져 있으면 패스
        if (container == null || container.style.display == DisplayStyle.None) return;

        // 추적 중이던 대상이 파괴되었을 경우 UI 숨김
        if (targetTransform == null)
        {
            Hide();
            return;
        }

        // 월드 좌표를 스크린 좌표로 변환
        Vector3 screenPos = mainCamera.WorldToScreenPoint(targetTransform.position);
        
        // 카메라 뒤에 있는 경우 UI 숨김
        if (screenPos.z < 0)
        {
            container.style.opacity = 0;
        }
        else
        {
            container.style.opacity = 1;
            
            // UI Toolkit 화면 비율에 맞춘 정확한 포지셔닝
            if (container.panel != null)
            {
                Vector2 uiPos = RuntimePanelUtils.CameraTransformWorldToPanel(container.panel, targetTransform.position, mainCamera);
                
                container.style.left = uiPos.x - (container.resolvedStyle.width / 2f);
                container.style.top = uiPos.y - (container.resolvedStyle.height / 2f);
            }
            else
            {
                // Fallback
                Vector2 uiPos = new Vector2(screenPos.x, Screen.height - screenPos.y);
                container.style.left = uiPos.x - (container.resolvedStyle.width / 2f);
                container.style.top = uiPos.y - (container.resolvedStyle.height / 2f);
            }
        }
    }

    public void Show(string objectName, string prompt, Transform target)
    {
        if (container == null) return;

        targetTransform = target;
        objectNameLabel.text = objectName;
        interactionPromptLabel.text = prompt;

        container.style.display = DisplayStyle.Flex;
    }

    public void Hide()
    {
        if (container == null) return;

        targetTransform = null;
        container.style.display = DisplayStyle.None;
    }
}
