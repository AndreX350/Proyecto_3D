using System.Collections.Generic;
using UnityEngine;

public class FurnitureCatalog : MonoBehaviour
{
    [SerializeField]
    private List<FurnitureItemData> items = new List<FurnitureItemData>();

    public IReadOnlyList<FurnitureItemData> Items => items;

    public bool HasItems()
    {
        return items != null && items.Count > 0;
    }

    public FurnitureItemData GetItem(int index)
    {
        if (items == null || index < 0 || index >= items.Count)
        {
            Debug.LogWarning("FurnitureCatalog: invalid furniture index.");
            return null;
        }

        return items[index];
    }

    public FurnitureItemData GetItemByName(string itemName)
    {
        if (items == null)
        {
            return null;
        }

        foreach (FurnitureItemData item in items)
        {
            if (item != null && item.itemName == itemName)
            {
                return item;
            }
        }

        Debug.LogWarning("FurnitureCatalog: furniture not found: " + itemName);
        return null;
    }

    public List<FurnitureItemData> GetItemsByCategory(FurnitureCategory category)
    {
        List<FurnitureItemData> result = new List<FurnitureItemData>();

        if (items == null)
        {
            return result;
        }

        foreach (FurnitureItemData item in items)
        {
            if (item != null && item.category == category)
            {
                result.Add(item);
            }
        }

        return result;
    }
}
