using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class RoomColorManager : MonoBehaviour
{
    [SerializeField]
    private List<Renderer> wallRenderers = new List<Renderer>();

    [SerializeField]
    private float arWallOverlayAlpha = 0.45f;

    [SerializeField]
    private float detectedWallOverlayAlpha = 0.18f;

    [SerializeField]
    private float selectedWallOverlayAlpha = 0.68f;

    [SerializeField]
    private Color detectedWallTint = new Color(0.15f, 0.72f, 1f, 1f);

    [SerializeField]
    private Color selectedWallTint = new Color(1f, 0.35f, 0.9f, 1f);

    [SerializeField]
    private float minimumARWallSize = 0.25f;

    [SerializeField]
    private float wallSelectionScreenPadding = 90f;

    [SerializeField]
    private float selectedWallBoostDuration = 0.8f;

    [SerializeField]
    private float selectedWallBoostMultiplier = 1.45f;

    [SerializeField]
    private bool requireExplicitApplyInAR = true;

    private Color currentWallColor = Color.white;
    private bool hasCurrentWallColor;
    private readonly Dictionary<ARPlane, Renderer> arWallOverlays = new Dictionary<ARPlane, Renderer>();
    private readonly Dictionary<ARPlane, LineRenderer> arWallOutlines = new Dictionary<ARPlane, LineRenderer>();
    private readonly Dictionary<ARPlane, Color> arWallAppliedColors = new Dictionary<ARPlane, Color>();
    private readonly List<ARRaycastHit> arHits = new List<ARRaycastHit>();
    private ARRaycastManager arRaycastManager;
    private ARPlaneManager arPlaneManager;
    private ARPlane selectedARWall;
    private float selectedWallBoostUntil;
    private float verticalDetectionReadyTime;
    private Color pendingWallColor = Color.white;
    private bool hasPendingWallColor;

    private const TrackableType WallRaycastTrackables =
        TrackableType.PlaneWithinPolygon |
        TrackableType.PlaneWithinBounds |
        TrackableType.PlaneWithinInfinity;

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
        verticalDetectionReadyTime = Time.unscaledTime + 1f;

        if (wallRenderers.Count == 0)
        {
            FindWallsInScene();
        }
    }

    private void Update()
    {
        ResolveARManagers();
        AddDetectedARWalls();
        UpdateSelectedWallVisuals();
    }

    public bool ApplyWallColor(Color color)
    {
        ResolveARManagers();
        AddDetectedARWalls();

        if (ShouldRequireExplicitApply() && selectedARWall == null)
        {
            pendingWallColor = color;
            hasPendingWallColor = true;
            ARDiagnostics.Report("Selecciona una pared AR antes de aplicar el color.");
            return false;
        }

        currentWallColor = color;
        hasCurrentWallColor = true;
        hasPendingWallColor = false;

        if (selectedARWall != null)
        {
            arWallAppliedColors[selectedARWall] = color;
            Renderer selectedWallRenderer = GetSelectedWallRenderer();
            if (selectedWallRenderer != null)
            {
                ApplyColorToRenderer(selectedWallRenderer, color);
            }

            UpdateSelectedWallVisuals();
            Debug.Log("Color aplicado a pared AR seleccionada.");
            ARDiagnostics.Report("Color aplicado a pared seleccionada.");
            return true;
        }

        if (wallRenderers.Count == 0)
        {
            FindWallsInScene();
        }

        foreach (Renderer wallRenderer in wallRenderers)
        {
            ApplyColorToRenderer(wallRenderer, color);
        }

        Debug.Log("Color de pared aplicado.");
        return true;
    }

    public void QueueWallColor(Color color)
    {
        if (ShouldRequireExplicitApply())
        {
            pendingWallColor = color;
            hasPendingWallColor = true;
            ARDiagnostics.Report("Color en espera. Toca APLICAR para pintar la pared.");
            return;
        }

        ApplyWallColor(color);
    }

    public void ApplyPendingWallColor()
    {
        if (!hasPendingWallColor)
        {
            ARDiagnostics.Report("No hay color pendiente para aplicar.");
            return;
        }

        if (ApplyWallColor(pendingWallColor))
        {
            ARDiagnostics.Report("Color aplicado a pared.");
        }
    }

    public string GetSelectedWallShortName()
    {
        if (selectedARWall != null)
        {
            return "Pared AR " + GetWallIndex(selectedARWall) + " seleccionada";
        }

        return "Sin pared seleccionada";
    }

    public bool HasPendingWallColor() => hasPendingWallColor;

    public bool HasAppliedWallColor() => hasCurrentWallColor || arWallAppliedColors.Count > 0;

    public string GetWallStatusText()
    {
        string selectedLabel = GetSelectedWallShortName();
        string pendingLabel = hasPendingWallColor ? "Color pendiente" : "Sin color pendiente";
        string appliedLabel = HasAppliedWallColor() ? "Color aplicado" : "Sin color aplicado";
        return selectedLabel + "\n" + pendingLabel + "\n" + appliedLabel;
    }

    private int GetWallIndex(ARPlane wall)
    {
        if (arPlaneManager == null || wall == null)
        {
            return 1;
        }

        int index = 1;
        foreach (ARPlane plane in arPlaneManager.trackables)
        {
            if (plane == null || plane.alignment != PlaneAlignment.Vertical)
            {
                continue;
            }

            if (plane == wall)
            {
                return index;
            }

            index++;
        }

        return 1;
    }

    public bool TrySelectWallAtScreenPoint(Vector2 screenPoint)
    {
        ResolveARManagers();

        if (arRaycastManager == null || arPlaneManager == null)
        {
            ARDiagnostics.Report("No se puede seleccionar pared: falta ARRaycastManager o ARPlaneManager.");
            return false;
        }

        AddDetectedARWalls();

        if (!TryGetVerticalWallHit(screenPoint, WallRaycastTrackables, out ARPlane plane))
        {
            if (!TryGetVerticalWallHit(screenPoint, TrackableType.PlaneEstimated, out plane) &&
                !TryGetClosestVerticalWallAtScreenPoint(screenPoint, out plane))
            {
                ARDiagnostics.Report("Raycast AR a pared sin plano vertical util.");
                return false;
            }
        }

        selectedARWall = plane;
        Renderer renderer = EnsureARWallOverlay(plane);
        if (renderer != null && !wallRenderers.Contains(renderer))
        {
            wallRenderers.Add(renderer);
        }

        selectedWallBoostUntil = Time.unscaledTime + selectedWallBoostDuration;
        UpdateSelectedWallVisuals();

        Debug.Log("Pared AR seleccionada para color.");
        ARDiagnostics.Report(GetSelectedWallShortName());
        return true;
    }

    public void ClearSelectedWall()
    {
        selectedARWall = null;
        selectedWallBoostUntil = 0f;
        UpdateSelectedWallVisuals();
    }

    public bool TryGetCurrentWallColor(out Color color)
    {
        if (hasCurrentWallColor)
        {
            color = currentWallColor;
            return true;
        }

        if (ShouldRequireExplicitApply() && arWallAppliedColors.Count == 0)
        {
            color = Color.white;
            return false;
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

    private bool TryGetVerticalWallHit(Vector2 screenPoint, TrackableType trackableTypes, out ARPlane wallPlane)
    {
        wallPlane = null;
        arHits.Clear();

        if (!arRaycastManager.Raycast(screenPoint, arHits, trackableTypes))
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

            wallPlane = plane;
            return true;
        }

        return false;
    }

    private bool TryGetClosestVerticalWallAtScreenPoint(Vector2 screenPoint, out ARPlane wallPlane)
    {
        wallPlane = null;
        if (arPlaneManager == null)
        {
            return false;
        }

        Camera camera = Camera.main;
        if (camera == null)
        {
            return false;
        }

        float bestScore = float.MaxValue;
        foreach (ARPlane plane in arPlaneManager.trackables)
        {
            if (plane == null || plane.alignment != PlaneAlignment.Vertical)
            {
                continue;
            }

            if (!TryGetPlaneScreenRect(camera, plane, out Rect screenRect))
            {
                continue;
            }

            Rect paddedRect = screenRect;
            paddedRect.xMin -= wallSelectionScreenPadding;
            paddedRect.xMax += wallSelectionScreenPadding;
            paddedRect.yMin -= wallSelectionScreenPadding;
            paddedRect.yMax += wallSelectionScreenPadding;

            if (!paddedRect.Contains(screenPoint))
            {
                continue;
            }

            float centerDistance = Vector2.Distance(screenRect.center, screenPoint);
            float score = screenRect.Contains(screenPoint) ? centerDistance * 0.25f : centerDistance;
            if (score < bestScore)
            {
                bestScore = score;
                wallPlane = plane;
            }
        }

        return wallPlane != null;
    }

    private bool TryGetPlaneScreenRect(Camera camera, ARPlane plane, out Rect screenRect)
    {
        screenRect = default;

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

        Vector3[] localCorners =
        {
            new Vector3(center.x - halfWidth, 0f, center.y - halfHeight),
            new Vector3(center.x + halfWidth, 0f, center.y - halfHeight),
            new Vector3(center.x - halfWidth, 0f, center.y + halfHeight),
            new Vector3(center.x + halfWidth, 0f, center.y + halfHeight)
        };

        bool hasVisibleCorner = false;
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);

        for (int i = 0; i < localCorners.Length; i++)
        {
            Vector3 screen = camera.WorldToScreenPoint(plane.transform.TransformPoint(localCorners[i]));
            if (screen.z <= 0f)
            {
                continue;
            }

            hasVisibleCorner = true;
            min = Vector2.Min(min, screen);
            max = Vector2.Max(max, screen);
        }

        if (!hasVisibleCorner)
        {
            return false;
        }

        screenRect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        return true;
    }

    private void AddDetectedARWalls()
    {
        if (arPlaneManager == null || Time.unscaledTime < verticalDetectionReadyTime)
        {
            return;
        }

        foreach (ARPlane plane in arPlaneManager.trackables)
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
            if (!arWallOutlines.TryGetValue(plane, out LineRenderer existingOutline) || existingOutline == null)
            {
                existingOutline = existingRenderer.GetComponent<LineRenderer>();
                if (existingOutline == null)
                {
                    existingOutline = existingRenderer.gameObject.AddComponent<LineRenderer>();
                }

                ConfigureWallOutline(existingOutline);
                arWallOutlines[plane] = existingOutline;
            }

            UpdateARWallOverlayMesh(plane, existingRenderer.GetComponent<MeshFilter>());
            return existingRenderer;
        }

        GameObject overlay = new GameObject("ARWallColorOverlay");
        overlay.transform.SetParent(plane.transform, false);

        MeshFilter meshFilter = overlay.AddComponent<MeshFilter>();
        MeshRenderer renderer = overlay.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = CreateARWallMaterial(detectedWallTint, detectedWallOverlayAlpha);

        LineRenderer outline = overlay.AddComponent<LineRenderer>();
        ConfigureWallOutline(outline);

        UpdateARWallOverlayMesh(plane, meshFilter);

        arWallOverlays[plane] = renderer;
        arWallOutlines[plane] = outline;
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

        LineRenderer outline = meshFilter.GetComponent<LineRenderer>();
        if (outline != null)
        {
            outline.positionCount = 4;
            outline.SetPosition(0, mesh.vertices[0]);
            outline.SetPosition(1, mesh.vertices[1]);
            outline.SetPosition(2, mesh.vertices[3]);
            outline.SetPosition(3, mesh.vertices[2]);
        }
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

    private void UpdateSelectedWallVisuals()
    {
        if (arWallOverlays.Count == 0)
        {
            return;
        }

        bool boostActive = Time.unscaledTime <= selectedWallBoostUntil;
        foreach (KeyValuePair<ARPlane, Renderer> pair in arWallOverlays)
        {
            Renderer renderer = pair.Value;
            if (renderer == null)
            {
                continue;
            }

            ARPlane plane = pair.Key;
            Color appliedColor = Color.white;
            bool hasAppliedColor = plane != null && arWallAppliedColors.TryGetValue(plane, out appliedColor);
            Color baseColor = hasAppliedColor ? appliedColor : detectedWallTint;
            bool isSelected = selectedARWall != null && pair.Key == selectedARWall;
            float alpha = hasAppliedColor ? arWallOverlayAlpha : detectedWallOverlayAlpha;
            Color tinted = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);

            if (isSelected)
            {
                Color selectedColor = hasPendingWallColor
                    ? pendingWallColor
                    : hasAppliedColor ? appliedColor : selectedWallTint;

                if (boostActive)
                {
                    tinted = new Color(
                        Mathf.Clamp01(selectedColor.r * selectedWallBoostMultiplier),
                        Mathf.Clamp01(selectedColor.g * selectedWallBoostMultiplier),
                        Mathf.Clamp01(selectedColor.b * selectedWallBoostMultiplier),
                        selectedWallOverlayAlpha);
                }
                else
                {
                    tinted = new Color(selectedColor.r, selectedColor.g, selectedColor.b, selectedWallOverlayAlpha);
                }
            }

            Material material = renderer.material;
            ConfigureMaterialForColor(material, tinted, true);

            if (plane != null && arWallOutlines.TryGetValue(plane, out LineRenderer outline) && outline != null)
            {
                outline.enabled = true;
                outline.startWidth = isSelected ? 0.018f : 0.008f;
                outline.endWidth = outline.startWidth;
                outline.startColor = tinted;
                outline.endColor = tinted;
                if (outline.sharedMaterial != null)
                {
                    ConfigureMaterialForColor(outline.sharedMaterial, tinted, true);
                }
            }
        }
    }

    private void ConfigureWallOutline(LineRenderer outline)
    {
        if (outline == null)
        {
            return;
        }

        outline.useWorldSpace = false;
        outline.loop = true;
        outline.widthMultiplier = 1f;
        outline.numCornerVertices = 2;
        outline.numCapVertices = 2;
        outline.sharedMaterial = CreateARWallMaterial(detectedWallTint, Mathf.Clamp01(detectedWallOverlayAlpha + 0.18f));
    }

    private Material CreateARWallMaterial(Color color, float alpha)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        Material material = new Material(shader);
        ConfigureMaterialForColor(material, new Color(color.r, color.g, color.b, alpha), true);
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

    private bool ShouldRequireExplicitApply()
    {
        if (!requireExplicitApplyInAR)
        {
            return false;
        }

        return SceneManager.GetActiveScene().name == "ARScene";
    }
}
