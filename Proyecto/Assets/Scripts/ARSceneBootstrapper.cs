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
        Type arAnchorManagerType = GetTypeFromAssemblies("UnityEngine.XR.ARFoundation.ARAnchorManager");

        if (arSessionType == null || xROriginType == null)
        {
            return false;
        }

        GameObject sessionObject = GameObject.Find("AR Session") ?? new GameObject("AR Session");
        AddComponentIfMissing(sessionObject, arSessionType);

        GameObject originObject = GameObject.Find("XR Origin") ?? new GameObject("XR Origin");
        Component origin = AddComponentIfMissing(originObject, xROriginType);

        Transform cameraOffset = GetOrCreateCameraOffset(originObject.transform);
        Camera arCamera = FindBestARCamera(originObject.transform, arCameraManagerType, arCameraBackgroundType);
        if (arCamera == null)
        {
            GameObject cameraObject = new GameObject("AR Camera");
            arCamera = cameraObject.AddComponent<Camera>();
        }

        arCamera.gameObject.tag = "MainCamera";
        arCamera.enabled = true;
        arCamera.transform.SetParent(cameraOffset, false);
        arCamera.transform.localPosition = Vector3.zero;
        arCamera.transform.localRotation = Quaternion.identity;
        arCamera.clearFlags = CameraClearFlags.SolidColor;
        arCamera.backgroundColor = Color.black;

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
            Component planeManager = AddComponentIfMissing(originObject, arPlaneManagerType);
            SetPlaneDetectionMode(planeManager);
        }

        if (arRaycastManagerType != null)
        {
            AddComponentIfMissing(originObject, arRaycastManagerType);
        }

        if (arAnchorManagerType != null)
        {
            AddComponentIfMissing(originObject, arAnchorManagerType);
        }

        SetOriginCamera(origin, arCamera);
        SetOriginCameraOffset(origin, cameraOffset);
        DisableDuplicateCameras(arCamera);
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

    private static Transform GetOrCreateCameraOffset(Transform originTransform)
    {
        Transform cameraOffset = originTransform.Find("Camera Offset");
        if (cameraOffset != null)
        {
            return cameraOffset;
        }

        GameObject cameraOffsetObject = new GameObject("Camera Offset");
        cameraOffsetObject.transform.SetParent(originTransform, false);
        return cameraOffsetObject.transform;
    }

    private static Camera FindBestARCamera(Transform originTransform, Type arCameraManagerType, Type arCameraBackgroundType)
    {
        Camera[] cameras = FindObjectsOfType<Camera>(true);

        foreach (Camera camera in cameras)
        {
            if (HasComponent(camera.gameObject, arCameraBackgroundType) || HasComponent(camera.gameObject, arCameraManagerType))
            {
                return camera;
            }
        }

        Transform cameraOffset = originTransform.Find("Camera Offset");
        if (cameraOffset != null)
        {
            Camera offsetCamera = cameraOffset.GetComponentInChildren<Camera>(true);
            if (offsetCamera != null)
            {
                return offsetCamera;
            }
        }

        foreach (Camera camera in cameras)
        {
            if (camera.name == "AR Camera")
            {
                return camera;
            }
        }

        return Camera.main;
    }

    private static bool HasComponent(GameObject target, Type componentType)
    {
        return componentType != null && target.GetComponent(componentType) != null;
    }

    private static void DisableDuplicateCameras(Camera arCamera)
    {
        foreach (Camera camera in FindObjectsOfType<Camera>(true))
        {
            if (camera == arCamera)
            {
                continue;
            }

            camera.enabled = false;
            if (camera.CompareTag("MainCamera"))
            {
                camera.tag = "Untagged";
            }

            AudioListener audioListener = camera.GetComponent<AudioListener>();
            if (audioListener != null)
            {
                audioListener.enabled = false;
            }
        }
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

    private static void SetOriginCameraOffset(Component origin, Transform cameraOffset)
    {
        if (origin == null || cameraOffset == null)
        {
            return;
        }

        PropertyInfo cameraOffsetProperty = origin.GetType().GetProperty("CameraFloorOffsetObject");
        if (cameraOffsetProperty != null && cameraOffsetProperty.CanWrite)
        {
            cameraOffsetProperty.SetValue(origin, cameraOffset.gameObject);
        }
    }

    private static void SetPlaneDetectionMode(Component planeManager)
    {
        if (planeManager == null)
        {
            return;
        }

        Type planeDetectionModeType = GetTypeFromAssemblies("UnityEngine.XR.ARSubsystems.PlaneDetectionMode");
        if (planeDetectionModeType == null)
        {
            return;
        }

        object horizontalAndVertical = Enum.Parse(planeDetectionModeType, "Horizontal, Vertical");
        PropertyInfo requestedDetectionModeProperty = planeManager.GetType().GetProperty("requestedDetectionMode");
        if (requestedDetectionModeProperty != null && requestedDetectionModeProperty.CanWrite)
        {
            requestedDetectionModeProperty.SetValue(planeManager, horizontalAndVertical);
        }
    }
}
