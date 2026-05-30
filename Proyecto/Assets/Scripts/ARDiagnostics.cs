using System.Text;
using UnityEngine;

public static class ARDiagnostics
{
    private static bool runtimeEnabled = Debug.isDebugBuild;
    private static string lastStatus = "Sin eventos AR aun.";
    private static float lastStatusTime;

    public static bool Enabled => runtimeEnabled;

    public static string LastStatus => lastStatus;

    public static void Report(string status)
    {
        if (!Enabled || string.IsNullOrEmpty(status))
        {
            return;
        }

        lastStatus = status;
        lastStatusTime = Time.unscaledTime;
        Debug.Log("[AR-DEBUG] " + status);
    }

    public static bool ToggleRuntimeEnabled()
    {
        runtimeEnabled = !runtimeEnabled;
        Report("AR DEBUG " + (runtimeEnabled ? "ACTIVADO" : "DESACTIVADO"));
        return runtimeEnabled;
    }

    public static void SetRuntimeEnabled(bool enabled)
    {
        runtimeEnabled = enabled;
        Report("AR DEBUG " + (runtimeEnabled ? "ACTIVADO" : "DESACTIVADO"));
    }

    public static string BuildOverlayText(
        bool arPlacementEnabled,
        bool hasRaycastManager,
        bool hasPlaneManager,
        bool hasAnchorManager,
        bool hasSelectedFurniture,
        int detectedHorizontalPlanes,
        int detectedVerticalPlanes,
        int detectedOtherPlanes,
        int trackingPlanes,
        string requestedDetectionMode,
        string currentDetectionMode,
        bool hasPlanePrefab)
    {
        StringBuilder builder = new StringBuilder(256);
        builder.AppendLine("AR DEBUG");
        builder.Append("Placement: ").AppendLine(arPlacementEnabled ? "ON" : "OFF");
        builder.Append("ARRaycastManager: ").AppendLine(hasRaycastManager ? "OK" : "MISSING");
        builder.Append("ARPlaneManager: ").AppendLine(hasPlaneManager ? "OK" : "MISSING");
        builder.Append("Plane Prefab: ").AppendLine(hasPlanePrefab ? "OK" : "MISSING");
        builder.Append("Requested Mode: ").AppendLine(requestedDetectionMode);
        builder.Append("Current Mode: ").AppendLine(currentDetectionMode);
        builder.Append("ARAnchorManager: ").AppendLine(hasAnchorManager ? "OK" : "MISSING");
        builder.Append("Mueble seleccionado: ").AppendLine(hasSelectedFurniture ? "SI" : "NO");
        builder.Append("Planos H detectados: ").AppendLine(detectedHorizontalPlanes.ToString());
        builder.Append("Planos V detectados: ").AppendLine(detectedVerticalPlanes.ToString());
        builder.Append("Planos otros: ").AppendLine(detectedOtherPlanes.ToString());
        builder.Append("Planos tracking: ").AppendLine(trackingPlanes.ToString());
        builder.Append("Ultimo evento: ").AppendLine(lastStatus);
        builder.Append("Hace: ").Append((Time.unscaledTime - lastStatusTime).ToString("0.0")).AppendLine("s");
        return builder.ToString();
    }
}
