using System.Collections.Generic;
using UnityEngine;

public class RoomColorManager : MonoBehaviour
{
    [SerializeField]
    private List<Renderer> wallRenderers = new List<Renderer>();

    private Color currentWallColor = Color.white;
    private bool hasCurrentWallColor;

    private static readonly HashSet<string> AllowedWallNames = new HashSet<string>
    {
        "wall_back",
        "wall_left",
        "wall_right"
    };

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

        currentWallColor = color;
        hasCurrentWallColor = true;

        Debug.Log("Color de pared aplicado.");
    }

    public bool TryGetCurrentWallColor(out Color color)
    {
        if (hasCurrentWallColor)
        {
            color = currentWallColor;
            return true;
        }

        if (wallRenderers.Count == 0)
        {
            FindWallsInScene();
        }

        foreach (Renderer wallRenderer in wallRenderers)
        {
            if (wallRenderer == null)
            {
                continue;
            }

            Material wallMaterial = wallRenderer.sharedMaterial != null
                ? wallRenderer.sharedMaterial
                : wallRenderer.material;

            if (wallMaterial.HasProperty("_BaseColor"))
            {
                color = wallMaterial.GetColor("_BaseColor");
                return true;
            }

            color = wallMaterial.color;
            return true;
        }

        color = Color.white;
        return false;
    }

    private void FindWallsInScene()
    {
        wallRenderers.Clear();

        Renderer[] renderers = FindObjectsOfType<Renderer>();
        foreach (Renderer sceneRenderer in renderers)
        {
            string objectName = sceneRenderer.gameObject.name.ToLowerInvariant();
            bool looksLikeWall = objectName.Contains("wall") || objectName.Contains("pared");
            bool isAllowedWall = AllowedWallNames.Contains(objectName);
            if (looksLikeWall && isAllowedWall)
            {
                wallRenderers.Add(sceneRenderer);
            }
        }
    }
}
