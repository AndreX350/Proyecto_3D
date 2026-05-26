using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

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

    [SerializeField]
    private DesignSaveManager designSaveManager;

    private const float PanelGap = 12f;
    private float panelBottomOffset = 248f;
    private Transform bottomButtonsContainer;
    private bool isPrimaryManager = true;
    private TextMeshProUGUI wallStatusText;

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
        isPrimaryManager = IsPrimaryManager();
        if (!isPrimaryManager)
        {
            return;
        }

        ResolveRuntimeLinks();
        ResolveBottomMenuLayout();
        WireSceneButtons();
        EnsureRuntimePanels();
        EnsureActionButtons();
        EnsureScaleButtons();
        BuildFurniturePanel();
        BuildColorPanel();
        ClosePanels();
        LoadPendingDesignForActiveScene();
    }

    private void Update()
    {
        if (!isPrimaryManager || panelColors == null || !panelColors.activeInHierarchy)
        {
            return;
        }

        UpdateWallStatusText();
    }

    public void OpenColors()
    {
        if (ForwardToPrimary(manager => manager.OpenColors()))
        {
            return;
        }

        FurniturePlacementManager.BlockWorldInputBriefly();
        EnsurePanelsReady();
        bool shouldOpen = panelColors != null && !panelColors.activeSelf;

        SetPanelActive(panelColors, shouldOpen);
        SetPanelActive(panelFurniture, false);
        UpdateWallStatusText();
    }

    public void OpenFurniture()
    {
        if (ForwardToPrimary(manager => manager.OpenFurniture()))
        {
            return;
        }

        FurniturePlacementManager.BlockWorldInputBriefly();
        EnsurePanelsReady();
        bool shouldOpen = panelFurniture != null && !panelFurniture.activeSelf;

        SetPanelActive(panelFurniture, shouldOpen);
        SetPanelActive(panelColors, false);
    }

    public void ClearScene()
    {
        if (ForwardToPrimary(manager => manager.ClearScene()))
        {
            return;
        }

        FurniturePlacementManager.BlockWorldInputBriefly();
        ResolveRuntimeLinks();

        if (placementManager != null)
        {
            placementManager.ClearPlacedFurniture();
        }
        ClosePanels();
    }

    public void SaveDesign()
    {
        if (ForwardToPrimary(manager => manager.SaveDesign()))
        {
            return;
        }

        FurniturePlacementManager.BlockWorldInputBriefly();
        ResolveRuntimeLinks();

        if (placementManager == null)
        {
            Debug.LogWarning("UIARMAnager: falta FurniturePlacementManager para guardar.");
            return;
        }

        if (designSaveManager == null)
        {
            designSaveManager = FindObjectOfType<DesignSaveManager>();
        }

        if (designSaveManager == null)
        {
            designSaveManager = gameObject.AddComponent<DesignSaveManager>();
        }

        placementManager.RefreshPlacedFurnitureList();
        designSaveManager.SaveDesign(placementManager.PlacedFurniture, roomColorManager);
        ClosePanels();
    }

    public void RotateSelectedFurniture()
    {
        if (ForwardToPrimary(manager => manager.RotateSelectedFurniture()))
        {
            return;
        }

        FurniturePlacementManager.BlockWorldInputBriefly();
        ResolveRuntimeLinks();

        if (placementManager == null)
        {
            Debug.LogWarning("UIARMAnager: falta FurniturePlacementManager.");
            return;
        }

        placementManager.RotateSelectedFurniture();
        ClosePanels();
    }

    public void RotateLastFurniture()
    {
        RotateSelectedFurniture();
    }

    public void DeleteSelectedFurniture()
    {
        if (ForwardToPrimary(manager => manager.DeleteSelectedFurniture()))
        {
            return;
        }

        FurniturePlacementManager.BlockWorldInputBriefly();
        ResolveRuntimeLinks();

        if (placementManager == null)
        {
            Debug.LogWarning("UIARMAnager: falta FurniturePlacementManager.");
            return;
        }

        placementManager.DeleteSelectedFurniture();
        ClosePanels();
    }

    public void IncreaseSelectedFurnitureScale()
    {
        if (ForwardToPrimary(manager => manager.IncreaseSelectedFurnitureScale()))
        {
            return;
        }

        FurniturePlacementManager.BlockWorldInputBriefly();
        ResolveRuntimeLinks();

        if (placementManager == null)
        {
            Debug.LogWarning("UIARMAnager: falta FurniturePlacementManager.");
            return;
        }

        placementManager.IncreaseSelectedFurnitureScale();
        ClosePanels();
    }

    public void DecreaseSelectedFurnitureScale()
    {
        if (ForwardToPrimary(manager => manager.DecreaseSelectedFurnitureScale()))
        {
            return;
        }

        FurniturePlacementManager.BlockWorldInputBriefly();
        ResolveRuntimeLinks();

        if (placementManager == null)
        {
            Debug.LogWarning("UIARMAnager: falta FurniturePlacementManager.");
            return;
        }

        placementManager.DecreaseSelectedFurnitureScale();
        ClosePanels();
    }

    public void ClosePanels()
    {
        if (ForwardToPrimary(manager => manager.ClosePanels()))
        {
            return;
        }

        FurniturePlacementManager.BlockWorldInputBriefly();
        EnsurePanelsReady();
        SetPanelActive(panelColors, false);
        SetPanelActive(panelFurniture, false);
    }

    public void OpenSavedDesigns()
    {
        if (ForwardToPrimary(manager => manager.OpenSavedDesigns()))
        {
            return;
        }

        SceneManager.LoadScene("SavedDesgins");
    }

    private void SetPanelActive(GameObject panel, bool isActive)
    {
        if (panel == null)
        {
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

        if (designSaveManager == null)
        {
            designSaveManager = FindObjectOfType<DesignSaveManager>();
        }
    }

    private bool IsPrimaryManager()
    {
        UIARMAnager best = FindPrimaryManager();
        return best == null || best == this;
    }

    private UIARMAnager FindPrimaryManager()
    {
        UIARMAnager[] managers = FindObjectsOfType<UIARMAnager>();
        UIARMAnager best = null;
        int bestScore = int.MinValue;

        foreach (UIARMAnager manager in managers)
        {
            if (manager == null || !manager.gameObject.activeInHierarchy)
            {
                continue;
            }

            int score = manager.GetSceneReferenceScore();
            if (score > bestScore)
            {
                best = manager;
                bestScore = score;
            }
        }

        return best;
    }

    private bool ForwardToPrimary(System.Action<UIARMAnager> action)
    {
        UIARMAnager primary = FindPrimaryManager();
        if (primary == null || primary == this)
        {
            return false;
        }

        action(primary);
        return true;
    }

    private int GetSceneReferenceScore()
    {
        int score = 0;
        if (furnitureCatalog != null) score += 4;
        if (placementManager != null) score += 4;
        if (panelColors != null) score += 1;
        if (panelFurniture != null) score += 1;
        if (gameObject.name == "UIManager") score += 1;
        return score;
    }

    private void LoadPendingDesignForActiveScene()
    {
        string activeScene = SceneManager.GetActiveScene().name;
        if (activeScene == "RoomDemo")
        {
            DesignSaveManager.TryLoadPendingRoomDemoDesign(furnitureCatalog, placementManager, roomColorManager);
        }
        else if (activeScene == "ARScene")
        {
            DesignSaveManager.TryLoadPendingARDesign(furnitureCatalog, placementManager, roomColorManager);
        }
    }

    private void ResolveBottomMenuLayout()
    {
        GameObject bottomMenu = GameObject.Find("BottomMenu");
        if (bottomMenu != null)
        {
            EnsureRaycastBlockingImage(bottomMenu);

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
            EnsureRaycastBlockingImage(buttonsContainer);
            bottomButtonsContainer = buttonsContainer.transform;
        }
    }

    private void WireSceneButtons()
    {
        WireButton("BtnColors", OpenColors);
        WireButton("BtnFurniture", OpenFurniture);
        WireButton("BtnRotate", RotateSelectedFurniture);
        WireButton("BtnSave", SaveDesign);
        WireButton("BtnCargar", OpenSavedDesigns);
        WireButton("BtnCerrar", ClosePanels);
        WireButton("BtnDelete", ClearScene);
        WireButton("BtnClear", ClearScene);
        WireButton("BtnDeleteSelected", DeleteSelectedFurniture);
        WireButton("BtnScaleUp", IncreaseSelectedFurnitureScale);
        WireButton("BtnScaleDown", DecreaseSelectedFurnitureScale);
    }

    private void WireButton(string buttonName, UnityEngine.Events.UnityAction action)
    {
        foreach (Button button in FindButtonsByName(buttonName))
        {
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(action);
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

    private void EnsurePanelsReady()
    {
        if (panelFurniture == null || panelColors == null)
        {
            ResolveBottomMenuLayout();
            EnsureRuntimePanels();
            BuildFurniturePanel();
            BuildColorPanel();
        }
    }

    private void EnsureActionButtons()
    {
        if (placementManager == null)
        {
            return;
        }

        EnsureRotateButton();
        EnsureSelectedDeleteButton();
    }

    private void EnsureRotateButton()
    {
        if (placementManager == null)
        {
            return;
        }

        Button rotateButton = FindRotateButton();

        if (rotateButton == null)
        {
            if (bottomButtonsContainer == null)
            {
                return;
            }

            rotateButton = CreateTextButton(bottomButtonsContainer, "ROTAR");
            rotateButton.name = "BtnRotate";
        }

        rotateButton.onClick.RemoveListener(RotateSelectedFurniture);
        rotateButton.onClick.AddListener(RotateSelectedFurniture);
    }

    private void EnsureSelectedDeleteButton()
    {
        if (placementManager == null)
        {
            return;
        }

        Button deleteButton = FindButtonByName("BtnDeleteSelected");

        if (deleteButton == null)
        {
            if (bottomButtonsContainer == null)
            {
                return;
            }

            deleteButton = CreateTextButton(bottomButtonsContainer, "BORRAR SEL");
            deleteButton.name = "BtnDeleteSelected";
        }

        deleteButton.onClick = new Button.ButtonClickedEvent();
        deleteButton.onClick.AddListener(DeleteSelectedFurniture);
    }

    private void EnsureScaleButtons()
    {
        if (placementManager == null || bottomButtonsContainer == null)
        {
            return;
        }

        Button scaleUpButton = FindButtonByName("BtnScaleUp");
        if (scaleUpButton == null)
        {
            scaleUpButton = CreateTextButton(bottomButtonsContainer, "ESC+");
            scaleUpButton.name = "BtnScaleUp";
        }

        scaleUpButton.onClick.RemoveListener(IncreaseSelectedFurnitureScale);
        scaleUpButton.onClick.AddListener(IncreaseSelectedFurnitureScale);

        Button scaleDownButton = FindButtonByName("BtnScaleDown");
        if (scaleDownButton == null)
        {
            scaleDownButton = CreateTextButton(bottomButtonsContainer, "ESC-");
            scaleDownButton.name = "BtnScaleDown";
        }

        scaleDownButton.onClick.RemoveListener(DecreaseSelectedFurnitureScale);
        scaleDownButton.onClick.AddListener(DecreaseSelectedFurnitureScale);
    }

    private Button FindRotateButton()
    {
        if (bottomButtonsContainer != null)
        {
            Transform rotateTransform = bottomButtonsContainer.Find("BtnRotate");
            if (rotateTransform != null)
            {
                return rotateTransform.GetComponent<Button>();
            }
        }

        GameObject rotateObject = GameObject.Find("BtnRotate");
        if (rotateObject != null)
        {
            return rotateObject.GetComponent<Button>();
        }

        return null;
    }

    private Button FindButtonByName(string buttonName)
    {
        List<Button> buttons = FindButtonsByName(buttonName);
        return buttons.Count > 0 ? buttons[0] : null;
    }

    private List<Button> FindButtonsByName(string buttonName)
    {
        List<Button> buttons = new List<Button>();

        if (bottomButtonsContainer != null)
        {
            Transform buttonTransform = bottomButtonsContainer.Find(buttonName);
            if (buttonTransform != null)
            {
                Button button = buttonTransform.GetComponent<Button>();
                if (button != null)
                {
                    buttons.Add(button);
                }
            }
        }

        foreach (Button button in FindObjectsOfType<Button>(true))
        {
            if (button != null && button.name == buttonName && !buttons.Contains(button))
            {
                buttons.Add(button);
            }
        }

        return buttons;
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
                FurniturePlacementManager.BlockWorldInputBriefly();
                if (placementManager == null)
                {
                    Debug.LogWarning("UIARMAnager: falta FurniturePlacementManager.");
                    return;
                }

                placementManager.SelectFurniture(item);
                if (!placementManager.UsesARTapPlacement)
                {
                    placementManager.PlaceSelectedFurniture();
                }
            });
        }
    }

    private void BuildColorPanel()
    {
        if (panelColors == null)
        {
            return;
        }

        PreparePanel(panelColors, 130f, new Vector2(60f, 60f));
        ClearPanelChildren(panelColors.transform);

        foreach (Color color in wallColors)
        {
            Button button = CreateTextButton(panelColors.transform, string.Empty);
            Image image = button.GetComponent<Image>();
            image.color = color;
            button.onClick.AddListener(() =>
            {
                FurniturePlacementManager.BlockWorldInputBriefly();
                if (roomColorManager == null)
                {
                    Debug.LogWarning("UIARMAnager: falta RoomColorManager.");
                    return;
                }

                roomColorManager.QueueWallColor(color);
                UpdateWallStatusText();
            });
        }

        Button applyButton = CreateTextButton(panelColors.transform, "APLICAR");
        applyButton.name = "BtnApplyWallColor";
        applyButton.onClick.AddListener(() =>
        {
            FurniturePlacementManager.BlockWorldInputBriefly();
            if (roomColorManager == null)
            {
                return;
            }

            roomColorManager.ApplyPendingWallColor();
            UpdateWallStatusText();
        });

        CreateOrRefreshWallStatusText();
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
            image.raycastTarget = true;
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

    private void EnsureRaycastBlockingImage(GameObject target)
    {
        Image image = target.GetComponent<Image>();
        if (image == null)
        {
            image = target.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);
        }

        image.raycastTarget = true;
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

    private void CreateOrRefreshWallStatusText()
    {
        if (panelColors == null)
        {
            return;
        }

        if (wallStatusText == null)
        {
            GameObject statusObject = new GameObject(
                "TxtWallStatus",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI),
                typeof(LayoutElement));
            statusObject.transform.SetParent(panelColors.transform, false);

            RectTransform rectTransform = statusObject.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(220f, 56f);

            LayoutElement layoutElement = statusObject.GetComponent<LayoutElement>();
            layoutElement.preferredWidth = 220f;
            layoutElement.preferredHeight = 56f;

            wallStatusText = statusObject.GetComponent<TextMeshProUGUI>();
            wallStatusText.color = Color.white;
            wallStatusText.fontSize = 19f;
            wallStatusText.alignment = TextAlignmentOptions.Center;
            wallStatusText.enableWordWrapping = true;
        }

        UpdateWallStatusText();
    }

    private void UpdateWallStatusText()
    {
        if (wallStatusText == null || roomColorManager == null)
        {
            return;
        }

        wallStatusText.text = roomColorManager.GetWallStatusText();
    }
}
