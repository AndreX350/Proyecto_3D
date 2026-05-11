using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class DesignSaveManager : MonoBehaviour
{
    [SerializeField]
    private string fileName = "decorar_design.json";

    public string SavePath => Path.Combine(Application.persistentDataPath, fileName);

    public void SaveDesign(IReadOnlyList<GameObject> placed)
    {
        DesignSaveData saveData = new DesignSaveData();

        if (placed != null)
        {
            for (int i = 0; i < placed.Count; i++)
            {
                GameObject placedObject = placed[i];
                if (placedObject == null)
                {
                    continue;
                }

                Transform t = placedObject.transform;
                PlacedFurnitureData item = new PlacedFurnitureData
                {
                    itemName = ExtractItemName(placedObject.name),
                    posX = t.position.x,
                    posY = t.position.y,
                    posZ = t.position.z,
                    rotY = t.eulerAngles.y,
                    scale = t.localScale.x
                };

                saveData.items.Add(item);
            }
        }

        string json = JsonUtility.ToJson(saveData, true);
        File.WriteAllText(SavePath, json);
        Debug.Log("DesignSaveManager: diseno guardado en " + SavePath);
    }

    private static string ExtractItemName(string objectName)
    {
        const string prefix = "Placed_";
        if (!string.IsNullOrEmpty(objectName) && objectName.StartsWith(prefix))
        {
            return objectName.Substring(prefix.Length);
        }

        return objectName;
    }
}
