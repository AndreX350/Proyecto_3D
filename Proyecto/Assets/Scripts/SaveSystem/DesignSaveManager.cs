using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DesignSaveManager : MonoBehaviour
{
    [SerializeField]
    private string fileNamePrefix = "decorar_design";

    private const string FileExtension = ".json";
    private const string RoomDemoSceneName = "RoomDemo";

    private static string pendingRoomDemoLoadPath;

    public string SaveDirectory => Application.persistentDataPath;

    public string SavePath => Path.Combine(SaveDirectory, fileNamePrefix + FileExtension);

    public string SaveDesign(IReadOnlyList<GameObject> placed, RoomColorManager roomColorManager = null)
    {
        DesignSaveData saveData = new DesignSaveData();
        saveData.savedAt = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        saveData.sourceScene = SceneManager.GetActiveScene().name;

        if (roomColorManager != null && roomColorManager.TryGetCurrentWallColor(out Color wallColor))
        {
            saveData.hasWallColor = true;
            saveData.wallColorR = wallColor.r;
            saveData.wallColorG = wallColor.g;
            saveData.wallColorB = wallColor.b;
            saveData.wallColorA = wallColor.a;
        }

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
        string savePath = GetUniqueSavePath(saveData.sourceScene);

        Directory.CreateDirectory(SaveDirectory);
        File.WriteAllText(savePath, json);
        Debug.Log("DesignSaveManager: diseno guardado en " + savePath);

        return savePath;
    }

    public static string[] GetSavedDesignFiles()
    {
        return GetSavedDesignFiles(null);
    }

    public static string[] GetSavedDesignFiles(string sourceScene)
    {
        string directory = Application.persistentDataPath;
        if (!Directory.Exists(directory))
        {
            return new string[0];
        }

        string[] allFiles = Directory.GetFiles(directory, "decorar_design*.json");
        List<string> files = new List<string>();
        for (int i = 0; i < allFiles.Length; i++)
        {
            if (string.IsNullOrEmpty(sourceScene) || IsDesignFromSource(allFiles[i], sourceScene))
            {
                files.Add(allFiles[i]);
            }
        }

        files.Sort((left, right) =>
            File.GetLastWriteTime(right).CompareTo(File.GetLastWriteTime(left)));

        return files.ToArray();
    }

    public static void LoadRoomDemoDesign(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            Debug.LogWarning("DesignSaveManager: no se encontro el guardado " + filePath);
            return;
        }

        pendingRoomDemoLoadPath = filePath;
        SceneManager.LoadScene(RoomDemoSceneName);
    }

    public static void TryLoadPendingRoomDemoDesign(
        FurnitureCatalog catalog,
        FurniturePlacementManager placementManager,
        RoomColorManager roomColorManager)
    {
        if (string.IsNullOrEmpty(pendingRoomDemoLoadPath))
        {
            return;
        }

        string filePath = pendingRoomDemoLoadPath;
        pendingRoomDemoLoadPath = null;

        LoadDesignIntoScene(filePath, catalog, placementManager, roomColorManager);
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

    private string GetUniqueSavePath(string sourceScene)
    {
        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        string sourceSuffix = string.IsNullOrEmpty(sourceScene) ? "unknown" : sourceScene.ToLowerInvariant();
        string basePath = Path.Combine(SaveDirectory, fileNamePrefix + "_" + sourceSuffix + "_" + timestamp);
        string savePath = basePath + FileExtension;
        int suffix = 1;

        while (File.Exists(savePath))
        {
            savePath = basePath + "_" + suffix + FileExtension;
            suffix++;
        }

        return savePath;
    }

    private static bool IsDesignFromSource(string filePath, string sourceScene)
    {
        DesignSaveData saveData = ReadDesignData(filePath);
        if (saveData == null)
        {
            return false;
        }

        if (string.IsNullOrEmpty(saveData.sourceScene))
        {
            return sourceScene == RoomDemoSceneName;
        }

        return saveData.sourceScene == sourceScene;
    }

    private static void LoadDesignIntoScene(
        string filePath,
        FurnitureCatalog catalog,
        FurniturePlacementManager placementManager,
        RoomColorManager roomColorManager)
    {
        if (catalog == null || placementManager == null)
        {
            Debug.LogWarning("DesignSaveManager: faltan referencias para cargar el guardado.");
            return;
        }

        DesignSaveData saveData = ReadDesignData(filePath);
        if (saveData == null)
        {
            return;
        }

        placementManager.ClearPlacedFurniture();

        for (int i = 0; i < saveData.items.Count; i++)
        {
            PlacedFurnitureData savedItem = saveData.items[i];
            FurnitureItemData catalogItem = catalog.GetItemByName(savedItem.itemName);
            if (catalogItem == null)
            {
                continue;
            }

            Vector3 position = new Vector3(savedItem.posX, savedItem.posY, savedItem.posZ);
            placementManager.PlaceLoadedFurniture(catalogItem, position, savedItem.rotY, savedItem.scale);
        }

        if (saveData.hasWallColor && roomColorManager != null)
        {
            Color wallColor = new Color(
                saveData.wallColorR,
                saveData.wallColorG,
                saveData.wallColorB,
                saveData.wallColorA);
            roomColorManager.ApplyWallColor(wallColor);
        }

        Debug.Log("DesignSaveManager: guardado cargado desde " + filePath);
    }

    private static DesignSaveData ReadDesignData(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            return null;
        }

        string json = File.ReadAllText(filePath);
        return JsonUtility.FromJson<DesignSaveData>(json);
    }
}
