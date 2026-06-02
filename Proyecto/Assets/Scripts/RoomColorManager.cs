using System.Collections;
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
    private float detectedWallOverlayAlpha = 0.35f;

    [SerializeField]
    private float selectedWallOverlayAlpha = 0.68f;

    [SerializeField]
    private Color detectedWallTint = new Color(0.15f, 0.72f, 1f, 1f);

    [SerializeField]
    private Color selectedWallTint = new Color(1f, 0.35f, 0.9f, 1f);

    [SerializeField]
    private float minimumARWallSize = 0.12f;

    [SerializeField]
    private float relaxedMinimumARWallSize = 0.06f;

    [SerializeField]
    private float wallSelectionScreenPadding = 150f;

    [SerializeField]
    private float maxWallSelectionDistance = 8.5f;

    [SerializeField]
    private float selectedWallBoostDuration = 0.8f;

    [SerializeField]
    private float selectedWallBoostMultiplier = 1.45f;

    [SerializeField]
    private bool requireExplicitApplyInAR = true;

    [SerializeField]
    private bool autoApplyPendingColorOnWallSelect = true;

    [SerializeField]
    private int wallSelectionRetryAttempts = 6;

    [SerializeField]
    private float wallSelectionRetryInterval = 0.22f;

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
    private bool isRetryingWallSelection;
    private float lastVerticalWallSeenTime = float.NegativeInfinity;

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
        verticalDetectionReadyTime = Time.unscaledTime + 0.3f;

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

    public bool ShouldPrioritizeWallSelection() => hasPendingWallColor;

    public bool HasAppliedWallColor() => hasCurrentWallColor || arWallAppliedColors.Count > 0;

    public string GetWallStatusText()
    {
        string selectedLabel = GetSelectedWallShortName();
        int trackedWalls = CountTrackedVerticalWalls();
        string pendingLabel = hasPendingWallColor ? "Color pendiente" : "Sin color pendiente";
        string appliedLabel = HasAppliedWallColor() ? "Color aplicado" : "Sin color aplicado";
        return selectedLabel + "\n" +
            "Paredes detectadas: " + trackedWalls + "\n" +
            pendingLabel + "\n" +
            appliedLabel;
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
            if (!IsVerticalLikePlane(plane))
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

        if (!TrySelectWallImmediate(screenPoint, out ARPlane plane))
        {
            TryScheduleWallSelectionRetry(screenPoint);
            ARDiagnostics.Report("Raycast AR a pared sin plano vertical util.");
            return false;
        }

        SelectARWall(plane);
        return true;
    }

    private bool TrySelectWallImmediate(Vector2 screenPoint, out ARPlane plane)
    {
        plane = null;
        if (!TryGetVerticalWallHit(screenPoint, WallRaycastTrackables, out plane))
        {
            return TryGetVerticalWallHit(screenPoint, TrackableType.PlaneEstimated, out plane) ||
                TryRaycastTrackedVerticalWall(screenPoint, out plane) ||
                TryGetClosestVerticalWallAtScreenPoint(screenPoint, out plane);
        }

        return true;
    }

    private void SelectARWall(ARPlane plane)
    {
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

        if (autoApplyPendingColorOnWallSelect && hasPendingWallColor)
        {
            Color queuedColor = pendingWallColor;
            hasPendingWallColor = false;
            ApplyWallColor(queuedColor);
            ARDiagnostics.Report("Color pendiente aplicado automaticamente a la pared seleccionada.");
        }
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

        Camera camera = Camera.main;
        float bestScore = float.MaxValue;
        for (int i = 0; i < arHits.Count; i++)
        {
            ARRaycastHit hit = arHits[i];
            ARPlane plane = arPlaneManager.GetPlane(hit.trackableId);
            if (!IsUsableVerticalPlane(plane, true))
            {
                plane = TryFindNearestVerticalPlane(hit.pose.position, true);
                if (!IsUsableVerticalPlane(plane, true))
                {
                    continue;
                }
            }

            if (!IsPlaneWithinSelectionDistance(camera, plane))
            {
                continue;
            }

            float score = hit.distance;
            if (camera != null)
            {
                Vector3 projected = camera.WorldToScreenPoint(plane.center);
                if (projected.z > 0f)
                {
                    score += Vector2.Distance(screenPoint, new Vector2(projected.x, projected.y)) * 0.0015f;
                }
            }

            if (score < bestScore)
            {
                bestScore = score;
                wallPlane = plane;
            }
        }

        return wallPlane != null;
    }

    private bool TryRaycastTrackedVerticalWall(Vector2 screenPoint, out ARPlane wallPlane)
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

        Ray ray = camera.ScreenPointToRay(screenPoint);
        float bestDistance = float.MaxValue;

        foreach (ARPlane plane in arPlaneManager.trackables)
        {
            if (!IsUsableVerticalPlane(plane, true))
            {
                continue;
            }

            Plane worldPlane = new Plane(plane.normal, plane.center);
            if (!worldPlane.Raycast(ray, out float distance) || distance < 0f)
            {
                continue;
            }

            if (distance > maxWallSelectionDistance)
            {
                continue;
            }

            Vector3 worldPoint = ray.GetPoint(distance);
            Vector3 localPoint = plane.transform.InverseTransformPoint(worldPoint);
            Vector2 planePoint = new Vector2(localPoint.x, localPoint.z);
            if (!IsPointInsidePlane(plane, planePoint))
            {
                continue;
            }

            if (distance < bestDistance)
            {
                bestDistance = distance;
                wallPlane = plane;
            }
        }

        return wallPlane != null;
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
        bool hasVisibleWallRect = false;
        foreach (ARPlane plane in arPlaneManager.trackables)
        {
            if (!IsUsableVerticalPlane(plane, true))
            {
                continue;
            }

            if (!TryGetPlaneScreenRect(camera, plane, out Rect screenRect))
            {
                continue;
            }

            hasVisibleWallRect = true;

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

        if (wallPlane != null)
        {
            return true;
        }

        if (hasVisibleWallRect)
        {
            return false;
        }

        Vector3 cameraPosition = camera.transform.position;
        float minDistance = float.MaxValue;
        foreach (ARPlane plane in arPlaneManager.trackables)
        {
            if (!IsUsableVerticalPlane(plane, true))
            {
                continue;
            }

            float distance = Vector3.Distance(cameraPosition, plane.transform.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                wallPlane = plane;
            }
        }

        return wallPlane != null;
    }

    private bool TryGetPlaneScreenRect(Camera camera, ARPlane plane, out Rect screenRect)
    {
        screenRect = default;

        Vector2 size = plane.size;
        float wallMinSize = GetEffectiveMinimumWallSize(false);
        if (size.x < wallMinSize || size.y < wallMinSize)
        {
            size = new Vector2(
                Mathf.Max(size.x, wallMinSize),
                Mathf.Max(size.y, wallMinSize));
        }

        Vector2 center = plane.centerInPlaneSpace;
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

    private bool IsUsableVerticalPlane(ARPlane plane, bool relaxedSize)
    {
        if (!IsVerticalLikePlane(plane))
        {
            return false;
        }

        if (plane.trackingState == TrackingState.None)
        {
            return false;
        }

        float wallMinSize = GetEffectiveMinimumWallSize(relaxedSize);
        Vector2 size = plane.size;
        return Mathf.Max(size.x, size.y) >= wallMinSize;
    }

    private static bool IsPointInsidePlane(ARPlane plane, Vector2 planePoint)
    {
        var boundary = plane.boundary;
        if (boundary.IsCreated && boundary.Length >= 3)
        {
            return WindingNumber(planePoint, boundary) != 0;
        }

        Vector2 center = plane.centerInPlaneSpace;
        Vector2 extents = plane.extents;
        return Mathf.Abs(planePoint.x - center.x) <= extents.x &&
            Mathf.Abs(planePoint.y - center.y) <= extents.y;
    }

    private static int WindingNumber(Vector2 point, Unity.Collections.NativeArray<Vector2> polygon)
    {
        int windingNumber = 0;
        for (int i = 0; i < polygon.Length; i++)
        {
            Vector2 current = polygon[i];
            Vector2 next = polygon[(i + 1) % polygon.Length];

            if (current.y <= point.y)
            {
                if (next.y > point.y && IsLeft(current, next, point) > 0f)
                {
                    windingNumber++;
                }
            }
            else if (next.y <= point.y && IsLeft(current, next, point) < 0f)
            {
                windingNumber--;
            }
        }

        return windingNumber;
    }

    private static float IsLeft(Vector2 a, Vector2 b, Vector2 point)
    {
        return (b.x - a.x) * (point.y - a.y) - (point.x - a.x) * (b.y - a.y);
    }

    private void AddDetectedARWalls()
    {
        if (arPlaneManager == null || Time.unscaledTime < verticalDetectionReadyTime)
        {
            return;
        }

        foreach (ARPlane plane in arPlaneManager.trackables)
        {
            if (!IsUsableVerticalPlane(plane, true))
            {
                continue;
            }

            lastVerticalWallSeenTime = Time.unscaledTime;

            Renderer renderer = EnsureARWallOverlay(plane);
            if (renderer != null && !wallRenderers.Contains(renderer))
            {
                wallRenderers.Add(renderer);
            }

            if (renderer != null && !arWallAppliedColors.ContainsKey(plane))
            {
                ApplyColorToRenderer(renderer, detectedWallTint, detectedWallOverlayAlpha);
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
        renderer.receiveShadows = false;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
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
        float wallMinSize = GetEffectiveMinimumWallSize(false);
        if (size.x < wallMinSize || size.y < wallMinSize)
        {
            size = new Vector2(
                Mathf.Max(size.x, wallMinSize),
                Mathf.Max(size.y, wallMinSize));
        }

        Vector2 center = plane.centerInPlaneSpace;
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

        Renderer renderer = meshFilter.GetComponent<Renderer>();
        if (renderer != null && !arWallAppliedColors.ContainsKey(plane))
        {
            ApplyColorToRenderer(renderer, detectedWallTint, detectedWallOverlayAlpha);
        }
    }

    private void ApplyColorToRenderer(Renderer wallRenderer, Color color)
    {
        if (wallRenderer == null)
        {
            return;
        }

        bool isARWall = wallRenderer.GetComponentInParent<ARPlane>() != null;
        float alpha = isARWall ? arWallOverlayAlpha : color.a;
        ApplyColorToRenderer(wallRenderer, color, alpha);
    }

    private void ApplyColorToRenderer(Renderer wallRenderer, Color color, float alpha)
    {
        if (wallRenderer == null)
        {
            return;
        }

        bool isARWall = wallRenderer.GetComponentInParent<ARPlane>() != null;
        Color appliedColor = isARWall ? new Color(color.r, color.g, color.b, alpha) : color;
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
        material.SetInt("_ZWrite", 0);
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

    private void TryScheduleWallSelectionRetry(Vector2 screenPoint)
    {
        if (isRetryingWallSelection)
        {
            return;
        }

        StartCoroutine(RetryWallSelection(screenPoint));
    }

    private IEnumerator RetryWallSelection(Vector2 screenPoint)
    {
        isRetryingWallSelection = true;
        int attempts = Mathf.Max(1, wallSelectionRetryAttempts);
        float waitSeconds = Mathf.Max(0.08f, wallSelectionRetryInterval);
        for (int attempt = 1; attempt <= attempts; attempt++)
        {
            yield return new WaitForSeconds(waitSeconds);
            ResolveARManagers();
            AddDetectedARWalls();
            if (arRaycastManager == null || arPlaneManager == null)
            {
                continue;
            }

            if (TrySelectWallImmediate(screenPoint, out ARPlane plane))
            {
                SelectARWall(plane);
                ARDiagnostics.Report("Pared AR seleccionada en reintento " + attempt + "/" + attempts + ".");
                isRetryingWallSelection = false;
                yield break;
            }
        }

        isRetryingWallSelection = false;
    }

    private int CountTrackedVerticalWalls()
    {
        if (arPlaneManager == null)
        {
            return 0;
        }

        int count = 0;
        foreach (ARPlane plane in arPlaneManager.trackables)
        {
            if (IsUsableVerticalPlane(plane, true))
            {
                count++;
            }
        }

        return count;
    }

    private float GetEffectiveMinimumWallSize(bool relaxed)
    {
        float strict = Mathf.Max(0.03f, minimumARWallSize);
        if (relaxed)
        {
            return Mathf.Max(0.03f, Mathf.Min(strict, relaxedMinimumARWallSize));
        }

        if (HasSeenVerticalWallsRecently())
        {
            return strict;
        }

        return Mathf.Max(0.03f, Mathf.Min(strict, relaxedMinimumARWallSize));
    }

    private bool HasSeenVerticalWallsRecently()
    {
        return Time.unscaledTime - lastVerticalWallSeenTime <= 2.5f;
    }

    private static bool IsVerticalLikePlane(ARPlane plane)
    {
        if (plane == null)
        {
            return false;
        }

        if (plane.alignment.IsVertical())
        {
            return true;
        }

        if (plane.alignment != PlaneAlignment.NotAxisAligned)
        {
            return false;
        }

        return IsNearVerticalByNormal(plane.normal);
    }

    private static bool IsNearVerticalByNormal(Vector3 normal)
    {
        if (normal.sqrMagnitude < 0.0001f)
        {
            return false;
        }

        float upDot = Mathf.Abs(Vector3.Dot(normal.normalized, Vector3.up));
        return upDot <= 0.3f;
    }

    private ARPlane TryFindNearestVerticalPlane(Vector3 worldPoint, bool relaxedSize)
    {
        if (arPlaneManager == null)
        {
            return null;
        }

        ARPlane nearest = null;
        float bestDistance = float.MaxValue;
        foreach (ARPlane plane in arPlaneManager.trackables)
        {
            if (!IsUsableVerticalPlane(plane, relaxedSize))
            {
                continue;
            }

            float distance = Vector3.Distance(worldPoint, plane.center);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                nearest = plane;
            }
        }

        return nearest;
    }

    private bool IsPlaneWithinSelectionDistance(Camera camera, ARPlane plane)
    {
        if (camera == null || plane == null)
        {
            return true;
        }

        float distanceToCenter = Vector3.Distance(camera.transform.position, plane.center);
        return distanceToCenter <= Mathf.Max(1.2f, maxWallSelectionDistance);
    }
}
