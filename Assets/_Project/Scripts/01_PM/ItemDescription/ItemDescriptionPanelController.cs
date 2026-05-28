using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class ItemDescriptionPanelController : MonoBehaviour
{
    private const string UxmlPath = "UI/ItemDescriptionPanel";
    private const string UssPath = "UI/ItemDescriptionPanel";
    private const float ShapeCellSize = 14f;
    private const float ShapeCellGap = 2f;
    private const float BottomPadding = 150f;

    [SerializeField] private UIDocument document;

    private static ItemDescriptionPanelController activeInstance;
    private static VisualTreeAsset panelAsset;
    private static StyleSheet panelStyleSheet;
    private static VisualElement currentRoot;
    private static VisualElement currentOwner;
    private static bool usingSceneDocumentPanel;
    private static VisualElement layer;
    private static VisualElement icon;
    private static VisualElement shapePreview;
    private static VisualElement shapeGrid;
    private static Label itemName;
    private static Label itemPrice;
    private static Label itemQuantity;
    private static Label itemDesc;

    private void Awake()
    {
        RegisterInstance();
    }

    private void OnEnable()
    {
        RegisterInstance();
    }

    private void Start()
    {
        EnsureDocumentPanel(true);
    }

    private void OnDisable()
    {
        HideImmediate();
        if (activeInstance == this)
        {
            activeInstance = null;
        }
    }

    private void OnDestroy()
    {
        if (activeInstance == this)
        {
            activeInstance = null;
        }
    }

    private void RegisterInstance()
    {
        if (document == null)
        {
            document = GetComponent<UIDocument>();
        }

        activeInstance = this;
        EnsureDocumentPanel(true);
    }

    public static void Show(ItemInstance item, VisualElement owner)
    {
        if (item == null || item.Data == null || owner == null)
        {
            Hide(owner);
            return;
        }

        if (!TryEnsureActivePanel())
        {
            VisualElement root = GetRoot(owner);
            if (root == null || !EnsureFallbackPanel(root))
            {
                return;
            }
        }

        currentOwner = owner;
        Populate(item);
        layer.style.display = DisplayStyle.Flex;
        layer.BringToFront();
    }

    public static void Hide(VisualElement owner)
    {
        if (owner != null && currentOwner != null && owner != currentOwner)
        {
            return;
        }

        HideImmediate();
    }

    public static void HideImmediate()
    {
        if (layer != null)
        {
            layer.style.display = DisplayStyle.None;
        }

        currentOwner = null;
    }

    private static bool TryEnsureActivePanel()
    {
        return activeInstance != null &&
               activeInstance.isActiveAndEnabled &&
               activeInstance.EnsureDocumentPanel(false);
    }

    private bool EnsureDocumentPanel(bool forceHide)
    {
        if (document == null)
        {
            document = GetComponent<UIDocument>();
        }

        if (document == null || document.rootVisualElement == null)
        {
            return false;
        }

        LoadAssets();

        VisualElement root = document.rootVisualElement;
        if (panelStyleSheet != null && !root.styleSheets.Contains(panelStyleSheet))
        {
            root.styleSheets.Add(panelStyleSheet);
        }

        VisualElement documentLayer = root.Q<VisualElement>("item-description-layer");
        if (documentLayer == null && panelAsset != null)
        {
            VisualElement clonedTree = panelAsset.CloneTree();
            root.Add(clonedTree);
            documentLayer = clonedTree.Q<VisualElement>("item-description-layer");
        }

        if (documentLayer == null)
        {
            return false;
        }

        if (layer != null && layer.parent != null && !usingSceneDocumentPanel && layer != documentLayer)
        {
            layer.RemoveFromHierarchy();
        }

        CachePanelElements(root, documentLayer, true);
        if (forceHide)
        {
            layer.style.display = DisplayStyle.None;
        }

        return true;
    }

    private static bool EnsureFallbackPanel(VisualElement root)
    {
        LoadAssets();

        if (panelAsset == null)
        {
            Debug.LogWarning("[ItemDescriptionPanel] ItemDescriptionPanel.uxml not found in Resources/UI.");
            return false;
        }

        if (currentRoot != root || layer == null || usingSceneDocumentPanel)
        {
            if (layer != null && layer.parent != null && !usingSceneDocumentPanel)
            {
                layer.RemoveFromHierarchy();
            }
            else if (usingSceneDocumentPanel)
            {
                HideImmediate();
            }

            if (panelStyleSheet != null && !root.styleSheets.Contains(panelStyleSheet))
            {
                root.styleSheets.Add(panelStyleSheet);
            }

            VisualElement clonedTree = panelAsset.CloneTree();
            VisualElement clonedLayer = clonedTree.Q<VisualElement>("item-description-layer");
            if (clonedLayer == null)
            {
                Debug.LogWarning("[ItemDescriptionPanel] item-description-layer is missing in UXML.");
                return false;
            }

            CachePanelElements(root, clonedLayer, false);
            root.Add(layer);
        }

        return true;
    }

    private static void LoadAssets()
    {
        if (panelAsset == null)
        {
            panelAsset = Resources.Load<VisualTreeAsset>(UxmlPath);
        }

        if (panelStyleSheet == null)
        {
            panelStyleSheet = Resources.Load<StyleSheet>(UssPath);
        }
    }

    private static void CachePanelElements(VisualElement root, VisualElement panelLayer, bool isSceneDocumentPanel)
    {
        currentRoot = root;
        usingSceneDocumentPanel = isSceneDocumentPanel;
        layer = panelLayer;
        icon = layer.Q<VisualElement>("item-description-icon");
        shapePreview = layer.Q<VisualElement>("item-description-shape-preview");
        shapeGrid = layer.Q<VisualElement>("item-description-shape-grid");
        itemName = layer.Q<Label>("item-description-name");
        itemPrice = layer.Q<Label>("item-description-price");
        itemQuantity = layer.Q<Label>("item-description-quantity");
        itemDesc = layer.Q<Label>("item-description-desc");

        ApplyLayerLayout(layer);
        SetPickingModeRecursive(layer, PickingMode.Ignore);
    }

    private static void ApplyLayerLayout(VisualElement target)
    {
        if (target == null)
        {
            return;
        }

        target.style.position = Position.Absolute;
        target.style.left = 0;
        target.style.right = 0;
        target.style.top = 0;
        target.style.bottom = 0;
        target.style.justifyContent = Justify.FlexEnd;
        target.style.alignItems = Align.Center;
        target.style.paddingBottom = BottomPadding;
    }

    private static VisualElement GetRoot(VisualElement element)
    {
        VisualElement root = element;
        while (root.parent != null)
        {
            root = root.parent;
        }

        return root;
    }

    private static void SetPickingModeRecursive(VisualElement element, PickingMode pickingMode)
    {
        if (element == null)
        {
            return;
        }

        element.pickingMode = pickingMode;
        foreach (VisualElement child in element.Children())
        {
            SetPickingModeRecursive(child, pickingMode);
        }
    }

    private static void Populate(ItemInstance item)
    {
        ItemData data = item.Data;

        if (icon != null)
        {
            icon.style.backgroundImage = data.itemIcon != null ? new StyleBackground(data.itemIcon) : default;
            icon.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(BackgroundSizeType.Contain));
        }

        if (itemName != null)
        {
            itemName.text = data.ItemNameString;
        }

        if (itemPrice != null)
        {
            itemPrice.text = $"{data.price} Gold";
        }

        if (itemQuantity != null)
        {
            itemQuantity.text = $"\uC218\uB7C9 : {item.currentStackCount}";
        }

        if (itemDesc != null)
        {
            itemDesc.text = data.DescriptionString;
        }

        DrawShape(data.gridShape, item.currentRotation);
    }

    private static void DrawShape(ItemGridShape shape, ItemRotation rotation)
    {
        if (shapeGrid == null || shapePreview == null)
        {
            return;
        }

        shapeGrid.Clear();

        if (shape == null || shape.basePositions == null || shape.basePositions.Count == 0)
        {
            shapePreview.style.display = DisplayStyle.None;
            return;
        }

        shapePreview.style.display = DisplayStyle.Flex;

        List<Vector2Int> positions = shape.GetRotatedPositions(rotation);
        int minX = 0;
        int minY = 0;
        int maxX = 0;
        int maxY = 0;

        foreach (Vector2Int pos in positions)
        {
            if (pos.x < minX) minX = pos.x;
            if (pos.y < minY) minY = pos.y;
            if (pos.x > maxX) maxX = pos.x;
            if (pos.y > maxY) maxY = pos.y;
        }

        int width = maxX - minX + 1;
        int height = maxY - minY + 1;
        float stride = ShapeCellSize + ShapeCellGap;

        shapeGrid.style.width = width * stride - ShapeCellGap;
        shapeGrid.style.height = height * stride - ShapeCellGap;

        foreach (Vector2Int pos in positions)
        {
            VisualElement cell = new VisualElement();
            cell.AddToClassList("item-description-shape-cell");
            cell.pickingMode = PickingMode.Ignore;
            cell.style.left = (pos.x - minX) * stride;
            cell.style.top = (pos.y - minY) * stride;
            shapeGrid.Add(cell);
        }
    }
}
