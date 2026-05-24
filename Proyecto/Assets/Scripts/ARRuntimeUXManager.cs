using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ARRuntimeUXManager : MonoBehaviour
{
    [SerializeField]
    private string fallbackSceneName = "RoomDemo";

    [SerializeField]
    private bool showScanGuide = true;

    [SerializeField]
    private float unsupportedDelaySeconds = 2.5f;

    private ARPlaneManager planeManager;
    private float sceneStartTime;
    private GUIStyle textStyle;
    private GUIStyle titleStyle;
    private string compatMessage;
    private bool showCompatFallback;
    private float firstTapTime = -1f;
    private int tapCount;

    private void Awake()
    {
        planeManager = FindObjectOfType<ARPlaneManager>();
        sceneStartTime = Time.unscaledTime;

        titleStyle = new GUIStyle
        {
            fontSize = 28,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white },
            alignment = TextAnchor.MiddleCenter
        };

        textStyle = new GUIStyle
        {
            fontSize = 22,
            normal = { textColor = Color.white },
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true
        };
    }

    private void Update()
    {
        if (planeManager == null)
        {
            planeManager = FindObjectOfType<ARPlaneManager>();
        }

        EvaluateCompatibilityState();
        HandleDebugToggleGesture();
    }

    private void OnGUI()
    {
        if (showScanGuide)
        {
            DrawScanGuide();
        }

        if (showCompatFallback)
        {
            DrawCompatibilityFallback();
        }
    }

    private void DrawScanGuide()
    {
        CountPlanes(out int horizontal, out int vertical);
        string guidance;

        if (horizontal <= 0 && vertical <= 0)
        {
            guidance = "Escanea piso y paredes moviendo el celular lentamente.";
        }
        else if (horizontal <= 0)
        {
            guidance = "Pared detectada. Ahora apunta al piso para colocar muebles.";
        }
        else if (vertical <= 0)
        {
            guidance = "Piso detectado. Ahora apunta a una pared para poder pintarla.";
        }
        else
        {
            guidance = "Listo: piso y paredes detectadas.";
        }

        Rect box = new Rect(20, 120, Screen.width - 40, 86);
        GUI.color = new Color(0f, 0f, 0f, 0.45f);
        GUI.Box(box, GUIContent.none);
        GUI.color = Color.white;
        GUI.Label(
            box,
            guidance + "\nPlanos H: " + horizontal + " | Planos V: " + vertical,
            textStyle);
    }

    private void DrawCompatibilityFallback()
    {
        Rect panel = new Rect(36, Screen.height * 0.2f, Screen.width - 72, Screen.height * 0.5f);
        GUI.color = new Color(0f, 0f, 0f, 0.8f);
        GUI.Box(panel, GUIContent.none);
        GUI.color = Color.white;

        GUI.Label(new Rect(panel.x + 20, panel.y + 30, panel.width - 40, 40), "AR no disponible", titleStyle);
        GUI.Label(
            new Rect(panel.x + 20, panel.y + 90, panel.width - 40, panel.height - 170),
            compatMessage,
            textStyle);

        if (GUI.Button(new Rect(panel.x + 40, panel.yMax - 70, panel.width - 80, 44), "Volver a RoomDemo"))
        {
            SceneManager.LoadScene(fallbackSceneName);
        }
    }

    private void EvaluateCompatibilityState()
    {
        if (showCompatFallback)
        {
            return;
        }

        if (Time.unscaledTime - sceneStartTime < unsupportedDelaySeconds)
        {
            return;
        }

        ARSessionState state = ARSession.state;
        if (state == ARSessionState.Unsupported)
        {
            ShowFallback("Tu dispositivo no soporta ARCore/AR Foundation.");
            return;
        }

        if (state == ARSessionState.NeedsInstall || state == ARSessionState.Installing)
        {
            ShowFallback("Se requiere Google Play Services for AR y no pudo inicializarse.");
            return;
        }
    }

    private void ShowFallback(string message)
    {
        compatMessage = message + "\n\nPuedes continuar en modo RoomDemo mientras usas un dispositivo compatible.";
        showCompatFallback = true;
    }

    private void CountPlanes(out int horizontal, out int vertical)
    {
        horizontal = 0;
        vertical = 0;

        if (planeManager == null)
        {
            return;
        }

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

    private void HandleDebugToggleGesture()
    {
        if (Input.touchCount != 1)
        {
            return;
        }

        Touch touch = Input.GetTouch(0);
        if (touch.phase != TouchPhase.Began)
        {
            return;
        }

        bool inLogoArea = touch.position.x <= Screen.width * 0.34f && touch.position.y >= Screen.height * 0.82f;
        if (!inLogoArea)
        {
            return;
        }

        float now = Time.unscaledTime;
        if (firstTapTime < 0f || now - firstTapTime > 0.9f)
        {
            firstTapTime = now;
            tapCount = 1;
            return;
        }

        tapCount++;
        if (tapCount >= 3)
        {
            ARDiagnostics.ToggleRuntimeEnabled();
            tapCount = 0;
            firstTapTime = -1f;
        }
    }
}
