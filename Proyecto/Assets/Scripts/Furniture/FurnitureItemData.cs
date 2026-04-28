using UnityEngine;

[CreateAssetMenu(fileName = "SO_FurnitureItem", menuName = "DecorAR/Furniture Item")]
public class FurnitureItemData : ScriptableObject
{
    [Header("Basic Info")]
    public string itemName;
    [TextArea]
    public string description;
    public FurnitureCategory category;

    [Header("Visual Asset")]
    public GameObject prefab;
    public Sprite thumbnail;

    [Header("Approximate Size")]
    public Vector3 dimensions = Vector3.one;

    [Header("Placement")]
    public Vector3 defaultScale = Vector3.one;
    public Vector3 placementOffset = Vector3.zero;
}
