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
    private float minimumVisibleWallOverlayWidth = 0.5f;

    [SerializeField]
    private float minimumVisibleWallOverlayHeight = 0.42f;

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
    private const string WallOverlayName = "ARWallColorOverlay";
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
        TrackableType.PlaneWithinBounds;

    private const float WallSelectionBoundsPadding = 0.24f;

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
            if (autoApplyPendingColorOnWallSelect)
            {
                if (HasSelectedWall())
                {
                    ApplyPendingWallColor();
                    return;
                }

                ARDiagnostics.Report("Color en espera. Toca una pared para aplicarlo.");
                return;
            }

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

    public void ClearWallColor()
    {
        currentWallColor = Color.white;
        hasCurrentWallColor = false;
        pendingWallColor = Color.white;
        hasPendingWallColor = false;
        arWallAppliedColors.Clear();
        ClearSelectedWall();

        if (wallRenderers.Count == 0)
        {
            FindWallsInScene();
        }

        for (int i = wallRenderers.Count - 1; i >= 0; i--)
        {
            Renderer wallRenderer = wallRenderers[i];
            if (wallRenderer == null)
            {
                wallRenderers.RemoveAt(i);
                continue;
            }

            bool isARWall = IsARWallRenderer(wallRenderer);
            if (isARWall)
            {
                ApplyColorToRenderer(wallRenderer, detectedWallTint, detectedWallOverlayAlpha);
            }
            else
            {
                ApplyColorToRenderer(wallRenderer, Color.white);
            }
        }

        UpdateSelectedWallVisuals();
        ARDiagnostics.Report("Color de paredes borrado.");
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

    private bool HasSelectedWall() => selectedARWall != null;

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
            // Debug: contar planos verticales detectados y mostrar diagnóstico detallado
            Debug.Log($"\n=== DIAGNÓSTICO DE FALLO DE SELECCIÓN ===");
            Debug.Log($"ScreenPoint: {screenPoint}");
            int verticalCount = 0;
            int visibleScreenCount = 0;
            
            foreach (ARPlane p in arPlaneManager.trackables)
            {
                if (IsVerticalLikePlane(p) && p.trackingState != TrackingState.None)
                {
                    verticalCount++;
                    if (TryGetPlaneScreenRect(Camera.main, p, out Rect screenRect))
                    {
                        visibleScreenCount++;
                        bool contains = screenRect.Contains(screenPoint);
                        Debug.Log($"  Plano {p.trackableId}: Rect={screenRect}, ContainsTap={contains}, Tracking={p.trackingState}, Size={p.size}");
                    }
                }
            }
            
            Debug.Log($"Planos verticales en escena: {verticalCount}, Visibles en pantalla: {visibleScreenCount}");
            Debug.Log($"=== FIN DIAGNÓSTICO ===\n");
            
            ClearSelectedWall();
            TryScheduleWallSelectionRetry(screenPoint);
            ARDiagnostics.Report("✗ Raycast AR sin pared. " + verticalCount + " planos verticales en escena.");
            return false;
        }

        SelectARWall(plane);
        return true;
    }

    private bool TrySelectWallImmediate(Vector2 screenPoint, out ARPlane plane)
    {
        plane = null;
        
        // Debug: contar planos verticales disponibles
        int totalVerticalPlanes = 0;
        int visibleOnScreen = 0;
        foreach (ARPlane p in arPlaneManager.trackables)
        {
            if (IsVerticalLikePlane(p) && p.trackingState != TrackingState.None)
            {
                totalVerticalPlanes++;
                if (TryGetPlaneScreenRect(Camera.main, p, out _))
                {
                    visibleOnScreen++;
                }
            }
        }
        Debug.Log($"[SelectWall] Total planos verticales: {totalVerticalPlanes}, Visibles en pantalla: {visibleOnScreen}");
        
        // Primero intentar selección visual (más confiable en AR)
        if (TryGetVisibleVerticalWallAtScreenPoint(screenPoint, out plane))
        {
            Debug.Log($"[SelectWall] SUCCESS: Pared seleccionada por visibilidad en pantalla. TrackableId: {plane.trackableId}");
            ARDiagnostics.Report("✓ Pared seleccionada por visibilidad");
            return true;
        }
        else
        {
            Debug.Log($"[SelectWall] FALLO: TryGetVisibleVerticalWallAtScreenPoint retornó false");
        }
        
        // Fallback: intentar raycast directo
        if (TryGetVerticalWallHit(screenPoint, out plane))
        {
            Debug.Log($"[SelectWall] SUCCESS: Pared seleccionada por raycast AR directo. TrackableId: {plane.trackableId}");
            ARDiagnostics.Report("✓ Pared seleccionada por raycast directo");
            return true;
        }
        else
        {
            Debug.Log($"[SelectWall] FALLO: TryGetVerticalWallHit retornó false");
        }
        
        // Último fallback: raycast contra planos trackeados
        if (TryRaycastTrackedVerticalWall(screenPoint, out plane))
        {
            Debug.Log($"[SelectWall] SUCCESS: Pared seleccionada por raycast contra planos trackeados. TrackableId: {plane.trackableId}");
            ARDiagnostics.Report("✓ Pared seleccionada por raycast trackeado");
            return true;
        }
        else
        {
            Debug.Log($"[SelectWall] FALLO: TryRaycastTrackedVerticalWall retornó false");
        }
        
        Debug.Log($"[SelectWall] NINGÚN MÉTODO FUNCIONÓ. ScreenPoint: {screenPoint}");
        return false;
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

    private bool TryGetVerticalWallHit(Vector2 screenPoint, out ARPlane wallPlane)
    {
        wallPlane = null;
        if (arRaycastManager == null || arPlaneManager == null)
        {
            Debug.Log("[TryGetVerticalWallHit] ERROR: arRaycastManager o arPlaneManager es null");
            return false;
        }

        arHits.Clear();

        // Intentar raycast con trackables más permisivos
        TrackableType wallMaskExpanded = 
            TrackableType.PlaneWithinPolygon |
            TrackableType.PlaneWithinBounds |
            TrackableType.PlaneWithinInfinity;  // Incluir PlaneWithinInfinity para paredes lejanas

        bool hasHits = arRaycastManager.Raycast(screenPoint, arHits, wallMaskExpanded);
        
        // Si no hay hits, intentar con solo los trackables estándar
        if (!hasHits)
        {
            hasHits = arRaycastManager.Raycast(screenPoint, arHits, WallRaycastTrackables);
        }
        
        if (!hasHits || arHits.Count == 0)
        {
            Debug.Log($"[TryGetVerticalWallHit] No raycast hits encontrados en {screenPoint}");
            return false;
        }
        
        Debug.Log($"[TryGetVerticalWallHit] Raycast encontró {arHits.Count} hits");

        Camera camera = Camera.main;
        float bestScore = float.MaxValue;
        int checkedHits = 0;
        
        for (int i = 0; i < arHits.Count; i++)
        {
            ARRaycastHit hit = arHits[i];
            checkedHits++;
            
            ARPlane plane = arPlaneManager.GetPlane(hit.trackableId);
            
            if (plane == null)
            {
                Debug.Log($"[TryGetVerticalWallHit] Hit {i}: plano es null");
                continue;
            }
            
            Debug.Log($"[TryGetVerticalWallHit] Hit {i}: ID={plane.trackableId}, HitType={hit.hitType}");
            
            // Check de verticalidad más flexible (no requerir tamaño específico si es un raycast hit)
            if (!IsVerticalLikePlane(plane))
            {
                Debug.Log($"[TryGetVerticalWallHit] Hit {i}: NO es vertical");
                continue;
            }
            
            Debug.Log($"[TryGetVerticalWallHit] Hit {i}: ✓ Es vertical");

            if (plane.trackingState == TrackingState.None)
            {
                Debug.Log($"[TryGetVerticalWallHit] Hit {i}: Tracking=None");
                continue;
            }

            if (!IsPlaneWithinSelectionDistance(Camera.main, plane))
            {
                Debug.Log($"[TryGetVerticalWallHit] Hit {i}: Fuera de distancia máxima");
                continue;
            }

            float score = GetVerticalHitScore(
                hit,
                plane,
                screenPoint,
                Camera.main,
                0f);
            
            Debug.Log($"[TryGetVerticalWallHit] Hit {i}: Score={score:F3}");

            if (score < bestScore)
            {
                bestScore = score;
                wallPlane = plane;
                Debug.Log($"[TryGetVerticalWallHit] → Nuevo mejor hit");
            }
        }
        
        Debug.Log($"[TryGetVerticalWallHit] Resultado: {(wallPlane != null ? "SUCCESS" : "FALLO")}");
        return wallPlane != null;
    }

    private bool TryRaycastTrackedVerticalWall(Vector2 screenPoint, out ARPlane wallPlane)
    {
        wallPlane = null;
        if (arPlaneManager == null)
        {
            Debug.Log("[TryRaycastTracked] ERROR: arPlaneManager es null");
            return false;
        }

        Camera camera = Camera.main;
        if (camera == null)
        {
            Debug.Log("[TryRaycastTracked] ERROR: Camera.main es null");
            return false;
        }

        Ray ray = camera.ScreenPointToRay(screenPoint);
        float bestDistance = float.MaxValue;
        int checkedPlanes = 0;
        int verticalPlanes = 0;

        foreach (ARPlane plane in arPlaneManager.trackables)
        {
            checkedPlanes++;
            
            // Usar check más flexible de verticalidad
            if (!IsVerticalLikePlane(plane))
            {
                continue;
            }
            verticalPlanes++;
            
            Debug.Log($"[TryRaycastTracked] Plano {plane.trackableId}: vertical check OK");

            if (plane.trackingState == TrackingState.None)
            {
                Debug.Log($"[TryRaycastTracked] Plano {plane.trackableId}: tracking=None");
                continue;
            }

            Vector3 normal = GetPlaneNormal(plane);
            if (normal.sqrMagnitude < 0.0001f)
            {
                Debug.Log($"[TryRaycastTracked] Plano {plane.trackableId}: normal inválida");
                continue;
            }
            
            Debug.Log($"[TryRaycastTracked] Plano {plane.trackableId}: normal OK, normal={normal}");

            Plane worldPlane = new Plane(normal, plane.center);
            if (!worldPlane.Raycast(ray, out float distance) || distance < 0f)
            {
                Debug.Log($"[TryRaycastTracked] Plano {plane.trackableId}: raycast sin intersección");
                continue;
            }
            
            Debug.Log($"[TryRaycastTracked] Plano {plane.trackableId}: ✓ intersectó a distance={distance:F3}m");

            if (distance > maxWallSelectionDistance)
            {
                Debug.Log($"[TryRaycastTracked] Plano {plane.trackableId}: fuera de distancia máxima ({maxWallSelectionDistance}m)");
                continue;
            }

            Vector3 worldPoint = ray.GetPoint(distance);
            
            // Validación de punto dentro/cerca del plano con padding MUY generoso
            bool pointInside = IsWorldPointInsidePlane(plane, worldPoint);
            bool pointNearPadding1 = IsWorldPointNearPlaneBounds(plane, worldPoint, WallSelectionBoundsPadding);
            bool pointNearPadding3 = IsWorldPointNearPlaneBounds(plane, worldPoint, WallSelectionBoundsPadding * 3f);
            
            bool pointValid = pointInside || pointNearPadding1 || pointNearPadding3;
            
            Debug.Log($"[TryRaycastTracked] Plano {plane.trackableId}: inside={pointInside}, padding1={pointNearPadding1}, padding3={pointNearPadding3}");
                
            // Si aún no es válido, pero el raycast intersectó, aceptarlo igualmente
            // (el raycast ya filtró planos que no están en el rayo)
            if (!pointValid && plane.trackingState != TrackingState.Tracking)
            {
                Debug.Log($"[TryRaycastTracked] Plano {plane.trackableId}: punto no válido y no Tracking");
                continue;
            }
            
            if (!pointValid && plane.trackingState == TrackingState.Tracking)
            {
                Debug.Log($"[TryRaycastTracked] Plano {plane.trackableId}: ✓ Aceptado porque está siendo Tracked");
            }

            if (distance < bestDistance)
            {
                bestDistance = distance;
                wallPlane = plane;
                Debug.Log($"[TryRaycastTracked] Plano {plane.trackableId}: → Nuevo mejor candidato");
            }
        }
        
        Debug.Log($"[TryRaycastTracked] RESUMEN: Chequeados={checkedPlanes}, Verticales={verticalPlanes}, Resultado={wallPlane != null}");
        return wallPlane != null;
    }

    private bool TryGetVisibleVerticalWallAtScreenPoint(Vector2 screenPoint, out ARPlane wallPlane)
    {
        wallPlane = null;
        if (arPlaneManager == null)
        {
            Debug.Log("[TryGetVisibleVertical] ERROR: arPlaneManager es null");
            return false;
        }

        Camera camera = Camera.main;
        if (camera == null)
        {
            Debug.Log("[TryGetVisibleVertical] ERROR: Camera.main es null");
            return false;
        }

        float bestScore = float.MaxValue;
        int checkedPlanes = 0;
        int verticalPlanes = 0;
        int screenRectFailed = 0;
        
        foreach (ARPlane plane in arPlaneManager.trackables)
        {
            checkedPlanes++;
            
            // Check flexible de verticalidad
            if (!IsVerticalLikePlane(plane))
            {
                continue;
            }
            verticalPlanes++;
            
            if (plane.trackingState == TrackingState.None)
            {
                Debug.Log($"[TryGetVisibleVertical] Plano {plane.trackableId}: tracking=None");
                continue;
            }

            if (!TryGetPlaneScreenRect(camera, plane, out Rect screenRect))
            {
                screenRectFailed++;
                Debug.Log($"[TryGetVisibleVertical] Plano {plane.trackableId}: TryGetPlaneScreenRect falló");
                continue;
            }
            
            Debug.Log($"[TryGetVisibleVertical] Plano {plane.trackableId}: screenRect={screenRect}, tap={screenPoint}");

            Rect paddedRect = screenRect;
            paddedRect.xMin -= wallSelectionScreenPadding * 1.5f;
            paddedRect.xMax += wallSelectionScreenPadding * 1.5f;
            paddedRect.yMin -= wallSelectionScreenPadding * 1.5f;
            paddedRect.yMax += wallSelectionScreenPadding * 1.5f;

            if (!paddedRect.Contains(screenPoint))
            {
                Debug.Log($"[TryGetVisibleVertical] Plano {plane.trackableId}: paddedRect NO contiene el tap");
                continue;
            }
            
            Debug.Log($"[TryGetVisibleVertical] Plano {plane.trackableId}: ✓ paddedRect SÍ contiene el tap");

            // Mejor scoring: selecciona el plano más cercano a donde tocó
            float centerDistance = Vector2.Distance(screenRect.center, screenPoint);
            
            // Si está dentro, excelente - tomar el más cercano al centro
            if (screenRect.Contains(screenPoint))
            {
                Debug.Log($"[TryGetVisibleVertical] Plano {plane.trackableId}: ✓ DENTRO del rect. Distance={centerDistance}");
                if (centerDistance < bestScore)
                {
                    bestScore = centerDistance;
                    wallPlane = plane;
                    Debug.Log($"[TryGetVisibleVertical] → Nuevo MEJOR plano seleccionado");
                }
            }
            else if (wallPlane == null)
            {
                // Fallback: si no hay tap dentro, aceptar cualquiera en área padded
                Debug.Log($"[TryGetVisibleVertical] Plano {plane.trackableId}: En área padded (sin tap dentro aún)");
                wallPlane = plane;
            }
        }
        
        Debug.Log($"[TryGetVisibleVertical] RESUMEN: Chequeados={checkedPlanes}, Verticales={verticalPlanes}, ScreenRectFailed={screenRectFailed}, Resultado={wallPlane != null}");
        return wallPlane != null;
    }

    private bool TryGetPlaneScreenRect(Camera camera, ARPlane plane, out Rect screenRect)
    {
        screenRect = default;

        Vector2 size = plane.size;
        float wallMinSize = GetEffectiveMinimumWallSize(false);
        float visualMinWidth = Mathf.Max(wallMinSize, minimumVisibleWallOverlayWidth);
        float visualMinHeight = Mathf.Max(wallMinSize, minimumVisibleWallOverlayHeight);
        if (size.x < visualMinWidth || size.y < visualMinHeight)
        {
            size = new Vector2(
                Mathf.Max(size.x, visualMinWidth),
                Mathf.Max(size.y, visualMinHeight));
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

    private static float GetVerticalHitScore(
        ARRaycastHit hit,
        ARPlane plane,
        Vector2 screenPoint,
        Camera camera,
        float fallbackPenalty)
    {
        float score = hit.distance + fallbackPenalty;
        TrackableType hitType = hit.hitType;
        if ((hitType & TrackableType.PlaneWithinPolygon) != 0)
        {
            score += 0f;
        }
        else if ((hitType & TrackableType.PlaneWithinBounds) != 0)
        {
            score += 0.12f;
        }
        else
        {
            score += 0.5f;
        }

        if (camera != null && plane != null)
        {
            Vector3 projected = camera.WorldToScreenPoint(plane.center);
            if (projected.z > 0f)
            {
                score += Vector2.Distance(screenPoint, new Vector2(projected.x, projected.y)) * 0.0015f;
            }
        }

        return score;
    }

    private static bool IsWorldPointInsidePlane(ARPlane plane, Vector3 worldPoint)
    {
        Vector3 localPoint = plane.transform.InverseTransformPoint(worldPoint);
        return IsPointInsidePlane(plane, new Vector2(localPoint.x, localPoint.z));
    }

    private static bool IsWorldPointNearPlaneBounds(ARPlane plane, Vector3 worldPoint, float padding)
    {
        Vector3 localPoint = plane.transform.InverseTransformPoint(worldPoint);
        return IsPointNearPlaneBounds(plane, new Vector2(localPoint.x, localPoint.z), padding);
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

    private static bool IsPointNearPlaneBounds(ARPlane plane, Vector2 planePoint, float padding)
    {
        Vector2 center = plane.centerInPlaneSpace;
        Vector2 extents = plane.extents;
        return Mathf.Abs(planePoint.x - center.x) <= extents.x + padding &&
            Mathf.Abs(planePoint.y - center.y) <= extents.y + padding;
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

    /// <summary>
    /// Filtra planos verticales para remover duplicados/sobrelapados.
    /// Si dos planos están muy cercanos en posición y tamaño, mantiene solo el que está siendo tracked más activamente.
    /// </summary>
    private List<ARPlane> FilterDuplicateVerticalPlanes(TrackableCollection<ARPlane> planes)
    {
        List<ARPlane> filtered = new List<ARPlane>();
        List<ARPlane> allPlanes = new List<ARPlane>();
        
        // Convertir TrackableCollection a List
        foreach (ARPlane p in planes)
        {
            allPlanes.Add(p);
        }
        
        float overlapThreshold = 0.15f; // 15cm - si dos planos están más cerca, considerar duplicados
        
        foreach (ARPlane candidate in allPlanes)
        {
            if (!IsVerticalLikePlane(candidate) || candidate.trackingState == TrackingState.None)
            {
                continue;
            }

            bool isDuplicate = false;
            
            // Comparar con planos ya agregados
            for (int i = filtered.Count - 1; i >= 0; i--)
            {
                ARPlane existing = filtered[i];
                
                // Calcular distancia entre centros
                float centerDistance = Vector3.Distance(candidate.center, existing.center);
                
                // Si están muy cercanos, es probable que sean el mismo plano duplicado
                if (centerDistance < overlapThreshold)
                {
                    // Mantener el que está being tracked más recientemente
                    if (candidate.trackingState == TrackingState.Tracking && existing.trackingState != TrackingState.Tracking)
                    {
                        filtered[i] = candidate;
                        Debug.Log($"[FilterDuplicate] Reemplazado plano {existing.trackableId} con {candidate.trackableId} (más reciente). Distance={centerDistance:F3}m");
                    }
                    else
                    {
                        Debug.Log($"[FilterDuplicate] Descartado plano duplicado {candidate.trackableId} (cercano a {existing.trackableId}). Distance={centerDistance:F3}m");
                    }
                    isDuplicate = true;
                    break;
                }
                
                // También revisar si los tamaños son muy similares y los centros están alineados
                Vector2 sizeA = candidate.size;
                Vector2 sizeB = existing.size;
                float sizeSimilarity = Mathf.Abs(sizeA.x - sizeB.x) + Mathf.Abs(sizeA.y - sizeB.y);
                
                if (centerDistance < overlapThreshold * 2f && sizeSimilarity < 0.05f)
                {
                    Debug.Log($"[FilterDuplicate] Descartado plano {candidate.trackableId} - muy similar a {existing.trackableId}. Distance={centerDistance:F3}m, SizeDiff={sizeSimilarity:F3}");
                    isDuplicate = true;
                    break;
                }
            }
            
            if (!isDuplicate)
            {
                filtered.Add(candidate);
            }
        }
        
        Debug.Log($"[FilterDuplicate] Total planos: {allPlanes.Count}, Después de filtrar: {filtered.Count}");
        return filtered;
    }

    private void AddDetectedARWalls()
    {
        if (arPlaneManager == null || Time.unscaledTime < verticalDetectionReadyTime)
        {
            return;
        }

        // Filtrar planos duplicados/sobrelapados
        List<ARPlane> validPlanes = FilterDuplicateVerticalPlanes(arPlaneManager.trackables);
        
        foreach (ARPlane plane in validPlanes)
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
        
        // Crear material overlay (con manejo de null)
        Material overlayMaterial = CreateARWallMaterial(detectedWallTint, detectedWallOverlayAlpha);
        if (overlayMaterial != null)
        {
            renderer.sharedMaterial = overlayMaterial;
        }
        else
        {
            Debug.LogWarning("RoomColorManager: No se pudo crear material para overlay. Usando material default.");
        }

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

        bool isARWall = IsARWallRenderer(wallRenderer);
        float alpha = isARWall ? arWallOverlayAlpha : color.a;
        ApplyColorToRenderer(wallRenderer, color, alpha);
    }

    private void ApplyColorToRenderer(Renderer wallRenderer, Color color, float alpha)
    {
        if (wallRenderer == null)
        {
            return;
        }

        bool isARWall = IsARWallRenderer(wallRenderer);
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
        
        Material outlineMaterial = CreateARWallMaterial(detectedWallTint, Mathf.Clamp01(detectedWallOverlayAlpha + 0.18f));
        if (outlineMaterial != null)
        {
            outline.sharedMaterial = outlineMaterial;
        }
    }

    private Material CreateARWallMaterial(Color color, float alpha)
    {
        Shader shader = TryFindShader();
        if (shader == null)
        {
            // Fallback: intentar usar shader Standard de Unity
            Debug.LogWarning("RoomColorManager: No se encontro shader adecuado. Intentando Standard...");
            Shader standardShader = Shader.Find("Standard");
            if (standardShader != null)
            {
                Material fallbackMaterial = new Material(standardShader);
                ConfigureMaterialForColor(fallbackMaterial, new Color(color.r, color.g, color.b, alpha), true);
                return fallbackMaterial;
            }
            
            // Si ni siquiera Standard existe, devolver null
            Debug.LogError("RoomColorManager: No se encontro ningun shader valido en el sistema.");
            return null;
        }

        Material material = new Material(shader);
        ConfigureMaterialForColor(material, new Color(color.r, color.g, color.b, alpha), true);
        return material;
    }
    
    private static Shader TryFindShader()
    {
        // Orden de busqueda: shaders modernos primero, luego fallbacks
        string[] shaderNames = new string[]
        {
            "Universal Render Pipeline/Unlit",
            "Universal Render Pipeline/Lit",
            "Unlit/Color",
            "Unlit/Texture",
            "Sprites/Default",
            "UI/Default",
            "Hidden/Internal-Colored",
            "Standard"
        };
        
        foreach (string shaderName in shaderNames)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader != null)
            {
                return shader;
            }
        }
        
        return null;
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
        else if (material.HasProperty("_Color"))
        {
            // Fallback para _Color si no existe _BaseColor
            material.SetColor("_Color", color);
        }

        if (!transparent)
        {
            return;
        }

        try
        {
            SetFloatIfMaterialHasProperty(material, "_Surface", 1f);
            SetFloatIfMaterialHasProperty(material, "_Blend", 0f);
            SetFloatIfMaterialHasProperty(material, "_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            SetFloatIfMaterialHasProperty(material, "_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            SetFloatIfMaterialHasProperty(material, "_ZWrite", 0);
            SetFloatIfMaterialHasProperty(material, "_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            
            // Intentar habilitar keywords, pero no fallar si no existen
            try { material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); } catch { }
            
            material.SetInt("_ZWrite", 0);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("RoomColorManager: Error configurando material transparente: " + ex.Message);
        }
    }

    private static bool IsARWallRenderer(Renderer renderer)
    {
        return renderer != null &&
            (renderer.GetComponentInParent<ARPlane>() != null ||
            renderer.gameObject.name == WallOverlayName);
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
        float strict = Mathf.Max(0.025f, Mathf.Min(minimumARWallSize, 0.08f));
        if (relaxed)
        {
            return Mathf.Max(0.02f, Mathf.Min(strict, relaxedMinimumARWallSize));
        }

        if (HasSeenVerticalWallsRecently())
        {
            return strict;
        }

        return Mathf.Max(0.02f, Mathf.Min(strict, relaxedMinimumARWallSize));
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

        if (plane.alignment.IsHorizontal())
        {
            return false;
        }

        return IsNearVerticalByNormal(GetPlaneNormal(plane));
    }

    private static Vector3 GetPlaneNormal(ARPlane plane)
    {
        if (plane == null)
        {
            return Vector3.zero;
        }

        if (plane.normal.sqrMagnitude >= 0.0001f)
        {
            return plane.normal.normalized;
        }

        // Fallback: usar la matriz de transformación del plano (más confiable que solo transform.up)
        Vector3 transformNormal = plane.transform.right;  // Para planos verticales, right es mejor que up
        if (transformNormal.sqrMagnitude >= 0.0001f)
        {
            return transformNormal.normalized;
        }
        
        transformNormal = plane.transform.up;
        return transformNormal.sqrMagnitude >= 0.0001f ? transformNormal.normalized : Vector3.zero;
    }

    private static bool IsNearVerticalByNormal(Vector3 normal)
    {
        if (normal.sqrMagnitude < 0.0001f)
        {
            return false;
        }

        float upDot = Mathf.Abs(Vector3.Dot(normal.normalized, Vector3.up));
        // Umbral más permisivo: tolera planos más inclinados (0.65 en lugar de 0.55)
        // Esto permite detectar paredes que no sean perfectamente verticales
        return upDot <= 0.65f;
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
