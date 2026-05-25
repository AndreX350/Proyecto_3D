using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ARSurfaceDetectionManager : MonoBehaviour
{
    [SerializeField]
    private bool hidePlaneVisuals = true;

    [SerializeField]
    private bool detectHorizontalPlanes = true;

    [SerializeField]
    private bool detectVerticalPlanes = true;

    private ARPlaneManager planeManager;
    private void Awake()
    {
        ResolvePlaneManager();
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

        PlaneDetectionMode mode = PlaneDetectionMode.None;
        if (detectHorizontalPlanes)
        {
            mode |= PlaneDetectionMode.Horizontal;
        }

        if (detectVerticalPlanes)
        {
            mode |= PlaneDetectionMode.Vertical;
        }
        else if (!detectHorizontalPlanes)
        {
            mode |= PlaneDetectionMode.Vertical;
        }

        planeManager.requestedDetectionMode = mode;
        ARDiagnostics.Report("PlaneDetectionMode activo: " + mode);
    }

    private void OnPlanesChanged(ARPlanesChangedEventArgs args)
    {
        if (!hidePlaneVisuals)
        {
            return;
        }

        for (int i = 0; i < args.added.Count; i++)
        {
            HidePlaneVisuals(args.added[i]);
        }

        for (int i = 0; i < args.updated.Count; i++)
        {
            HidePlaneVisuals(args.updated[i]);
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
            HidePlaneVisuals(planes[i]);
        }
    }

    private static void HidePlaneVisuals(ARPlane plane)
    {
        if (plane == null)
        {
            return;
        }

        Renderer[] renderers = plane.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (IsAppOverlay(renderers[i].gameObject))
            {
                continue;
            }

            renderers[i].enabled = false;
        }

        LineRenderer[] lineRenderers = plane.GetComponentsInChildren<LineRenderer>(true);
        for (int i = 0; i < lineRenderers.Length; i++)
        {
            lineRenderers[i].enabled = false;
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
}
