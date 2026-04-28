using System.Collections.Generic;
using UnityEngine;

public class FurniturePlacementManager : MonoBehaviour
{
    [Header("Placement")]
    [SerializeField]
    private Transform spawnPoint;

    [SerializeField]
    private Vector3 fallbackSpawnPosition = new Vector3(0f, 0f, 0.8f);

    [SerializeField]
    private float placementSpacing = 0.8f;

    [Header("Runtime")]
    [SerializeField]
    private FurnitureItemData selectedFurniture;

    private readonly List<GameObject> placedFurniture = new List<GameObject>();
    private GameObject lastPlacedFurniture;

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
        GameObject instance = Instantiate(item.prefab, position, Quaternion.identity);
        instance.name = "Placed_" + item.itemName;
        instance.transform.localScale = item.defaultScale;

        placedFurniture.Add(instance);
        lastPlacedFurniture = instance;

        Debug.Log("Placed furniture: " + item.itemName);
    }

    public void RotateLastFurniture()
    {
        if (lastPlacedFurniture == null)
        {
            Debug.LogWarning("FurniturePlacementManager: no furniture to rotate.");
            return;
        }

        lastPlacedFurniture.transform.Rotate(0f, 45f, 0f);
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
        Vector3 offset = new Vector3(placedFurniture.Count * placementSpacing, 0f, 0f);

        return basePosition + offset;
    }
}
