using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class RoomColorManager : MonoBehaviour
{
    [SerializeField]
    private List<Renderer> wallRenderers = new List<Renderer>();

    [SerializeField]
    private float arWallOverlayAlpha = 0.45f;

    [SerializeField]
    private float minimumARWallSize = 0.25f;

    private Color currentWallColor = Color.white;
    private bool hasCurrentWallColor;
    private readonly Dictionary<ARPlane, Renderer> arWallOverlays = new Dictionary<ARPlane, Renderer>();
    private readonly List<ARRaycastHit> arHits = new List<ARRaycastHit>();
    private ARRaycastManager arRaycastManager;
    private ARPlaneManager arPlaneManager;
    private ARPlane selectedARWall;

    private static readonly HashSet<string> AllowedWallNames = new HashSet<string>
    {
        "wall_back",
        "wall_left",
        "wall_right",
        "wall_front"
    };

    private void Awake()
    {
        ResolveARManagers();

        if (wallRenderers.Count == 0)
        {
            FindWallsInScene();
        }
    }

    private void Update()
    {
        if (!hasCurrentWallColor)
        {
            return;
        }

        ResolveARManagers();

        int previousCount = wallRenderers.Count;
        AddDetectedARWalls();
        if (selectedARWall != null)
        {
            return;
        }

        for (int i = previousCount; i < wallRenderers.Count; i++)
        {
            ApplyColorToRenderer(wallRenderers[i], currentWallColor);
        }
    }

    public void ApplyWallColor(Color color)
    {
        currentWallColor = color;
        hasCurrentWallColor = true;

        if (wallRenderers.Count == 0)
        {
            FindWallsInScene();
        }

        AddDetectedARWalls();

        if (wallRenderers.Count == 0)
        {
            Debug.LogWarning("RoomColorManager: color guardado; esperando detectar paredes AR.");
            return;
        }

        Renderer selectedWallRenderer = GetSelectedWallRenderer();
        if (selectedWallRenderer != null)
        {
            ApplyColorToRenderer(selectedWallRenderer, color);
            Debug.Log("Color aplicado a pared AR seleccionada.");
            return;
        }

        foreach (Renderer wallRenderer in wallRenderers)
        {
            ApplyColorToRenderer(wallRenderer, color);
        }

        Debug.Log("Color de pared aplicado.");
    }

    public bool TrySelectWallAtScreenPoint(Vector2 screenPoint)
    {
        ResolveARManagers();

        if (arRaycastManager == null || arPlaneManager == null)
        {
            return false;
        }

        if (!arRaycastManager.Raycast(screenPoint, arHits, TrackableType.PlaneWithinPolygon))
        {
            return false;
        }

        foreach (ARRaycastHit hit in arHits)
        {
            ARPlane plane = arPlaneManager.GetPlane(hit.trackableId);
            if (plane == null || plane.alignment != PlaneAlignment.Vertical)
            {
                continue;
            }

            selectedARWall = plane;
            Renderer renderer = EnsureARWallOverlay(plane);
            if (renderer != null && !wallRenderers.Contains(renderer))
            {
                wallRenderers.Add(renderer);
            }

            if (hasCurrentWallColor && renderer != null)
            {
                ApplyColorToRenderer(renderer, currentWallColor);
            }

            Debug.Log("Pared AR seleccionada para color.");
            return true;
        }

        return false;
    }

    public void ClearSelectedWall()
    {
        selectedARWall = null;
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

        AddDetectedARWalls();

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

        AddDetectedARWalls();
    }

    private void ResolveARManagers()
    {
        if (arRaycastManager == null)
        {
            arRaycastManager = FindObjectOfType<ARRaycastManager>();
        }

        if (arPlaneManager == null)
        {
            arPlaneManager = FindObjectOfType<ARPlaneManager>();
        }
    }

    private void AddDetectedARWalls()
    {
        ARPlane[] planes = FindObjectsOfType<ARPlane>();
        foreach (ARPlane plane in planes)
        {
            if (plane == null || plane.alignment != PlaneAlignment.Vertical)
            {
                continue;
            }

            Renderer renderer = EnsureARWallOverlay(plane);
            if (renderer != null && !wallRenderers.Contains(renderer))
            {
                wallRenderers.Add(renderer);
            }
        }
    }

    private Renderer EnsureARWallOverlay(ARPlane plane)
    {
        if (arWallOverlays.TryGetValue(plane, out Renderer existingRenderer) && existingRenderer != null)
        {
            UpdateARWallOverlayMesh(plane, existingRenderer.GetComponent<MeshFilter>());
            return existingRenderer;
        }

        GameObject overlay = new GameObject("ARWallColorOverlay");
        overlay.transform.SetParent(plane.transform, false);

        MeshFilter meshFilter = overlay.AddComponent<MeshFilter>();
        MeshRenderer renderer = overlay.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = CreateARWallMaterial(currentWallColor);
        UpdateARWallOverlayMesh(plane, meshFilter);

        arWallOverlays[plane] = renderer;
        return renderer;
    }

    private Renderer GetSelectedWallRenderer()
    {
        if (selectedARWall == null)
        {
            return null;
        }

        if (arWallOverlays.TryGetValue(selectedARWall, out Renderer renderer) && renderer != null)
        {
            return renderer;
        }

        return EnsureARWallOverlay(selectedARWall);
    }

    private void UpdateARWallOverlayMesh(ARPlane plane, MeshFilter meshFilter)
    {
        if (meshFilter == null)
        {
            return;
        }

        Vector2 size = plane.size;
        if (size.x < minimumARWallSize || size.y < minimumARWallSize)
        {
            size = new Vector2(
                Mathf.Max(size.x, minimumARWallSize),
                Mathf.Max(size.y, minimumARWallSize));
        }

        Vector2 center = plane.center;
        float halfWidth = size.x * 0.5f;
        float halfHeight = size.y * 0.5f;

        Mesh mesh = meshFilter.sharedMesh;
        if (mesh == null)
        {
            mesh = new Mesh();
            mesh.name = "ARWallColorOverlayMesh";
            meshFilter.sharedMesh = mesh;
        }

        mesh.Clear();
        mesh.vertices = new[]
        {
            new Vector3(center.x - halfWidth, 0f, center.y - halfHeight),
            new Vector3(center.x + halfWidth, 0f, center.y - halfHeight),
            new Vector3(center.x - halfWidth, 0f, center.y + halfHeight),
            new Vector3(center.x + halfWidth, 0f, center.y + halfHeight)
        };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f)
        };
        mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    private void ApplyColorToRenderer(Renderer wallRenderer, Color color)
    {
        if (wallRenderer == null)
        {
            return;
        }

        bool isARWall = wallRenderer.GetComponentInParent<ARPlane>() != null;
        Color appliedColor = isARWall
            ? new Color(color.r, color.g, color.b, arWallOverlayAlpha)
            : color;
        Material wallMaterial = wallRenderer.material;
        ConfigureMaterialForColor(wallMaterial, appliedColor, isARWall);
    }

    private Material CreateARWallMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        Material material = new Material(shader);
        ConfigureMaterialForColor(material, new Color(color.r, color.g, color.b, arWallOverlayAlpha), true);
        return material;
    }

    private static void ConfigureMaterialForColor(Material material, Color color, bool transparent)
    {
        if (material == null)
        {
            return;
        }

        material.color = color;

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (!transparent)
        {
            return;
        }

        SetFloatIfMaterialHasProperty(material, "_Surface", 1f);
        SetFloatIfMaterialHasProperty(material, "_Blend", 0f);
        SetFloatIfMaterialHasProperty(material, "_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        SetFloatIfMaterialHasProperty(material, "_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        SetFloatIfMaterialHasProperty(material, "_ZWrite", 0);
        SetFloatIfMaterialHasProperty(material, "_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    private static void SetFloatIfMaterialHasProperty(Material material, string propertyName, float value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }
}
