using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class FurniturePlacementManager : MonoBehaviour
{
    private static float blockWorldInputUntil;

    [Header("Placement")]
    [SerializeField]
    private Transform spawnPoint = null;

    [SerializeField]
    private Vector3 fallbackSpawnPosition = new Vector3(-1.2f, 0f, 0.2f);

    [SerializeField]
    private float placementSpacing = 1.1f;

    [SerializeField]
    private int placementsPerRow = 4;

    [SerializeField]
    private float rowSpacing = 1.1f;

    [Header("Runtime")]
    [SerializeField]
    private FurnitureItemData selectedFurniture;

    [Header("AR")]
    [SerializeField]
    private bool enableARPlacement = true;

    [SerializeField]
    private ARRaycastManager arRaycastManager;

    [SerializeField]
    private bool anchorARPlacedFurniture = true;

    [SerializeField]
    private float arFallbackPlacementDistance = 1.6f;

    [SerializeField]
    private Camera placementCamera;

    [SerializeField]
    private LayerMask placedFurnitureLayerMask = ~0;

    [Header("RoomDemo Editing")]
    [SerializeField]
    private bool enableRoomEditing = true;

    [SerializeField]
    private LayerMask floorLayerMask = ~0;

    [SerializeField]
    private float moveRayDistance = 100f;

    [SerializeField]
    private float rotateStepDegrees = 45f;

    [SerializeField]
    private float scaleStep = 0.1f;

    [SerializeField]
    private float minUniformScale = 0.4f;

    [SerializeField]
    private float maxUniformScale = 2.5f;

    [SerializeField]
    private bool blockRoomEditingWhenPointerOverUI = true;

    [SerializeField]
    private bool blockPlacementWhenPointerOverUI = true;

    private readonly List<GameObject> placedFurniture = new List<GameObject>();
    private readonly List<ARRaycastHit> arHits = new List<ARRaycastHit>();
    private GameObject lastPlacedFurniture;
    private GameObject selectedPlacedFurniture;
    private bool isDraggingSelectedFurniture;
    private bool isDraggingARFurniture;
    private Plane selectedDragPlane;
    private GameObject selectionVisual;
    public IReadOnlyList<GameObject> PlacedFurniture => placedFurniture;
    public bool UsesARTapPlacement => enableARPlacement && arRaycastManager != null;

    public static void BlockWorldInputBriefly(float seconds = 0.25f)
    {
        blockWorldInputUntil = Mathf.Max(blockWorldInputUntil, Time.unscaledTime + seconds);
    }

    private void Awake()
    {
        if (placementCamera == null)
        {
            placementCamera = Camera.main;
        }

        if (arRaycastManager == null)
        {
            arRaycastManager = FindObjectOfType<ARRaycastManager>();
        }
    }

    private void Update()
    {
        RefreshRuntimeReferences();

        if (Time.unscaledTime < blockWorldInputUntil)
        {
            isDraggingSelectedFurniture = false;
            return;
        }

        if (UsesARTapPlacement)
        {
            UpdateARPlacementInput();
            return;
        }

        UpdateRoomEditingInput();
    }

    private void RefreshRuntimeReferences()
    {
        if (placementCamera == null || !placementCamera.enabled)
        {
            placementCamera = Camera.main;
        }

        if (arRaycastManager == null)
        {
            arRaycastManager = FindObjectOfType<ARRaycastManager>();
        }
    }

    private void UpdateARPlacementInput()
    {
        if (!enableARPlacement || arRaycastManager == null)
        {
            return;
        }

        if (Input.touchCount <= 0)
        {
            isDraggingARFurniture = false;
            return;
        }

        Touch touch = Input.GetTouch(0);
        if (IsPointerOverBlockingUI(touch.position, touch.fingerId))
        {
            isDraggingARFurniture = false;
            return;
        }

        if (touch.phase == TouchPhase.Began)
        {
            if (TrySelectPlacedFurnitureAtScreenPoint(touch.position))
            {
                isDraggingARFurniture = true;
                return;
            }

            TryPlaceSelectedFurnitureAtScreenPoint(touch.position);
            return;
        }

        if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
        {
            if (isDraggingARFurniture && selectedPlacedFurniture != null)
            {
                TryMoveSelectedARFurnitureToScreenPoint(touch.position);
            }

            return;
        }

        if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
        {
            isDraggingARFurniture = false;
            return;
        }
    }

    private void UpdateRoomEditingInput()
    {
        if (!enableRoomEditing)
        {
            return;
        }

        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                HandlePointerDown(touch.position, touch.fingerId);
            }
            else if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
            {
                if (IsPointerBlockedByUI(touch.position, touch.fingerId))
                {
                    isDraggingSelectedFurniture = false;
                    return;
                }

                HandlePointerDrag(touch.position);
            }
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                isDraggingSelectedFurniture = false;
            }

            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            HandlePointerDown(Input.mousePosition, -1);
        }
        else if (Input.GetMouseButton(0))
        {
            if (IsPointerBlockedByUI(Input.mousePosition, -1))
            {
                isDraggingSelectedFurniture = false;
                return;
            }

            HandlePointerDrag(Input.mousePosition);
        }
        else if (Input.GetMouseButtonUp(0))
        {
            isDraggingSelectedFurniture = false;
        }
    }

    private void HandlePointerDown(Vector2 screenPoint, int pointerId)
    {
        if (IsPointerBlockedByUI(screenPoint, pointerId))
        {
            isDraggingSelectedFurniture = false;
            return;
        }

        if (TrySelectPlacedFurnitureAtScreenPoint(screenPoint))
        {
            BeginSelectedFurnitureDrag();
            return;
        }

        if (selectedPlacedFurniture != null)
        {
            ClearSelectedFurniture();
        }
    }

    private void HandlePointerDrag(Vector2 screenPoint)
    {
        if (!isDraggingSelectedFurniture || selectedPlacedFurniture == null)
        {
            return;
        }

        Camera cam = GetPlacementCamera();
        if (cam == null)
        {
            return;
        }

        Ray ray = cam.ScreenPointToRay(screenPoint);
        if (TryGetFloorHitPosition(ray, out Vector3 floorPosition))
        {
            selectedPlacedFurniture.transform.position = floorPosition;
            return;
        }

        if (selectedDragPlane.Raycast(ray, out float enter))
        {
            Vector3 planePoint = ray.GetPoint(enter);
            selectedPlacedFurniture.transform.position = planePoint;
        }
    }

    public void SelectFurniture(FurnitureItemData item)
    {
        selectedFurniture = item;

        if (selectedFurniture != null)
        {
            ClearSelectedFurniture();
            Debug.Log("Selected furniture: " + selectedFurniture.itemName);

            if (UsesARTapPlacement)
            {
                PlaceSelectedFurnitureInCameraView();
            }
        }
    }

    public void PlaceSelectedFurniture()
    {
        if (selectedFurniture == null)
        {
            Debug.LogWarning("FurniturePlacementManager: no furniture selected.");
            return;
        }

        PlaceFurniture(selectedFurniture);
    }

    public void PlaceSelectedFurnitureInCameraView()
    {
        if (selectedFurniture == null)
        {
            Debug.LogWarning("FurniturePlacementManager: no furniture selected.");
            return;
        }

        Camera cam = GetPlacementCamera();
        if (cam == null)
        {
            PlaceSelectedFurniture();
            return;
        }

        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        if (TryPlaceSelectedFurnitureAtScreenPoint(screenCenter))
        {
            return;
        }

        Vector3 position = cam.transform.position + cam.transform.forward * arFallbackPlacementDistance + selectedFurniture.placementOffset;
        Quaternion rotation = Quaternion.Euler(0f, cam.transform.eulerAngles.y, 0f);
        GameObject placed = PlaceFurnitureAt(selectedFurniture, position, rotation);
        TryAnchorPlacedFurniture(placed);
    }

    public void PlaceFurniture(FurnitureItemData item)
    {
        if (item == null)
        {
            Debug.LogWarning("FurniturePlacementManager: item is null.");
            return;
        }

        if (item.prefab == null)
        {
            Debug.LogWarning("FurniturePlacementManager: item has no prefab: " + item.itemName);
            return;
        }

        Vector3 position = GetNextPlacementPosition() + item.placementOffset;
        AddFurnitureInstance(item, position, Quaternion.Euler(0f, 180f, 0f), item.defaultScale);

        Debug.Log("Placed furniture: " + item.itemName);
    }

    public bool TryPlaceSelectedFurnitureAtScreenPoint(Vector2 screenPoint)
    {
        if (!enableARPlacement || arRaycastManager == null)
        {
            return false;
        }

        if (selectedFurniture == null)
        {
            return false;
        }

        if (!arRaycastManager.Raycast(screenPoint, arHits, TrackableType.PlaneWithinPolygon))
        {
            return false;
        }

        Pose hitPose = arHits[0].pose;
        Vector3 position = hitPose.position + selectedFurniture.placementOffset;
        Quaternion rotation = Quaternion.Euler(0f, hitPose.rotation.eulerAngles.y, 0f);
        GameObject placed = PlaceFurnitureAt(selectedFurniture, position, rotation);
        TryAnchorPlacedFurniture(placed);
        return true;
    }

    public GameObject PlaceFurnitureAt(FurnitureItemData item, Vector3 position, Quaternion rotation)
    {
        if (item == null)
        {
            Debug.LogWarning("FurniturePlacementManager: item is null.");
            return null;
        }

        if (item.prefab == null)
        {
            Debug.LogWarning("FurniturePlacementManager: item has no prefab: " + item.itemName);
            return null;
        }

        GameObject instance = AddFurnitureInstance(item, position, rotation, item.defaultScale);
        Debug.Log("Placed furniture at position: " + item.itemName);
        return instance;
    }

    public void PlaceLoadedFurniture(FurnitureItemData item, Vector3 position, float rotY, float scale)
    {
        float safeScale = scale > 0f ? scale : item != null ? item.defaultScale.x : 1f;
        PlaceLoadedFurniture(item, position, rotY, Vector3.one * safeScale);
    }

    public void PlaceLoadedFurniture(FurnitureItemData item, Vector3 position, float rotY, Vector3 scale)
    {
        if (item == null)
        {
            Debug.LogWarning("FurniturePlacementManager: loaded item is null.");
            return;
        }

        if (item.prefab == null)
        {
            Debug.LogWarning("FurniturePlacementManager: loaded item has no prefab: " + item.itemName);
            return;
        }

        Vector3 safeScale = scale.sqrMagnitude > 0f ? scale : item.defaultScale;
        AddFurnitureInstance(item, position, Quaternion.Euler(0f, rotY, 0f), safeScale);
    }

    private GameObject AddFurnitureInstance(FurnitureItemData item, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        GameObject instance = Instantiate(item.prefab, position, rotation);
        instance.name = "Placed_" + item.itemName;
        instance.transform.localScale = scale;
        EnsureSelectableCollider(instance);

        placedFurniture.Add(instance);
        lastPlacedFurniture = instance;
        return instance;
    }

    public bool TrySelectPlacedFurnitureAtScreenPoint(Vector2 screenPoint)
    {
        Camera cam = placementCamera != null ? placementCamera : Camera.main;
        if (cam == null)
        {
            return false;
        }

        Ray ray = cam.ScreenPointToRay(screenPoint);
        if (!Physics.Raycast(ray, out RaycastHit hit, 100f, placedFurnitureLayerMask))
        {
            return false;
        }

        GameObject placedRoot = GetPlacedFurnitureRoot(hit.transform);
        if (placedRoot == null)
        {
            return false;
        }

        SetSelectedFurniture(placedRoot);
        return true;
    }

    public void DeleteSelectedFurniture()
    {
        GameObject target = selectedPlacedFurniture != null ? selectedPlacedFurniture : lastPlacedFurniture;
        if (target == null)
        {
            Debug.LogWarning("FurniturePlacementManager: no selected furniture to delete.");
            return;
        }

        placedFurniture.Remove(target);
        if (lastPlacedFurniture == target)
        {
            lastPlacedFurniture = null;
        }

        if (selectedPlacedFurniture == target)
        {
            selectedPlacedFurniture = null;
        }

        GameObject toDelete = target;
        selectedPlacedFurniture = null;
        Destroy(toDelete);
        Debug.Log("Deleted selected furniture.");
    }

    public void RotateLastFurniture()
    {
        RotateSelectedFurniture();
    }

    public void RotateSelectedFurniture()
    {
        GameObject target = selectedPlacedFurniture != null ? selectedPlacedFurniture : lastPlacedFurniture;
        if (target == null)
        {
            Debug.LogWarning("FurniturePlacementManager: no furniture to rotate.");
            return;
        }

        target.transform.Rotate(0f, rotateStepDegrees, 0f);
        Debug.Log("Rotated furniture: " + target.name);
    }

    public void ScaleSelectedFurniture(float signedStep)
    {
        GameObject target = selectedPlacedFurniture != null ? selectedPlacedFurniture : lastPlacedFurniture;
        if (target == null)
        {
            Debug.LogWarning("FurniturePlacementManager: no selected furniture to scale.");
            return;
        }

        float uniformScale = target.transform.localScale.x + signedStep;
        uniformScale = Mathf.Clamp(uniformScale, minUniformScale, maxUniformScale);
        target.transform.localScale = Vector3.one * uniformScale;
    }

    public void IncreaseSelectedFurnitureScale()
    {
        ScaleSelectedFurniture(scaleStep);
    }

    public void DecreaseSelectedFurnitureScale()
    {
        ScaleSelectedFurniture(-scaleStep);
    }

    public void ClearPlacedFurniture()
    {
        DestroySelectionVisual();
        for (int i = placedFurniture.Count - 1; i >= 0; i--)
        {
            if (placedFurniture[i] != null)
            {
                Destroy(placedFurniture[i]);
            }
        }

        placedFurniture.Clear();
        lastPlacedFurniture = null;
        selectedPlacedFurniture = null;

        Debug.Log("Placed furniture cleared.");
    }

    private Vector3 GetNextPlacementPosition()
    {
        Vector3 basePosition = spawnPoint != null ? spawnPoint.position : fallbackSpawnPosition;
        int safePlacementsPerRow = Mathf.Max(1, placementsPerRow);
        int activeCount = CountActivePlacedFurniture();
        int column = activeCount % safePlacementsPerRow;
        int row = activeCount / safePlacementsPerRow;
        Vector3 offset = new Vector3(column * placementSpacing, 0f, row * rowSpacing);

        return basePosition + offset;
    }

    private int CountActivePlacedFurniture()
    {
        int count = 0;

        for (int i = placedFurniture.Count - 1; i >= 0; i--)
        {
            if (placedFurniture[i] == null)
            {
                placedFurniture.RemoveAt(i);
                continue;
            }

            count++;
        }

        return count;
    }

    private GameObject GetPlacedFurnitureRoot(Transform hitTransform)
    {
        Transform current = hitTransform;
        while (current != null)
        {
            if (placedFurniture.Contains(current.gameObject))
            {
                return current.gameObject;
            }

            current = current.parent;
        }

        return null;
    }

    private void SetSelectedFurniture(GameObject target)
    {
        if (selectedPlacedFurniture == target)
        {
            return;
        }

        DestroySelectionVisual();
        selectedPlacedFurniture = target;
        lastPlacedFurniture = target;
        CreateSelectionVisual(target);
        Debug.Log("Selected placed furniture: " + selectedPlacedFurniture.name);
    }

    private void ClearSelectedFurniture()
    {
        DestroySelectionVisual();
        selectedPlacedFurniture = null;
        isDraggingSelectedFurniture = false;
        isDraggingARFurniture = false;
    }

    private void CreateSelectionVisual(GameObject target)
    {
        DestroySelectionVisual();

        selectionVisual = new GameObject("SelectionVisual");
        selectionVisual.transform.SetParent(target.transform, false);
        selectionVisual.transform.localPosition = new Vector3(0f, 0.02f, 0f);

        MeshFilter meshFilter = selectionVisual.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = selectionVisual.AddComponent<MeshRenderer>();

        Mesh mesh = new Mesh();
        mesh.name = "SelectionQuadMesh";
        mesh.vertices = new Vector3[]
        {
            new Vector3(-0.5f, 0f, -0.5f),
            new Vector3(0.5f, 0f, -0.5f),
            new Vector3(-0.5f, 0f, 0.5f),
            new Vector3(0.5f, 0f, 0.5f)
        };
        mesh.triangles = new int[] { 0, 2, 1, 2, 3, 1 };
        mesh.normals = new Vector3[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
        mesh.uv = new Vector2[]
        {
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0f, 1f), new Vector2(1f, 1f)
        };
        meshFilter.mesh = mesh;

        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if (mat.shader == null)
            mat = new Material(Shader.Find("Standard"));
        mat.color = new Color(1f, 0.84f, 0f, 0.35f);
        mat.SetFloat("_Surface", 1f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = 3000;
        meshRenderer.material = mat;

        Renderer targetRenderer = target.GetComponentInChildren<Renderer>();
        if (targetRenderer != null)
        {
            float size = Mathf.Max(targetRenderer.bounds.size.x, targetRenderer.bounds.size.z) * 1.0f;
            size = Mathf.Clamp(size, 0.5f, 2.5f);
            Vector3 localScale = target.transform.lossyScale;
            selectionVisual.transform.localScale = new Vector3(
                Mathf.Abs(localScale.x) > 0.001f ? size / localScale.x : 1f,
                1f,
                Mathf.Abs(localScale.z) > 0.001f ? size / localScale.z : 1f);
        }
        else
        {
            selectionVisual.transform.localScale = new Vector3(0.7f, 1f, 0.7f);
        }
    }

    private void DestroySelectionVisual()
    {
        if (selectionVisual != null)
        {
            Destroy(selectionVisual);
            selectionVisual = null;
        }
    }

    private void BeginSelectedFurnitureDrag()
    {
        if (selectedPlacedFurniture == null)
        {
            return;
        }

        isDraggingSelectedFurniture = true;
        selectedDragPlane = new Plane(Vector3.up, selectedPlacedFurniture.transform.position);
    }

    private Camera GetPlacementCamera()
    {
        if (placementCamera != null)
        {
            return placementCamera;
        }

        placementCamera = Camera.main;
        return placementCamera;
    }

    private bool TryGetFloorHitPosition(Ray ray, out Vector3 hitPoint)
    {
        if (Physics.Raycast(ray, out RaycastHit floorHit, moveRayDistance, floorLayerMask))
        {
            hitPoint = floorHit.point;
            return true;
        }

        hitPoint = Vector3.zero;
        return false;
    }

    private bool TryMoveSelectedFurnitureToScreenPoint(Vector2 screenPoint)
    {
        GameObject target = selectedPlacedFurniture != null ? selectedPlacedFurniture : lastPlacedFurniture;
        if (target == null)
        {
            return false;
        }

        Camera cam = GetPlacementCamera();
        if (cam == null)
        {
            return false;
        }

        Ray ray = cam.ScreenPointToRay(screenPoint);
        if (!TryGetFloorHitPosition(ray, out Vector3 floorPoint))
        {
            return false;
        }

        target.transform.position = floorPoint;
        SetSelectedFurniture(target);
        return true;
    }

    private bool TryMoveSelectedARFurnitureToScreenPoint(Vector2 screenPoint)
    {
        if (selectedPlacedFurniture == null)
        {
            return false;
        }

        if (arRaycastManager != null &&
            arRaycastManager.Raycast(screenPoint, arHits, TrackableType.PlaneWithinPolygon))
        {
            MoveSelectedARFurniture(arHits[0].pose.position);
            return true;
        }

        Camera cam = GetPlacementCamera();
        if (cam == null)
        {
            return false;
        }

        Ray ray = cam.ScreenPointToRay(screenPoint);
        Plane dragPlane = new Plane(Vector3.up, selectedPlacedFurniture.transform.position);
        if (dragPlane.Raycast(ray, out float enter))
        {
            MoveSelectedARFurniture(ray.GetPoint(enter));
            return true;
        }

        return false;
    }

    private void MoveSelectedARFurniture(Vector3 position)
    {
        if (selectedPlacedFurniture == null)
        {
            return;
        }

        RemoveAnchor(selectedPlacedFurniture);
        selectedPlacedFurniture.transform.position = position;
        TryAnchorPlacedFurniture(selectedPlacedFurniture);
    }

    private bool IsPointerOverBlockingUI(Vector2 screenPoint, int pointerId)
    {
        if (IsScreenPointInsideKnownUIBlocker(screenPoint))
        {
            return true;
        }

        return IsPointerOverAnyCanvasGraphic(screenPoint);
    }

    private bool IsPointerBlockedByUI(Vector2 screenPoint, int pointerId)
    {
        return IsPointerOverBlockingUI(screenPoint, pointerId);
    }

    private static bool IsScreenPointInsideKnownUIBlocker(Vector2 screenPoint)
    {
        if (screenPoint.y <= Screen.height * 0.14f)
        {
            return true;
        }

        string[] blockerNames =
        {
            "PanelGuardados",
            "PanelFurniture",
            "PanelColors",
            "RuntimeFurniturePanel",
            "RuntimeColorPanel"
        };

        foreach (string blockerName in blockerNames)
        {
            GameObject blocker = GameObject.Find(blockerName);
            if (blocker == null || !blocker.activeInHierarchy)
            {
                continue;
            }

            RectTransform rectTransform = blocker.GetComponent<RectTransform>();
            if (rectTransform != null &&
                IsReasonablePanelBlocker(rectTransform) &&
                RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPoint))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPointerOverAnyCanvasGraphic(Vector2 screenPoint)
    {
        if (IsScreenPointOverAnySelectable(screenPoint))
        {
            return true;
        }

        if (EventSystem.current == null)
        {
            return false;
        }

        PointerEventData eventData = new PointerEventData(EventSystem.current)
        {
            position = screenPoint
        };

        GraphicRaycaster[] raycasters = FindObjectsOfType<GraphicRaycaster>();
        foreach (GraphicRaycaster raycaster in raycasters)
        {
            if (raycaster == null || !raycaster.isActiveAndEnabled)
            {
                continue;
            }

            List<RaycastResult> results = new List<RaycastResult>();
            raycaster.Raycast(eventData, results);
            if (HasBlockingUIResult(results))
            {
                return true;
            }
        }

        List<RaycastResult> eventSystemResults = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, eventSystemResults);
        return HasBlockingUIResult(eventSystemResults);
    }

    private static bool IsScreenPointOverAnySelectable(Vector2 screenPoint)
    {
        Selectable[] selectables = FindObjectsOfType<Selectable>();
        foreach (Selectable selectable in selectables)
        {
            if (selectable == null || !selectable.IsActive() || !selectable.interactable)
            {
                continue;
            }

            RectTransform rectTransform = selectable.GetComponent<RectTransform>();
            if (rectTransform != null && RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPoint))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasBlockingUIResult(List<RaycastResult> results)
    {
        for (int i = 0; i < results.Count; i++)
        {
            GameObject hitObject = results[i].gameObject;
            if (hitObject == null)
            {
                continue;
            }

            if (hitObject.GetComponentInParent<Selectable>() != null)
            {
                return true;
            }

            string objectName = hitObject.name.ToLowerInvariant();
            if (objectName.Contains("button") ||
                objectName.Contains("btn") ||
                objectName.Contains("panel"))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsReasonablePanelBlocker(RectTransform rectTransform)
    {
        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);
        float height = Mathf.Abs(corners[1].y - corners[0].y);
        float width = Mathf.Abs(corners[2].x - corners[1].x);

        return height <= Screen.height * 0.7f && width <= Screen.width * 1.15f;
    }

    private void TryAnchorPlacedFurniture(GameObject placed)
    {
        if (!anchorARPlacedFurniture || placed == null || !UsesARTapPlacement)
        {
            return;
        }

        if (placed.GetComponent<ARAnchor>() == null)
        {
            placed.AddComponent<ARAnchor>();
        }
    }

    private static void RemoveAnchor(GameObject target)
    {
        ARAnchor anchor = target.GetComponent<ARAnchor>();
        if (anchor != null)
        {
            Destroy(anchor);
        }
    }

    private static void EnsureSelectableCollider(GameObject instance)
    {
        if (instance == null || instance.GetComponentInChildren<Collider>() != null)
        {
            return;
        }

        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        BoxCollider collider = instance.AddComponent<BoxCollider>();
        collider.center = instance.transform.InverseTransformPoint(bounds.center);

        Vector3 scale = instance.transform.lossyScale;
        collider.size = new Vector3(
            SafeDivide(bounds.size.x, scale.x),
            SafeDivide(bounds.size.y, scale.y),
            SafeDivide(bounds.size.z, scale.z));
    }

    private static float SafeDivide(float value, float divisor)
    {
        return Mathf.Abs(divisor) > 0.0001f ? value / Mathf.Abs(divisor) : value;
    }
}
