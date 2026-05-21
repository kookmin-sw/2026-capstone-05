using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UIElements;

using UnityImage = UnityEngine.UI.Image;

[DefaultExecutionOrder(10000)]
public sealed class FixedAspectRatioManager : MonoBehaviour
{
    private const float TargetAspect = 16f / 9f;
    private const int BlackBarSortingOrder = short.MaxValue;
    private const string ToolkitViewportName = "FixedAspectToolkitViewport";
    private const int RefreshFramesAfterRequest = 1;

    private static readonly Vector2 DefaultReferenceResolution = new(1920f, 1080f);

    private static FixedAspectRatioManager instance;

    public static Rect NormalizedViewport { get; private set; } = new Rect(0f, 0f, 1f, 1f);
    public static Rect PixelViewport { get; private set; }
    public static Rect PanelViewport { get; private set; }

    private readonly Dictionary<RectTransform, AnchorState> originalAnchorStates = new();
    private readonly List<VisualElement> toolkitMoveBuffer = new();

    private Canvas blackBarCanvas;
    private RectTransform leftBar;
    private RectTransform rightBar;
    private RectTransform topBar;
    private RectTransform bottomBar;

    private int lastScreenWidth = -1;
    private int lastScreenHeight = -1;
    private int refreshFramesRemaining;
    private bool isApplyingAspectTargets;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
        {
            return;
        }

        var managerObject = new GameObject(nameof(FixedAspectRatioManager));
        DontDestroyOnLoad(managerObject);
        managerObject.AddComponent<FixedAspectRatioManager>();
    }

    public static bool IsScreenPointInsideActiveArea(Vector2 screenPosition)
    {
        return PixelViewport.Contains(screenPosition);
    }

    public static bool IsPanelPointInsideActiveArea(Vector2 panelPosition)
    {
        return PanelViewport.Contains(panelPosition);
    }

    public static bool TryScreenToActiveAreaPoint(Vector2 screenPosition, out Vector2 activeAreaPoint)
    {
        if (!IsScreenPointInsideActiveArea(screenPosition))
        {
            activeAreaPoint = default;
            return false;
        }

        activeAreaPoint = screenPosition - PixelViewport.position;
        return true;
    }

    public static void RequestRefresh()
    {
        if (instance == null)
        {
            return;
        }

        instance.QueueRefresh();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.activeSceneChanged += OnActiveSceneChanged;

        RebuildViewport();
        EnsureOverlayObjects();
        ApplyAspectTargets();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            instance = null;
        }
    }

    private void LateUpdate()
    {
        if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
        {
            RebuildViewport();
            UpdateBlackBars();
            QueueRefresh();
        }

        if (refreshFramesRemaining <= 0)
        {
            return;
        }

        ApplyAspectTargets();
        refreshFramesRemaining--;
    }

    private void QueueRefresh()
    {
        refreshFramesRemaining = Mathf.Max(refreshFramesRemaining, RefreshFramesAfterRequest);
    }

    private void OnGUI()
    {
        if (blackBarCanvas == null)
        {
            return;
        }

        var previousDepth = GUI.depth;
        var previousColor = GUI.color;
        GUI.depth = -10000;
        GUI.color = Color.black;

        var left = PanelViewport.xMin;
        var right = PanelViewport.xMax;
        var top = PanelViewport.yMin;
        var bottom = PanelViewport.yMax;

        DrawGuiRect(new Rect(0f, 0f, left, Screen.height));
        DrawGuiRect(new Rect(right, 0f, Screen.width - right, Screen.height));
        DrawGuiRect(new Rect(left, 0f, PanelViewport.width, top));
        DrawGuiRect(new Rect(left, bottom, PanelViewport.width, Screen.height - bottom));

        var currentEvent = Event.current;
        if (currentEvent != null && IsPointerEvent(currentEvent.type) && !PanelViewport.Contains(currentEvent.mousePosition))
        {
            currentEvent.Use();
        }

        GUI.depth = previousDepth;
        GUI.color = previousColor;
    }

    private static bool IsPointerEvent(EventType eventType)
    {
        return eventType == EventType.MouseDown
            || eventType == EventType.MouseUp
            || eventType == EventType.MouseDrag
            || eventType == EventType.MouseMove
            || eventType == EventType.ScrollWheel;
    }

    private static void DrawGuiRect(Rect rect)
    {
        if (rect.width <= 0f || rect.height <= 0f)
        {
            return;
        }

        GUI.DrawTexture(rect, Texture2D.whiteTexture);
    }

    private void RebuildViewport()
    {
        lastScreenWidth = Mathf.Max(1, Screen.width);
        lastScreenHeight = Mathf.Max(1, Screen.height);

        var screenAspect = lastScreenWidth / (float)lastScreenHeight;
        var viewport = new Rect(0f, 0f, 1f, 1f);

        if (screenAspect > TargetAspect)
        {
            viewport.width = TargetAspect / screenAspect;
            viewport.x = (1f - viewport.width) * 0.5f;
        }
        else if (screenAspect < TargetAspect)
        {
            viewport.height = screenAspect / TargetAspect;
            viewport.y = (1f - viewport.height) * 0.5f;
        }

        NormalizedViewport = viewport;

        var pixelX = viewport.x * lastScreenWidth;
        var pixelY = viewport.y * lastScreenHeight;
        var pixelWidth = viewport.width * lastScreenWidth;
        var pixelHeight = viewport.height * lastScreenHeight;

        PixelViewport = new Rect(pixelX, pixelY, pixelWidth, pixelHeight);
        PanelViewport = new Rect(pixelX, lastScreenHeight - pixelY - pixelHeight, pixelWidth, pixelHeight);
    }

    private void EnsureOverlayObjects()
    {
        if (blackBarCanvas != null)
        {
            return;
        }

        var canvasObject = new GameObject("FixedAspectBlackBars");
        canvasObject.transform.SetParent(transform, false);
        blackBarCanvas = canvasObject.AddComponent<Canvas>();
        blackBarCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        blackBarCanvas.sortingOrder = BlackBarSortingOrder;
        canvasObject.AddComponent<GraphicRaycaster>();

        leftBar = CreateBlackBar("Left");
        rightBar = CreateBlackBar("Right");
        topBar = CreateBlackBar("Top");
        bottomBar = CreateBlackBar("Bottom");

        UpdateBlackBars();
    }

    private RectTransform CreateBlackBar(string barName)
    {
        var barObject = new GameObject($"{barName}BlackBar");
        barObject.transform.SetParent(blackBarCanvas.transform, false);

        var image = barObject.AddComponent<UnityImage>();
        image.color = Color.black;
        image.raycastTarget = true;

        return barObject.GetComponent<RectTransform>();
    }

    private void UpdateBlackBars()
    {
        if (blackBarCanvas == null)
        {
            return;
        }

        ApplyAnchorRect(leftBar, new Vector2(0f, 0f), new Vector2(NormalizedViewport.xMin, 1f));
        ApplyAnchorRect(rightBar, new Vector2(NormalizedViewport.xMax, 0f), new Vector2(1f, 1f));
        ApplyAnchorRect(bottomBar, new Vector2(NormalizedViewport.xMin, 0f), new Vector2(NormalizedViewport.xMax, NormalizedViewport.yMin));
        ApplyAnchorRect(topBar, new Vector2(NormalizedViewport.xMin, NormalizedViewport.yMax), new Vector2(NormalizedViewport.xMax, 1f));
    }

    private static void ApplyAnchorRect(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax)
    {
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private void ApplyAspectTargets()
    {
        if (isApplyingAspectTargets)
        {
            return;
        }

        isApplyingAspectTargets = true;
        try
        {
            EnsureOverlayObjects();
            ApplyToCameras();
            RestoreCanvasViewports();
            ApplyToOverlayCanvasAnchors();
            RestoreUIDocumentRoots();
            ApplyToUIDocumentViewports();
        }
        finally
        {
            isApplyingAspectTargets = false;
        }
    }

    private void ApplyToCameras()
    {
        foreach (var targetCamera in FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (targetCamera == null || targetCamera.targetTexture != null)
            {
                continue;
            }

            targetCamera.rect = NormalizedViewport;
        }
    }

    private void RestoreCanvasViewports()
    {
        foreach (var targetCanvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (targetCanvas == null || targetCanvas == blackBarCanvas || targetCanvas.transform is not RectTransform canvasTransform)
            {
                continue;
            }

            var viewport = canvasTransform.Find("FixedAspectViewport");
            if (viewport == null)
            {
                continue;
            }

            for (var index = viewport.childCount - 1; index >= 0; index--)
            {
                viewport.GetChild(index).SetParent(canvasTransform, false);
            }

            Destroy(viewport.gameObject);
        }
    }

    private void ApplyToOverlayCanvasAnchors()
    {
        foreach (var targetCanvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (targetCanvas == null || targetCanvas == blackBarCanvas || targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                continue;
            }

            if (targetCanvas.transform is not RectTransform canvasTransform)
            {
                continue;
            }

            for (var index = 0; index < canvasTransform.childCount; index++)
            {
                if (canvasTransform.GetChild(index) is not RectTransform child)
                {
                    continue;
                }

                ApplyAnchorMapping(child);
            }
        }
    }

    private void ApplyAnchorMapping(RectTransform rectTransform)
    {
        if (!originalAnchorStates.TryGetValue(rectTransform, out var state))
        {
            state = new AnchorState(rectTransform.anchorMin, rectTransform.anchorMax);
            originalAnchorStates.Add(rectTransform, state);
        }

        rectTransform.anchorMin = MapAnchorToViewport(state.AnchorMin);
        rectTransform.anchorMax = MapAnchorToViewport(state.AnchorMax);
    }

    private static Vector2 MapAnchorToViewport(Vector2 anchor)
    {
        return new Vector2(
            Mathf.Lerp(NormalizedViewport.xMin, NormalizedViewport.xMax, anchor.x),
            Mathf.Lerp(NormalizedViewport.yMin, NormalizedViewport.yMax, anchor.y));
    }

    private void RestoreUIDocumentRoots()
    {
        foreach (var document in FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (document == null || document.rootVisualElement == null)
            {
                continue;
            }

            var root = document.rootVisualElement;
            if (root.style.position.value != Position.Absolute)
            {
                continue;
            }

            root.style.position = StyleKeyword.Null;
            root.style.left = StyleKeyword.Null;
            root.style.top = StyleKeyword.Null;
            root.style.right = StyleKeyword.Null;
            root.style.bottom = StyleKeyword.Null;
            root.style.width = StyleKeyword.Null;
            root.style.height = StyleKeyword.Null;
            root.style.overflow = StyleKeyword.Null;
        }
    }

    private void ApplyToUIDocumentViewports()
    {
        foreach (var document in FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (document == null || document.rootVisualElement == null)
            {
                continue;
            }

            var documentRoot = document.rootVisualElement;
            var viewport = GetOrCreateToolkitViewport(documentRoot);
            MoveToolkitChildrenIntoViewport(documentRoot, viewport);
            ApplyToolkitViewportLayout(document, documentRoot, viewport);
        }
    }

    private static float GetResolvedPanelLength(float resolvedLength, int fallbackLength)
    {
        if (float.IsNaN(resolvedLength) || resolvedLength <= 0f)
        {
            return Mathf.Max(1, fallbackLength);
        }

        return resolvedLength;
    }

    private VisualElement GetOrCreateToolkitViewport(VisualElement documentRoot)
    {
        var viewport = documentRoot.Q<VisualElement>(ToolkitViewportName);
        if (viewport != null)
        {
            return viewport;
        }

        viewport = new VisualElement { name = ToolkitViewportName };
        viewport.pickingMode = PickingMode.Ignore;
        documentRoot.Add(viewport);
        return viewport;
    }

    private void MoveToolkitChildrenIntoViewport(VisualElement documentRoot, VisualElement viewport)
    {
        toolkitMoveBuffer.Clear();

        foreach (var child in documentRoot.Children())
        {
            if (child == viewport)
            {
                continue;
            }

            toolkitMoveBuffer.Add(child);
        }

        for (var index = 0; index < toolkitMoveBuffer.Count; index++)
        {
            viewport.Add(toolkitMoveBuffer[index]);
        }
    }

    private void ApplyToolkitViewportLayout(UIDocument document, VisualElement documentRoot, VisualElement viewport)
    {
        var panelWidth = GetResolvedPanelLength(documentRoot.resolvedStyle.width, lastScreenWidth);
        var panelHeight = GetResolvedPanelLength(documentRoot.resolvedStyle.height, lastScreenHeight);
        var referenceResolution = GetReferenceResolution(document);
        var scaleX = NormalizedViewport.width * panelWidth / referenceResolution.x;
        var scaleY = NormalizedViewport.height * panelHeight / referenceResolution.y;

        viewport.style.position = Position.Absolute;
        viewport.style.left = NormalizedViewport.xMin * panelWidth;
        viewport.style.top = (1f - NormalizedViewport.yMax) * panelHeight;
        viewport.style.right = StyleKeyword.Auto;
        viewport.style.bottom = StyleKeyword.Auto;
        viewport.style.width = referenceResolution.x;
        viewport.style.height = referenceResolution.y;
        viewport.style.overflow = Overflow.Hidden;
        viewport.style.transformOrigin = new TransformOrigin(0f, 0f);
        viewport.style.scale = new Scale(new Vector3(scaleX, scaleY, 1f));
    }

    private static Vector2 GetReferenceResolution(UIDocument document)
    {
        if (document.panelSettings == null)
        {
            return DefaultReferenceResolution;
        }

        var referenceResolution = document.panelSettings.referenceResolution;
        if (referenceResolution.x <= 0f || referenceResolution.y <= 0f)
        {
            return DefaultReferenceResolution;
        }

        return referenceResolution;
    }

    private void OnActiveSceneChanged(Scene previousScene, Scene nextScene)
    {
        RebuildViewport();
        UpdateBlackBars();
        QueueRefresh();
    }

    private readonly struct AnchorState
    {
        public AnchorState(Vector2 anchorMin, Vector2 anchorMax)
        {
            AnchorMin = anchorMin;
            AnchorMax = anchorMax;
        }

        public Vector2 AnchorMin { get; }
        public Vector2 AnchorMax { get; }
    }

}
