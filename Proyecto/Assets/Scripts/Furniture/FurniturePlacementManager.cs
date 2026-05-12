using System.Collections.Generic;
using UnityEngine;

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

    private readonly List<GameObject> placedFurniture = new List<GameObject>();
    private GameObject lastPlacedFurniture;
    public IReadOnlyList<GameObject> PlacedFurniture => placedFurniture;

    public void SelectFurniture(FurnitureItemData item)
    {
        selectedFurniture = item;

        if (selectedFurniture != null)
        {
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
}
