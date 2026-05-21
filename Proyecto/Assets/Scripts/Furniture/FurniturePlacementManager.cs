using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class FurniturePlacementManager : MonoBehaviour
{
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

    private readonly List<GameObject> placedFurniture = new List<GameObject>();
    private readonly List<ARRaycastHit> arHits = new List<ARRaycastHit>();
    private GameObject lastPlacedFurniture;
    private GameObject selectedPlacedFurniture;
    private bool isDraggingSelectedFurniture;
    private Plane selectedDragPlane;
    public IReadOnlyList<GameObject> PlacedFurniture => placedFurniture;
    public bool UsesARTapPlacement => enableARPlacement && arRaycastManager != null;

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
        if (UsesARTapPlacement)
        {
            UpdateARPlacementInput();
            return;
        }

        UpdateRoomEditingInput();
    }

    private void UpdateARPlacementInput()
    {
        if (!enableARPlacement || arRaycastManager == null)
        {
            return;
        }

        if (Input.touchCount <= 0)
        {
            return;
        }

        Touch touch = Input.GetTouch(0);
        if (touch.phase != TouchPhase.Began)
        {
            return;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.fingerId))
        {
            return;
        }

        if (TrySelectPlacedFurnitureAtScreenPoint(touch.position))
        {
            return;
        }

        TryPlaceSelectedFurnitureAtScreenPoint(touch.position);
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
                if (blockRoomEditingWhenPointerOverUI && IsPointerOverBlockingUI(touch.position, touch.fingerId))
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
            if (blockRoomEditingWhenPointerOverUI && IsPointerOverBlockingUI(Input.mousePosition, -1))
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
        if (blockRoomEditingWhenPointerOverUI && IsPointerOverBlockingUI(screenPoint, pointerId))
        {
            isDraggingSelectedFurniture = false;
            return;
        }

        if (TrySelectPlacedFurnitureAtScreenPoint(screenPoint))
        {
            BeginSelectedFurnitureDrag();
            return;
        }

        if (TryMoveSelectedFurnitureToScreenPoint(screenPoint))
        {
            BeginSelectedFurnitureDrag();
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
        PlaceFurnitureAt(selectedFurniture, position, rotation);
        return true;
    }

    public void PlaceFurnitureAt(FurnitureItemData item, Vector3 position, Quaternion rotation)
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

        AddFurnitureInstance(item, position, rotation, item.defaultScale);
        Debug.Log("Placed furniture at position: " + item.itemName);
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

    private void AddFurnitureInstance(FurnitureItemData item, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        GameObject instance = Instantiate(item.prefab, position, rotation);
        instance.name = "Placed_" + item.itemName;
        instance.transform.localScale = scale;

        placedFurniture.Add(instance);
        lastPlacedFurniture = instance;
        SetSelectedFurniture(instance);
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

        selectedPlacedFurniture = target;
        lastPlacedFurniture = target;
        Debug.Log("Selected placed furniture: " + selectedPlacedFurniture.name);
    }

    private void ClearSelectedFurniture()
    {
        selectedPlacedFurniture = null;
        isDraggingSelectedFurniture = false;
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

    private bool IsPointerOverBlockingUI(Vector2 screenPoint, int pointerId)
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        bool overUI = pointerId >= 0
            ? EventSystem.current.IsPointerOverGameObject(pointerId)
            : EventSystem.current.IsPointerOverGameObject();
        if (!overUI)
        {
            return false;
        }

        PointerEventData eventData = new PointerEventData(EventSystem.current)
        {
            position = screenPoint
        };

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        return results.Count > 0;
    }
}
