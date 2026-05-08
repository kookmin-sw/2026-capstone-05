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

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Start()
    {
        EnsureUIInitialized();
    }

    private void Update()
    {
        EnsureUIInitialized();

        // 컨테이너가 없거나 숨겨져 있으면 패스
        if (container == null || container.style.display == DisplayStyle.None) return;

        // 추적 중이던 대상이 파괴되었을 경우 UI 숨김
        if (targetTransform == null)
        {
            Hide();
            return;
        }

        // 항상 화면에 보이도록 불투명도 1 유지
        container.style.opacity = 1;

        // 화면 중앙에 위치하도록 left/top 초기화
        container.style.left = StyleKeyword.Null;
        container.style.top = StyleKeyword.Null;
    }

    private void EnsureUIInitialized()
    {
        if (uiDocument == null || uiDocument.rootVisualElement == null) return;

        // 이미 캐싱되어 있고 패널이 유효하면 초기화 건너뜀
        if (container != null && container.panel != null && container.panel == uiDocument.rootVisualElement.panel) return;

        uiDocument.sortingOrder = 1;
        var root = uiDocument.rootVisualElement;

        // 재초기화 시 이전 상태 보존
        bool wasShowing = (targetTransform != null);
        string currentObjName = objectNameLabel != null ? objectNameLabel.text : "";
        string currentPrompt = interactionPromptLabel != null ? interactionPromptLabel.text : "";

        container = root.Q<VisualElement>(className: "interaction-container");
        objectNameLabel = root.Q<Label>("ObjectName");
        interactionPromptLabel = root.Q<Label>("InteractionPrompt");

        if (container != null)
        {
            if (wasShowing)
            {
                if (objectNameLabel != null) objectNameLabel.text = currentObjName;
                if (interactionPromptLabel != null) interactionPromptLabel.text = currentPrompt;
                container.style.display = DisplayStyle.Flex;
            }
            else
            {
                container.style.display = DisplayStyle.None;
            }
        }
    }
    public void Show(string objectName, string prompt, Transform target)
    {
        EnsureUIInitialized();

        if (container == null) return;

        targetTransform = target;
        if (objectNameLabel != null) objectNameLabel.text = objectName;
        if (interactionPromptLabel != null) interactionPromptLabel.text = prompt;

        container.style.display = DisplayStyle.Flex;
    }

    public void Hide()
    {
        EnsureUIInitialized();

        if (container == null) return;

        targetTransform = null;
        container.style.display = DisplayStyle.None;
    }
}
