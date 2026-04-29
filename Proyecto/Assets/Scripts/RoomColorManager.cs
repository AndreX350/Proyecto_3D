using System.Collections.Generic;
using UnityEngine;

public class RoomColorManager : MonoBehaviour
{
    [SerializeField]
    private List<Renderer> wallRenderers = new List<Renderer>();

    private void Awake()
    {
        if (wallRenderers.Count == 0)
        {
            FindWallsInScene();
        }
    }

    public void ApplyWallColor(Color color)
    {
        if (wallRenderers.Count == 0)
        {
            FindWallsInScene();
        }

        if (wallRenderers.Count == 0)
        {
            Debug.LogWarning("RoomColorManager: no se encontraron paredes para pintar.");
            return;
        }

        foreach (Renderer wallRenderer in wallRenderers)
        {
            if (wallRenderer != null)
            {
                Material wallMaterial = wallRenderer.material;
                wallMaterial.color = color;

                if (wallMaterial.HasProperty("_BaseColor"))
                {
                    wallMaterial.SetColor("_BaseColor", color);
                }
            }
        }

        Debug.Log("Color de pared aplicado.");
    }

    private void FindWallsInScene()
    {
        wallRenderers.Clear();

        Renderer[] renderers = FindObjectsOfType<Renderer>();
        foreach (Renderer sceneRenderer in renderers)
        {
            string objectName = sceneRenderer.gameObject.name.ToLowerInvariant();
            if (objectName.Contains("wall") || objectName.Contains("pared"))
            {
                wallRenderers.Add(sceneRenderer);
            }
        }
    }
}
