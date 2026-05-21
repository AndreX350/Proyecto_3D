using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
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

    private readonly List<GameObject> placedFurniture = new List<GameObject>();
    private readonly List<ARRaycastHit> arHits = new List<ARRaycastHit>();
    private GameObject lastPlacedFurniture;
    private GameObject selectedPlacedFurniture;
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

        float safeScale = scale > 0f ? scale : item.defaultScale.x;
        AddFurnitureInstance(item, position, Quaternion.Euler(0f, rotY, 0f), Vector3.one * safeScale);
    }

    private void AddFurnitureInstance(FurnitureItemData item, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        GameObject instance = Instantiate(item.prefab, position, rotation);
        instance.name = "Placed_" + item.itemName;
        instance.transform.localScale = scale;

        placedFurniture.Add(instance);
        lastPlacedFurniture = instance;
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
        if (selectedPlacedFurniture == null)
        {
            Debug.LogWarning("FurniturePlacementManager: no selected furniture to delete.");
            return;
        }

        placedFurniture.Remove(selectedPlacedFurniture);
        if (lastPlacedFurniture == selectedPlacedFurniture)
        {
            lastPlacedFurniture = null;
        }

        GameObject toDelete = selectedPlacedFurniture;
        selectedPlacedFurniture = null;
        Destroy(toDelete);
        Debug.Log("Deleted selected furniture.");
    }

    public void RotateLastFurniture()
    {
        if (lastPlacedFurniture == null)
        {
            Debug.LogWarning("FurniturePlacementManager: no furniture to rotate.");
            return;
        }

        lastPlacedFurniture.transform.Rotate(0f, 45f, 0f);
        Debug.Log("Rotated furniture: " + lastPlacedFurniture.name);
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
        Debug.Log("Selected placed furniture: " + selectedPlacedFurniture.name);
    }

    private void ClearSelectedFurniture()
    {
        selectedPlacedFurniture = null;
    }
}
