using System.Text;
using UnityEngine;

public static class ARDiagnostics
{
    private static string lastStatus = "Sin eventos AR aun.";
    private static float lastStatusTime;

    public static bool Enabled => Debug.isDebugBuild;

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

    public static string BuildOverlayText(
        bool arPlacementEnabled,
        bool hasRaycastManager,
        bool hasPlaneManager,
        bool hasAnchorManager,
        bool hasSelectedFurniture,
        int detectedHorizontalPlanes,
        int detectedVerticalPlanes)
    {
        StringBuilder builder = new StringBuilder(256);
        builder.AppendLine("AR DEBUG");
        builder.Append("Placement: ").AppendLine(arPlacementEnabled ? "ON" : "OFF");
        builder.Append("ARRaycastManager: ").AppendLine(hasRaycastManager ? "OK" : "MISSING");
        builder.Append("ARPlaneManager: ").AppendLine(hasPlaneManager ? "OK" : "MISSING");
        builder.Append("ARAnchorManager: ").AppendLine(hasAnchorManager ? "OK" : "MISSING");
        builder.Append("Mueble seleccionado: ").AppendLine(hasSelectedFurniture ? "SI" : "NO");
        builder.Append("Planos H detectados: ").AppendLine(detectedHorizontalPlanes.ToString());
        builder.Append("Planos V detectados: ").AppendLine(detectedVerticalPlanes.ToString());
        builder.Append("Ultimo evento: ").AppendLine(lastStatus);
        builder.Append("Hace: ").Append((Time.unscaledTime - lastStatusTime).ToString("0.0")).AppendLine("s");
        return builder.ToString();
    }
}
