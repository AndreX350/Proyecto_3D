using System;
using System.Collections.Generic;

[Serializable]
public class DesignSaveData
{
    public List<PlacedFurnitureData> items = new List<PlacedFurnitureData>();
}

[Serializable]
public class PlacedFurnitureData
{
    public string itemName;
    public float posX;
    public float posY;
    public float posZ;
    public float rotY;
    public float scale;
}
