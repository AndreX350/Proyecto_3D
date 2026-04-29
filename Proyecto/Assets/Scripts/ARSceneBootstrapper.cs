using System;
using System.Reflection;
using UnityEngine;

public class ARSceneBootstrapper : MonoBehaviour
{
    [SerializeField]
    private bool hideDemoRoomObjects = true;

    private void Start()
    {
        if (hideDemoRoomObjects)
        {
            SetDemoRoomVisible(false);
        }

        if (!TryCreateARFoundationRig())
        {
            Debug.LogWarning("ARSceneBootstrapper: AR Foundation aun no esta importado. Abre Unity y deja que Package Manager restaure AR Foundation/ARCore.");
            SetDemoRoomVisible(true);
        }
    }

    private bool TryCreateARFoundationRig()
    {
        Type arSessionType = GetTypeFromAssemblies("UnityEngine.XR.ARFoundation.ARSession");
        Type xROriginType = GetTypeFromAssemblies("Unity.XR.CoreUtils.XROrigin");
        Type arCameraManagerType = GetTypeFromAssemblies("UnityEngine.XR.ARFoundation.ARCameraManager");
        Type arCameraBackgroundType = GetTypeFromAssemblies("UnityEngine.XR.ARFoundation.ARCameraBackground");
        Type arPlaneManagerType = GetTypeFromAssemblies("UnityEngine.XR.ARFoundation.ARPlaneManager");
        Type arRaycastManagerType = GetTypeFromAssemblies("UnityEngine.XR.ARFoundation.ARRaycastManager");

        if (arSessionType == null || xROriginType == null)
        {
            return false;
        }

        GameObject sessionObject = GameObject.Find("AR Session") ?? new GameObject("AR Session");
        AddComponentIfMissing(sessionObject, arSessionType);

        GameObject originObject = GameObject.Find("XR Origin") ?? new GameObject("XR Origin");
        Component origin = AddComponentIfMissing(originObject, xROriginType);

        Camera arCamera = Camera.main;
        if (arCamera == null)
        {
            GameObject cameraObject = new GameObject("AR Camera");
            arCamera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
        }

        arCamera.transform.SetParent(originObject.transform, false);
        arCamera.clearFlags = CameraClearFlags.SolidColor;

        if (arCameraManagerType != null)
        {
            AddComponentIfMissing(arCamera.gameObject, arCameraManagerType);
        }

        if (arCameraBackgroundType != null)
        {
            AddComponentIfMissing(arCamera.gameObject, arCameraBackgroundType);
        }

        if (arPlaneManagerType != null)
        {
            AddComponentIfMissing(originObject, arPlaneManagerType);
        }

        if (arRaycastManagerType != null)
        {
            AddComponentIfMissing(originObject, arRaycastManagerType);
        }

        SetOriginCamera(origin, arCamera);
        Debug.Log("ARSceneBootstrapper: AR Foundation rig listo.");
        return true;
    }

    private void SetDemoRoomVisible(bool visible)
    {
        string[] demoObjectNames =
        {
            "Wall_Left",
            "Wall_Right",
            "Wall_Back",
            "Room_Floor",
            "ScanArea"
        };

        foreach (string objectName in demoObjectNames)
        {
            GameObject sceneObject = GameObject.Find(objectName);
            if (sceneObject != null)
            {
                sceneObject.SetActive(visible);
            }
        }
    }

    private static Type GetTypeFromAssemblies(string typeName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(typeName);
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }

    private static Component AddComponentIfMissing(GameObject target, Type componentType)
    {
        Component existing = target.GetComponent(componentType);
        if (existing != null)
        {
            return existing;
        }

        return target.AddComponent(componentType);
    }

    private static void SetOriginCamera(Component origin, Camera camera)
    {
        if (origin == null || camera == null)
        {
            return;
        }

        PropertyInfo cameraProperty = origin.GetType().GetProperty("Camera");
        if (cameraProperty != null && cameraProperty.CanWrite)
        {
            cameraProperty.SetValue(origin, camera);
        }
    }
}
