using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIARMAnager : MonoBehaviour
{
    public GameObject panelColors;
    public GameObject panelFurniture;

    [Header("Runtime Links")]
    [SerializeField]
    private FurnitureCatalog furnitureCatalog;

    [SerializeField]
    private FurniturePlacementManager placementManager;

    [SerializeField]
    private RoomColorManager roomColorManager;

    private const float PanelGap = 12f;
    private float panelBottomOffset = 248f;
    private Transform bottomButtonsContainer;

    private readonly Color[] wallColors =
    {
        new Color(0.92f, 0.90f, 0.84f),
        new Color(0.72f, 0.82f, 0.78f),
        new Color(0.58f, 0.68f, 0.78f),
        new Color(0.88f, 0.70f, 0.62f),
        new Color(0.78f, 0.72f, 0.86f),
        new Color(0.95f, 0.95f, 0.95f)
    };

    private void Start()
    {
        ResolveRuntimeLinks();
        ResolveBottomMenuLayout();
        EnsureRuntimePanels();
        EnsureRotateButton();
        BuildFurniturePanel();
        BuildColorPanel();
        ClosePanels();
    }

    public void OpenColors()
    {
        bool shouldOpen = panelColors != null && !panelColors.activeSelf;

        SetPanelActive(panelColors, shouldOpen);
        SetPanelActive(panelFurniture, false);
    }

    public void OpenFurniture()
    {
        bool shouldOpen = panelFurniture != null && !panelFurniture.activeSelf;

        SetPanelActive(panelFurniture, shouldOpen);
        SetPanelActive(panelColors, false);
    }

    public void ClearScene()
    {
        Debug.Log("Limpiar escena");
        ClosePanels();
    }

    public void SaveDesign()
    {
        Debug.Log("Guardar diseno");
        ClosePanels();
    }

    public void RotateLastFurniture()
    {
        if (placementManager == null)
        {
            Debug.LogWarning("UIARMAnager: falta FurniturePlacementManager.");
            return;
        }

        placementManager.RotateLastFurniture();
        ClosePanels();
    }

    public void ClosePanels()
    {
        SetPanelActive(panelColors, false);
        SetPanelActive(panelFurniture, false);
    }

    private void SetPanelActive(GameObject panel, bool isActive)
    {
        if (panel == null)
        {
            Debug.LogWarning("UIARMAnager: falta asignar un panel.");
            return;
        }

        panel.SetActive(isActive);
    }

    private void ResolveRuntimeLinks()
    {
        if (furnitureCatalog == null)
        {
            furnitureCatalog = FindObjectOfType<FurnitureCatalog>();
        }

        if (placementManager == null)
        {
            placementManager = FindObjectOfType<FurniturePlacementManager>();
        }

        if (roomColorManager == null)
        {
            roomColorManager = FindObjectOfType<RoomColorManager>();
        }

        if (roomColorManager == null)
        {
            roomColorManager = gameObject.AddComponent<RoomColorManager>();
        }
    }

    private void ResolveBottomMenuLayout()
    {
        GameObject bottomMenu = GameObject.Find("BottomMenu");
        if (bottomMenu != null)
        {
            RectTransform bottomRect = bottomMenu.GetComponent<RectTransform>();
            if (bottomRect != null)
            {
                float top = bottomRect.anchoredPosition.y + bottomRect.rect.height * (1f - bottomRect.pivot.y);
                panelBottomOffset = top + PanelGap;
            }
        }

        GameObject buttonsContainer = GameObject.Find("BottomsContainer");
        if (buttonsContainer != null)
        {
            bottomButtonsContainer = buttonsContainer.transform;
        }
    }

    private void EnsureRuntimePanels()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("UIARMAnager: no se encontro Canvas para crear paneles.");
            return;
        }

        if (panelFurniture == null || panelFurniture.name != "RuntimeFurniturePanel")
        {
            panelFurniture = CreateRuntimePanel(canvas.transform, "RuntimeFurniturePanel", 190f);
        }

        if (panelColors == null || panelColors.name != "RuntimeColorPanel")
        {
            panelColors = CreateRuntimePanel(canvas.transform, "RuntimeColorPanel", 130f);
        }
    }

    private void EnsureRotateButton()
    {
        if (bottomButtonsContainer == null || placementManager == null)
        {
            return;
        }

        if (bottomButtonsContainer.Find("BtnRotate") != null)
        {
            return;
        }

        Button rotateButton = CreateTextButton(bottomButtonsContainer, "ROTAR");
        rotateButton.name = "BtnRotate";
        rotateButton.onClick.AddListener(RotateLastFurniture);
    }

    private void BuildFurniturePanel()
    {
        if (panelFurniture == null || furnitureCatalog == null || !furnitureCatalog.HasItems())
        {
            Debug.LogWarning("UIARMAnager: no hay catalogo de muebles para mostrar.");
            return;
        }

        PreparePanel(panelFurniture, 190f, new Vector2(150f, 56f));
        ClearPanelChildren(panelFurniture.transform);

        foreach (FurnitureItemData item in furnitureCatalog.Items)
        {
            if (item == null)
            {
                continue;
            }

            Button button = CreateTextButton(panelFurniture.transform, item.itemName);
            button.onClick.AddListener(() =>
            {
                if (placementManager == null)
                {
                    Debug.LogWarning("UIARMAnager: falta FurniturePlacementManager.");
                    return;
                }

                placementManager.SelectFurniture(item);
                placementManager.PlaceSelectedFurniture();
            });
        }
    }

    private void BuildColorPanel()
    {
        if (panelColors == null)
        {
            return;
        }

        PreparePanel(panelColors, 130f, new Vector2(72f, 72f));
        ClearPanelChildren(panelColors.transform);

        foreach (Color color in wallColors)
        {
            Button button = CreateTextButton(panelColors.transform, string.Empty);
            Image image = button.GetComponent<Image>();
            image.color = color;
            button.onClick.AddListener(() =>
            {
                if (roomColorManager == null)
                {
                    Debug.LogWarning("UIARMAnager: falta RoomColorManager.");
                    return;
                }

                roomColorManager.ApplyWallColor(color);
            });
        }
    }

    private GameObject CreateRuntimePanel(Transform parent, string panelName, float height)
    {
        GameObject panel = new GameObject(
            panelName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        panel.transform.SetParent(parent, false);

        Image image = panel.GetComponent<Image>();
        image.color = new Color(0.08f, 0.09f, 0.10f, 0.92f);
        image.raycastTarget = true;

        RectTransform rectTransform = panel.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0f, 0f);
        rectTransform.anchorMax = new Vector2(1f, 0f);
        rectTransform.pivot = new Vector2(0.5f, 0f);
        rectTransform.anchoredPosition = new Vector2(0f, panelBottomOffset);
        rectTransform.sizeDelta = new Vector2(0f, height);

        panel.transform.SetAsLastSibling();
        return panel;
    }

    private void PreparePanel(GameObject panel, float height, Vector2 cellSize)
    {
        RectTransform rectTransform = panel.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.anchorMin = new Vector2(0f, 0f);
            rectTransform.anchorMax = new Vector2(1f, 0f);
            rectTransform.pivot = new Vector2(0.5f, 0f);
            rectTransform.anchoredPosition = new Vector2(0f, panelBottomOffset);
            rectTransform.sizeDelta = new Vector2(0f, height);
        }

        Image image = panel.GetComponent<Image>();
        if (image != null)
        {
            image.color = new Color(0.08f, 0.09f, 0.10f, 0.92f);
            image.raycastTarget = false;
        }

        GridLayoutGroup grid = panel.GetComponent<GridLayoutGroup>();
        if (grid == null)
        {
            grid = panel.AddComponent<GridLayoutGroup>();
        }

        grid.padding = new RectOffset(16, 16, 16, 16);
        grid.spacing = new Vector2(12f, 12f);
        grid.cellSize = cellSize;
        grid.childAlignment = TextAnchor.MiddleCenter;
        grid.constraint = GridLayoutGroup.Constraint.Flexible;

        panel.transform.SetAsLastSibling();
    }

    private void ClearPanelChildren(Transform panelTransform)
    {
        for (int i = panelTransform.childCount - 1; i >= 0; i--)
        {
            Destroy(panelTransform.GetChild(i).gameObject);
        }
    }

    private Button CreateTextButton(Transform parent, string label)
    {
        GameObject buttonObject = new GameObject(
            "Btn_" + (string.IsNullOrEmpty(label) ? "Color" : label),
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.92f);
        image.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        if (!string.IsNullOrEmpty(label))
        {
            GameObject textObject = new GameObject(
                "Label",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(buttonObject.transform, false);

            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8f, 4f);
            textRect.offsetMax = new Vector2(-8f, -4f);

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = label;
            text.color = new Color(0.12f, 0.12f, 0.12f);
            text.fontSize = 20f;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
        }

        return button;
    }
}
