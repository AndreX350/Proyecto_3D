using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class FurniturePlacementManager : MonoBehaviour
{
    private static float blockWorldInputUntil;
    private const string PlacedFurnitureLayerName = "PlacedFurniture";

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
    private ARPlaneManager arPlaneManager;

    [SerializeField]
    private ARAnchorManager arAnchorManager;

    [SerializeField]
    private bool anchorARPlacedFurniture = true;

    [SerializeField]
    private bool showPlacementReticle = true;

    [SerializeField]
    private float minimumHorizontalPlaneArea = 0.04f;

    [SerializeField]
    private float estimatedSurfaceUpDotThreshold = 0.65f;

    [SerializeField]
    private bool useGridSnapForAR = true;

    [SerializeField]
    private float arGridSize = 0.05f;

    [SerializeField]
    private bool snapARFurnitureWhileDragging = false;

    [SerializeField]
    private float maxARPlacementDistance = 10f;

    [SerializeField]
    private bool preventPlacementOnCollision = true;

    [SerializeField]
    private Camera placementCamera;

    [SerializeField]
    private LayerMask placedFurnitureLayerMask = 1 << 6;

    [SerializeField]
    private RoomColorManager roomColorManager;

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
    private GameObject placementReticle;
    private Material placementReticleMaterial;
    private Vector2 pendingWallRetryPoint;
    private float pendingWallRetryTime;
    private int pendingWallRetryCount;
    private ARPlane activeARDragPlane;
    private Pose activeARDragPose;
    private bool hasActiveARDragPose;
    public IReadOnlyList<GameObject> PlacedFurniture
    {
        get
        {
            RefreshPlacedFurnitureList();
            return placedFurniture;
        }
    }
    public bool UsesARTapPlacement => enableARPlacement && arRaycastManager != null;
    public bool IsAREnabled => enableARPlacement;
    public bool HasARRaycastManager => arRaycastManager != null;
    public bool HasARPlaneManager => arPlaneManager != null;
    public bool HasARAnchorManager => arAnchorManager != null;
    public bool HasSelectedFurnitureForAR => selectedFurniture != null;

    public static void BlockWorldInputBriefly(float seconds = 0.25f)
    {
        blockWorldInputUntil = Mathf.Max(blockWorldInputUntil, Time.unscaledTime + seconds);
    }

    private void Awake()
    {
        EnsurePlacedFurnitureLayerMask();

        if (placementCamera == null)
        {
            placementCamera = Camera.main;
        }

        if (arRaycastManager == null)
        {
            arRaycastManager = FindObjectOfType<ARRaycastManager>();
        }

        if (arPlaneManager == null)
        {
            arPlaneManager = FindObjectOfType<ARPlaneManager>();
        }

        if (arAnchorManager == null)
        {
            arAnchorManager = FindObjectOfType<ARAnchorManager>();
        }

        ARSession arSession = FindObjectOfType<ARSession>();
        if (arSession != null)
        {
            arSession.enabled = true;
            arSession.requestedTrackingMode = TrackingMode.PositionAndRotation;
        }

        if (roomColorManager == null)
        {
            roomColorManager = FindObjectOfType<RoomColorManager>();
        }
    }

    private void Update()
    {
        RefreshRuntimeReferences();

        if (Time.unscaledTime < blockWorldInputUntil)
        {
            isDraggingSelectedFurniture = false;
            FinishARFurnitureDrag();
            UpdatePlacementReticle(false, default);
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

        if (arPlaneManager == null)
        {
            arPlaneManager = FindObjectOfType<ARPlaneManager>();
        }

        if (arAnchorManager == null)
        {
            arAnchorManager = FindObjectOfType<ARAnchorManager>();
        }

        if (roomColorManager == null)
        {
            roomColorManager = FindObjectOfType<RoomColorManager>();
        }
    }

    private void UpdateARPlacementInput()
    {
        if (!enableARPlacement || arRaycastManager == null)
        {
            ARDiagnostics.Report("AR placement apagado o falta ARRaycastManager.");
            UpdatePlacementReticle(false, default);
            return;
        }

        ProcessPendingWallRetry();

        if (selectedFurniture != null)
        {
            UpdatePlacementReticleForScreenPoint(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
        }
        else
        {
            UpdatePlacementReticle(false, default);
        }

        if (Input.touchCount <= 0)
        {
            FinishARFurnitureDrag();
            return;
        }

        Touch touch = Input.GetTouch(0);
        if (blockPlacementWhenPointerOverUI && IsPointerOverBlockingUI(touch.position, touch.fingerId))
        {
            ARDiagnostics.Report("Tap bloqueado por UI.");
            FinishARFurnitureDrag();
            return;
        }

        if (touch.phase == TouchPhase.Began)
        {
            if (TrySelectPlacedFurnitureAtScreenPoint(touch.position))
            {
                BeginARFurnitureDrag();
                selectedFurniture = null;
                ARDiagnostics.Report("Mueble AR seleccionado para mover.");
                return;
            }

            if (TryPlaceSelectedFurnitureAtScreenPoint(touch.position))
            {
                ARDiagnostics.Report("Mueble colocado en plano horizontal.");
                return;
            }

            if (TrySelectWallAtScreenPoint(touch.position))
            {
                ARDiagnostics.Report("Pared AR vertical seleccionada.");
            }
            else
            {
                ARDiagnostics.Report("Tap sin hit util: ni piso horizontal ni pared vertical.");
                pendingWallRetryPoint = touch.position;
                pendingWallRetryTime = Time.unscaledTime + 0.5f;
                pendingWallRetryCount = 1;
            }
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
            FinishARFurnitureDrag();
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
                if (blockRoomEditingWhenPointerOverUI && IsPointerBlockedByUI(touch.position, touch.fingerId))
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
            if (blockRoomEditingWhenPointerOverUI && IsPointerBlockedByUI(Input.mousePosition, -1))
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

        Debug.LogWarning("FurniturePlacementManager: apunta al piso detectado para colocar muebles en AR.");
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
            ARDiagnostics.Report("No se puede colocar: AR placement desactivado o falta ARRaycastManager.");
            return false;
        }

        if (selectedFurniture == null)
        {
            ARDiagnostics.Report("No se puede colocar: no hay mueble seleccionado.");
            return false;
        }

        if (!TryGetBestHorizontalSurfaceHit(screenPoint, out ARRaycastHit horizontalHit, out ARPlane hitPlane))
        {
            ARDiagnostics.Report("No se detecto piso horizontal util en el tap.");
            return false;
        }

        Pose hitPose = horizontalHit.pose;
        Vector3 position = new Vector3(
            hitPose.position.x + selectedFurniture.placementOffset.x,
            hitPose.position.y,
            hitPose.position.z + selectedFurniture.placementOffset.z);
        if (!IsWithinPlacementDistance(position))
        {
            ARDiagnostics.Report("No se puede colocar: demasiado lejos de la camara.");
            return false;
        }

        if (useGridSnapForAR)
        {
            position = SnapPositionToGrid(position);
        }

        Quaternion rotation = Quaternion.Euler(0f, hitPose.rotation.eulerAngles.y, 0f);
        GameObject placed = PlaceFurnitureAt(selectedFurniture, position, rotation);
        if (placed == null)
        {
            return false;
        }

        if (preventPlacementOnCollision && IsPlacementColliding(placed))
        {
            ARDiagnostics.Report("No se coloco: colision con otro mueble.");
            DestroyPlacedFurniture(placed);
            placedFurniture.Remove(placed);
            if (lastPlacedFurniture == placed)
            {
                lastPlacedFurniture = null;
            }
            return false;
        }

        TryAnchorPlacedFurniture(placed, hitPlane, new Pose(position, rotation));
        selectedFurniture = null;
        SetSelectedFurniture(placed);
        UpdatePlacementReticle(false, default);
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
        ApplyPlacedFurnitureLayer(instance);
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
        RefreshPlacedFurnitureList();

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

        selectedPlacedFurniture = null;
        DestroyPlacedFurniture(target);
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
        RefreshPlacedFurnitureList();

        for (int i = placedFurniture.Count - 1; i >= 0; i--)
        {
            if (placedFurniture[i] != null)
            {
                DestroyPlacedFurniture(placedFurniture[i]);
            }
        }

        placedFurniture.Clear();
        lastPlacedFurniture = null;
        selectedPlacedFurniture = null;

        Debug.Log("Placed furniture cleared.");
    }

    public void RefreshPlacedFurnitureList()
    {
        for (int i = placedFurniture.Count - 1; i >= 0; i--)
        {
            if (placedFurniture[i] == null)
            {
                placedFurniture.RemoveAt(i);
            }
        }

        if (selectedPlacedFurniture == null || !placedFurniture.Contains(selectedPlacedFurniture))
        {
            selectedPlacedFurniture = null;
        }

        if (lastPlacedFurniture == null || !placedFurniture.Contains(lastPlacedFurniture))
        {
            lastPlacedFurniture = placedFurniture.Count > 0 ? placedFurniture[placedFurniture.Count - 1] : null;
        }
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
        FinishARFurnitureDrag();
        DestroySelectionVisual();
        selectedPlacedFurniture = null;
        isDraggingSelectedFurniture = false;
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
            TryGetBestHorizontalSurfaceHit(screenPoint, out ARRaycastHit horizontalHit, out ARPlane hitPlane))
        {
            MoveSelectedARFurniture(horizontalHit.pose.position, hitPlane);
            return true;
        }

        return false;
    }

    private void MoveSelectedARFurniture(Vector3 position, ARPlane hitPlane)
    {
        if (selectedPlacedFurniture == null)
        {
            return;
        }

        if (useGridSnapForAR && snapARFurnitureWhileDragging)
        {
            position = SnapPositionToGrid(position);
        }

        if (!IsWithinPlacementDistance(position))
        {
            ARDiagnostics.Report("Movimiento AR cancelado: destino lejos de la camara.");
            return;
        }

        Quaternion rotation = selectedPlacedFurniture.transform.rotation;
        Vector3 previousPosition = selectedPlacedFurniture.transform.position;
        selectedPlacedFurniture.transform.position = position;
        if (preventPlacementOnCollision && IsPlacementColliding(selectedPlacedFurniture))
        {
            selectedPlacedFurniture.transform.position = previousPosition;
            ARDiagnostics.Report("Movimiento AR cancelado: colision detectada.");
            return;
        }

        activeARDragPlane = hitPlane;
        activeARDragPose = new Pose(position, rotation);
        hasActiveARDragPose = true;
    }

    private void BeginARFurnitureDrag()
    {
        if (selectedPlacedFurniture == null)
        {
            return;
        }

        if (!isDraggingARFurniture)
        {
            RemoveAnchor(selectedPlacedFurniture);
        }

        activeARDragPlane = null;
        activeARDragPose = new Pose(selectedPlacedFurniture.transform.position, selectedPlacedFurniture.transform.rotation);
        hasActiveARDragPose = true;
        isDraggingARFurniture = true;
    }

    private void FinishARFurnitureDrag()
    {
        if (!isDraggingARFurniture)
        {
            return;
        }

        isDraggingARFurniture = false;

        if (selectedPlacedFurniture == null)
        {
            activeARDragPlane = null;
            hasActiveARDragPose = false;
            return;
        }

        Pose finalPose = hasActiveARDragPose
            ? activeARDragPose
            : new Pose(selectedPlacedFurniture.transform.position, selectedPlacedFurniture.transform.rotation);

        if (useGridSnapForAR && !snapARFurnitureWhileDragging)
        {
            finalPose.position = SnapPositionToGrid(finalPose.position);
            Vector3 previousPosition = selectedPlacedFurniture.transform.position;
            selectedPlacedFurniture.transform.position = finalPose.position;

            if (preventPlacementOnCollision && IsPlacementColliding(selectedPlacedFurniture))
            {
                selectedPlacedFurniture.transform.position = previousPosition;
                finalPose.position = previousPosition;
                ARDiagnostics.Report("Snap final cancelado: colision detectada.");
            }
        }

        TryAnchorPlacedFurniture(selectedPlacedFurniture, activeARDragPlane, finalPose);
        activeARDragPlane = null;
        hasActiveARDragPose = false;
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

    private bool TryGetBestHorizontalSurfaceHit(Vector2 screenPoint, out ARRaycastHit horizontalHit, out ARPlane hitPlane)
    {
        TrackableType preciseMask =
            TrackableType.PlaneWithinPolygon |
            TrackableType.PlaneWithinBounds |
            TrackableType.PlaneWithinInfinity;

        if (arRaycastManager.Raycast(screenPoint, arHits, preciseMask) &&
            TryGetHorizontalPlaneHit(out horizontalHit, out hitPlane, true))
        {
            return true;
        }

        TrackableType estimatedMask = preciseMask | TrackableType.PlaneEstimated;
        if (arRaycastManager.Raycast(screenPoint, arHits, estimatedMask) &&
            TryGetHorizontalPlaneHit(out horizontalHit, out hitPlane, false))
        {
            return true;
        }

        horizontalHit = default;
        hitPlane = null;
        return false;
    }

    private bool TryGetHorizontalPlaneHit(out ARRaycastHit horizontalHit, out ARPlane hitPlane, bool requireKnownPlane)
    {
        for (int i = 0; i < arHits.Count; i++)
        {
            ARPlane plane = arPlaneManager != null ? arPlaneManager.GetPlane(arHits[i].trackableId) : null;
            if (plane != null)
            {
                if (!IsUsableHorizontalPlane(plane))
                {
                    ARDiagnostics.Report("Plano horizontal detectado pero muy pequeno para colocar.");
                    continue;
                }

                horizontalHit = arHits[i];
                hitPlane = plane;
                return true;
            }

            if (requireKnownPlane || !IsEstimatedHorizontalHit(arHits[i]))
            {
                continue;
            }

            horizontalHit = arHits[i];
            hitPlane = null;
            return true;
        }

        horizontalHit = default;
        hitPlane = null;
        return false;
    }

    private static bool IsHorizontalPlane(ARPlane plane)
    {
        return plane.alignment == PlaneAlignment.HorizontalUp;
    }

    private bool IsUsableHorizontalPlane(ARPlane plane)
    {
        if (plane == null || !IsHorizontalPlane(plane))
        {
            return false;
        }

        Vector2 size = plane.size;
        return size.x * size.y >= minimumHorizontalPlaneArea;
    }

    private bool IsEstimatedHorizontalHit(ARRaycastHit hit)
    {
        float upDot = Vector3.Dot(hit.pose.up, Vector3.up);
        return upDot >= estimatedSurfaceUpDotThreshold;
    }

    private void UpdatePlacementReticleForScreenPoint(Vector2 screenPoint)
    {
        if (!showPlacementReticle || arRaycastManager == null)
        {
            UpdatePlacementReticle(false, default);
            return;
        }

        if (TryGetBestHorizontalSurfaceHit(screenPoint, out ARRaycastHit hit, out _))
        {
            UpdatePlacementReticle(true, hit.pose);
            return;
        }

        UpdatePlacementReticle(false, default);
    }

    private void UpdatePlacementReticle(bool visible, Pose pose)
    {
        if (!showPlacementReticle)
        {
            if (placementReticle != null)
            {
                placementReticle.SetActive(false);
            }
            return;
        }

        EnsurePlacementReticle();
        if (placementReticle == null)
        {
            return;
        }

        placementReticle.SetActive(visible);
        if (!visible)
        {
            return;
        }

        placementReticle.transform.SetPositionAndRotation(
            pose.position + Vector3.up * 0.006f,
            Quaternion.Euler(0f, pose.rotation.eulerAngles.y, 0f));
    }

    private void EnsurePlacementReticle()
    {
        if (placementReticle != null)
        {
            return;
        }

        placementReticle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        placementReticle.name = "ARPlacementReticle";
        placementReticle.transform.localScale = new Vector3(0.28f, 0.008f, 0.28f);

        Collider reticleCollider = placementReticle.GetComponent<Collider>();
        if (reticleCollider != null)
        {
            Destroy(reticleCollider);
        }

        Renderer renderer = placementReticle.GetComponent<Renderer>();
        if (renderer != null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            placementReticleMaterial = new Material(shader);
            placementReticleMaterial.color = new Color(0.18f, 0.9f, 0.55f, 0.55f);
            if (placementReticleMaterial.HasProperty("_BaseColor"))
            {
                placementReticleMaterial.SetColor("_BaseColor", placementReticleMaterial.color);
            }
            placementReticleMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            renderer.sharedMaterial = placementReticleMaterial;
        }

        placementReticle.SetActive(false);
    }

    private bool TrySelectWallAtScreenPoint(Vector2 screenPoint)
    {
        if (roomColorManager == null)
        {
            ARDiagnostics.Report("No se puede seleccionar pared: falta RoomColorManager.");
            return false;
        }

        if (roomColorManager.TrySelectWallAtScreenPoint(screenPoint))
        {
            return true;
        }

        roomColorManager.ClearSelectedWall();
        return false;
    }

    private void ProcessPendingWallRetry()
    {
        if (pendingWallRetryCount <= 0)
        {
            return;
        }

        if (Time.unscaledTime < pendingWallRetryTime)
        {
            return;
        }

        bool selected = TrySelectWallAtScreenPoint(pendingWallRetryPoint);
        if (selected)
        {
            ARDiagnostics.Report("Pared AR detectada en reintento.");
            pendingWallRetryCount = 0;
            return;
        }

        pendingWallRetryCount++;
        if (pendingWallRetryCount > 2)
        {
            pendingWallRetryCount = 0;
            return;
        }

        pendingWallRetryTime = Time.unscaledTime + 0.5f;
    }

    private void TryAnchorPlacedFurniture(GameObject placed, ARPlane plane = null, Pose? pose = null)
    {
        if (!anchorARPlacedFurniture || placed == null || !UsesARTapPlacement)
        {
            return;
        }

        RemoveAnchor(placed);

        Pose anchorPose = pose ?? new Pose(placed.transform.position, placed.transform.rotation);
        if (arAnchorManager != null && plane != null)
        {
            try
            {
                ARAnchor attachedAnchor = arAnchorManager.AttachAnchor(plane, anchorPose);
                if (attachedAnchor != null)
                {
                    placed.transform.SetParent(attachedAnchor.transform, true);
                    return;
                }
            }
            catch (System.InvalidOperationException exception)
            {
                Debug.LogWarning("FurniturePlacementManager: no se pudo adjuntar anchor al plano AR. Se usara anchor directo. " + exception.Message);
            }
        }

        if (placed.GetComponent<ARAnchor>() == null)
        {
            placed.AddComponent<ARAnchor>();
        }
    }

    private static void RemoveAnchor(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        ARAnchor anchor = target.GetComponent<ARAnchor>();
        if (anchor != null && anchor.gameObject != null)
        {
            Destroy(anchor);
            return;
        }

        Transform parent = target.transform.parent;
        if (parent == null)
        {
            return;
        }

        ARAnchor parentAnchor = parent.GetComponent<ARAnchor>();
        if (parentAnchor == null)
        {
            return;
        }

        target.transform.SetParent(null, true);
        Destroy(parent.gameObject);
    }

    private static void DestroyPlacedFurniture(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        Transform parent = target.transform.parent;
        if (parent != null && parent.GetComponent<ARAnchor>() != null)
        {
            Destroy(parent.gameObject);
            return;
        }

        Destroy(target);
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
        Vector3 colliderSize = new Vector3(
            SafeDivide(bounds.size.x, scale.x),
            SafeDivide(bounds.size.y, scale.y),
            SafeDivide(bounds.size.z, scale.z));
        collider.size = colliderSize;
        collider.center = new Vector3(collider.center.x, colliderSize.y * 0.5f, collider.center.z);
    }

    private static float SafeDivide(float value, float divisor)
    {
        return Mathf.Abs(divisor) > 0.0001f ? value / Mathf.Abs(divisor) : value;
    }

    private void EnsurePlacedFurnitureLayerMask()
    {
        int placedLayer = LayerMask.NameToLayer(PlacedFurnitureLayerName);
        if (placedLayer < 0)
        {
            return;
        }

        placedFurnitureLayerMask.value |= 1 << placedLayer;
    }

    private static void ApplyPlacedFurnitureLayer(GameObject instance)
    {
        int placedLayer = LayerMask.NameToLayer(PlacedFurnitureLayerName);
        if (placedLayer < 0 || instance == null)
        {
            return;
        }

        SetLayerRecursively(instance.transform, placedLayer);
    }

    private static void SetLayerRecursively(Transform target, int layer)
    {
        target.gameObject.layer = layer;

        for (int i = 0; i < target.childCount; i++)
        {
            SetLayerRecursively(target.GetChild(i), layer);
        }
    }

    private bool IsWithinPlacementDistance(Vector3 worldPosition)
    {
        Camera cam = GetPlacementCamera();
        if (cam == null)
        {
            return true;
        }

        return Vector3.Distance(cam.transform.position, worldPosition) <= maxARPlacementDistance;
    }

    private Vector3 SnapPositionToGrid(Vector3 worldPosition)
    {
        float safeGrid = Mathf.Max(0.01f, arGridSize);
        worldPosition.x = Mathf.Round(worldPosition.x / safeGrid) * safeGrid;
        worldPosition.z = Mathf.Round(worldPosition.z / safeGrid) * safeGrid;
        return worldPosition;
    }

    private bool IsPlacementColliding(GameObject target)
    {
        Collider[] colliders = target.GetComponentsInChildren<Collider>();
        if (colliders == null || colliders.Length == 0)
        {
            return false;
        }

        foreach (Collider collider in colliders)
        {
            if (collider == null)
            {
                continue;
            }

            Bounds bounds = collider.bounds;
            Vector3 halfExtents = bounds.extents * 0.96f;
            Collider[] overlaps = Physics.OverlapBox(bounds.center, halfExtents, collider.transform.rotation, placedFurnitureLayerMask);
            foreach (Collider overlap in overlaps)
            {
                if (overlap == null)
                {
                    continue;
                }

                if (overlap.transform.IsChildOf(target.transform))
                {
                    continue;
                }

                GameObject placedRoot = GetPlacedFurnitureRoot(overlap.transform);
                if (placedRoot != null && placedRoot != target)
                {
                    if (overlap.GetComponentInParent<ARPlane>() != null)
                    {
                        continue;
                    }

                    return true;
                }
            }
        }

        return false;
    }
}
