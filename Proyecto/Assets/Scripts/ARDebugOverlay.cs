using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ARDebugOverlay : MonoBehaviour
{
    [SerializeField]
    private FurniturePlacementManager placementManager;

    [SerializeField]
    private ARPlaneManager planeManager;

    [SerializeField]
    private bool showOverlay = true;

    private GUIStyle labelStyle;
    private string cachedText = string.Empty;
    private float nextRefreshTime;

    private void Awake()
    {
        if (!ARDiagnostics.Enabled)
        {
            enabled = false;
            return;
        }

        if (placementManager == null)
        {
            placementManager = FindObjectOfType<FurniturePlacementManager>();
        }

        if (planeManager == null)
        {
            planeManager = FindObjectOfType<ARPlaneManager>();
        }

        labelStyle = new GUIStyle
        {
            fontSize = 20,
            richText = false,
            normal = { textColor = Color.white }
        };
    }

    private void Update()
    {
        if (!showOverlay || Time.unscaledTime < nextRefreshTime)
        {
            return;
        }

        nextRefreshTime = Time.unscaledTime + 0.2f;
        RefreshText();
    }

    private void OnGUI()
    {
        if (!showOverlay || string.IsNullOrEmpty(cachedText))
        {
            return;
        }

        const int width = 520;
        const int height = 330;
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.Box(new Rect(18, 18, width, height), GUIContent.none);
        GUI.color = Color.white;
        GUI.Label(new Rect(28, 28, width - 20, height - 20), cachedText, labelStyle);
    }

    private void RefreshText()
    {
        if (placementManager == null)
        {
            placementManager = FindObjectOfType<FurniturePlacementManager>();
        }

        if (planeManager == null)
        {
            planeManager = FindObjectOfType<ARPlaneManager>();
        }

        bool hasPlacement = placementManager != null;
        bool hasRaycast = hasPlacement && placementManager.HasARRaycastManager;
        bool hasPlaneManager = hasPlacement && placementManager.HasARPlaneManager;
        bool hasAnchor = hasPlacement && placementManager.HasARAnchorManager;
        bool hasSelectedFurniture = hasPlacement && placementManager.HasSelectedFurnitureForAR;

        int horizontal = 0;
        int vertical = 0;
        if (planeManager != null)
        {
            foreach (ARPlane plane in planeManager.trackables)
            {
                if (plane.alignment == PlaneAlignment.HorizontalUp)
                {
                    horizontal++;
                }
                else if (plane.alignment == PlaneAlignment.Vertical)
                {
                    vertical++;
                }
            }
        }

        cachedText = ARDiagnostics.BuildOverlayText(
            hasPlacement && placementManager.IsAREnabled,
            hasRaycast,
            hasPlaneManager,
            hasAnchor,
            hasSelectedFurniture,
            horizontal,
            vertical);
    }
}
