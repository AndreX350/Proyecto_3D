using System;
using System.Collections.Generic;

[Serializable]
public class DesignSaveData
{
    public string savedAt;
    public string sourceScene;
    public bool hasWallColor;
    public float wallColorR;
    public float wallColorG;
    public float wallColorB;
    public float wallColorA;
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
