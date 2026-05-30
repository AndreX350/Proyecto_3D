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
        if (!showOverlay || !ARDiagnostics.Enabled || string.IsNullOrEmpty(cachedText))
        {
            return;
        }

        const int width = 600;
        const int height = 430;
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
        int other = 0;
        int tracking = 0;
        string requestedDetectionMode = "N/A";
        string currentDetectionMode = "N/A";
        bool hasPlanePrefab = false;
        if (planeManager != null)
        {
            requestedDetectionMode = planeManager.requestedDetectionMode.ToString();
            currentDetectionMode = planeManager.currentDetectionMode.ToString();
            hasPlanePrefab = planeManager.planePrefab != null;

            foreach (ARPlane plane in planeManager.trackables)
            {
                if (plane.trackingState != TrackingState.None)
                {
                    tracking++;
                }

                if (plane.alignment.IsHorizontal())
                {
                    horizontal++;
                }
                else if (plane.alignment.IsVertical())
                {
                    vertical++;
                }
                else
                {
                    other++;
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
            vertical,
            other,
            tracking,
            requestedDetectionMode,
            currentDetectionMode,
            hasPlanePrefab);
    }
}
