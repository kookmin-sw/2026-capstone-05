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

    private Transform targetTransform;
    private string currentObjectName = string.Empty;
    private string currentPrompt = string.Empty;
    private bool isInitialized;
    private bool isShowing;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
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
        if (isShowing && targetTransform == null)
        {
            Hide();
        }
    }

    private void EnsureUIInitialized()
    {
        if (uiDocument == null || uiDocument.rootVisualElement == null)
        {
            return;
        }

        if (container != null && container.panel != null && container.panel == uiDocument.rootVisualElement.panel)
        {
            isInitialized = true;
            return;
        }

        uiDocument.sortingOrder = 1;
        VisualElement root = uiDocument.rootVisualElement;

        container = root.Q<VisualElement>(className: "interaction-container");
        objectNameLabel = root.Q<Label>("ObjectName");
        interactionPromptLabel = root.Q<Label>("InteractionPrompt");

        if (container == null)
        {
            isInitialized = false;
            return;
        }

        container.style.opacity = 1f;
        container.style.left = StyleKeyword.Null;
        container.style.top = StyleKeyword.Null;
        container.style.display = isShowing ? DisplayStyle.Flex : DisplayStyle.None;

        if (objectNameLabel != null)
        {
            objectNameLabel.text = currentObjectName;
        }

        if (interactionPromptLabel != null)
        {
            interactionPromptLabel.text = currentPrompt;
        }

        isInitialized = true;
    }

    public void Show(string objectName, string prompt, Transform target)
    {
        if (!isInitialized)
        {
            EnsureUIInitialized();
        }

        if (container == null)
        {
            return;
        }

        targetTransform = target;
        objectName ??= string.Empty;
        prompt ??= string.Empty;

        if (objectNameLabel != null && currentObjectName != objectName)
        {
            currentObjectName = objectName;
            objectNameLabel.text = currentObjectName;
        }

        if (interactionPromptLabel != null && currentPrompt != prompt)
        {
            currentPrompt = prompt;
            interactionPromptLabel.text = currentPrompt;
        }

        if (!isShowing)
        {
            container.style.display = DisplayStyle.Flex;
            isShowing = true;
        }
    }

    public void Hide()
    {
        if (!isInitialized)
        {
            EnsureUIInitialized();
        }

        targetTransform = null;

        if (container != null && isShowing)
        {
            container.style.display = DisplayStyle.None;
        }

        isShowing = false;
    }
}
