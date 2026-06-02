using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ARSurfaceDetectionManager : MonoBehaviour
{
    [SerializeField]
    private bool hidePlaneVisuals = true;

    [SerializeField]
    private bool showVerticalPlaneHints = true;

    [SerializeField]
    private bool detectHorizontalPlanes = true;

    [SerializeField]
    private bool detectVerticalPlanes = true;

    private ARPlaneManager planeManager;
    private PlaneDetectionMode lastRequestedDetectionMode = PlaneDetectionMode.None;
    private bool hasConfiguredDetectionMode;
    private float forceFullDetectionUntil;

    private void Awake()
    {
        ResolvePlaneManager();
        forceFullDetectionUntil = Time.unscaledTime + 2f;
        ConfigurePlaneDetection();
        HideAllPlaneVisuals();
    }

    private void OnEnable()
    {
        ResolvePlaneManager();
        if (planeManager != null)
        {
            planeManager.planesChanged += OnPlanesChanged;
        }
    }

    private void OnDisable()
    {
        if (planeManager != null)
        {
            planeManager.planesChanged -= OnPlanesChanged;
        }
    }

    private void Update()
    {
        ResolvePlaneManager();
        ConfigurePlaneDetection();
    }

    private void ResolvePlaneManager()
    {
        if (planeManager == null)
        {
            planeManager = FindObjectOfType<ARPlaneManager>();
        }
    }

    private void ConfigurePlaneDetection()
    {
        if (planeManager == null)
        {
            return;
        }

        PlaneDetectionMode mode;
        if (Time.unscaledTime < forceFullDetectionUntil)
        {
            mode = PlaneDetectionMode.Horizontal | PlaneDetectionMode.Vertical;
        }
        else
        {
            mode = PlaneDetectionMode.None;
            if (detectHorizontalPlanes)
            {
                mode |= PlaneDetectionMode.Horizontal;
            }

            if (detectVerticalPlanes)
            {
                mode |= PlaneDetectionMode.Vertical;
            }
        }

        if (hasConfiguredDetectionMode &&
            lastRequestedDetectionMode == mode &&
            planeManager.requestedDetectionMode == mode)
        {
            return;
        }

        planeManager.requestedDetectionMode = mode;
        lastRequestedDetectionMode = mode;
        hasConfiguredDetectionMode = true;
        ARDiagnostics.Report("PlaneDetectionMode solicitado: " + mode + " | actual: " + planeManager.currentDetectionMode);
    }

    private void OnPlanesChanged(ARPlanesChangedEventArgs args)
    {
        if (!hidePlaneVisuals)
        {
            return;
        }

        for (int i = 0; i < args.added.Count; i++)
        {
            RefreshPlaneVisuals(args.added[i]);
        }

        for (int i = 0; i < args.updated.Count; i++)
        {
            RefreshPlaneVisuals(args.updated[i]);
        }
    }

    private void HideAllPlaneVisuals()
    {
        if (!hidePlaneVisuals)
        {
            return;
        }

        ARPlane[] planes = FindObjectsOfType<ARPlane>(true);
        for (int i = 0; i < planes.Length; i++)
        {
            RefreshPlaneVisuals(planes[i]);
        }
    }

    private void RefreshPlaneVisuals(ARPlane plane)
    {
        if (plane == null)
        {
            return;
        }

        bool shouldShowPlane = hidePlaneVisuals &&
            showVerticalPlaneHints &&
            IsLikelyVerticalPlane(plane);

        Renderer[] renderers = plane.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (IsAppOverlay(renderers[i].gameObject))
            {
                continue;
            }

            renderers[i].enabled = shouldShowPlane;
        }

        LineRenderer[] lineRenderers = plane.GetComponentsInChildren<LineRenderer>(true);
        for (int i = 0; i < lineRenderers.Length; i++)
        {
            if (IsAppOverlay(lineRenderers[i].gameObject))
            {
                continue;
            }

            lineRenderers[i].enabled = shouldShowPlane;
        }

        Canvas[] canvases = plane.GetComponentsInChildren<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            canvases[i].enabled = false;
        }
    }

    private static bool IsAppOverlay(GameObject target)
    {
        return target != null && target.name == "ARWallColorOverlay";
    }

    private static bool IsLikelyVerticalPlane(ARPlane plane)
    {
        if (plane == null)
        {
            return false;
        }

        if (plane.alignment.IsVertical())
        {
            return true;
        }

        if (plane.alignment.IsHorizontal() || plane.normal.sqrMagnitude < 0.0001f)
        {
            return false;
        }

        float upDot = Mathf.Abs(Vector3.Dot(plane.normal.normalized, Vector3.up));
        return upDot <= 0.55f;
    }
}
