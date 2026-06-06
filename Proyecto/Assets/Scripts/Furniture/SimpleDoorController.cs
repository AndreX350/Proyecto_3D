using UnityEngine;

[DisallowMultipleComponent]
public class SimpleDoorController : MonoBehaviour
{
    [Header("Door Angles")]
    [SerializeField]
    private float closedAngle = 0f;

    [SerializeField]
    private float openAngle = -95f;

    [SerializeField]
    private float openSpeed = 180f;

    [Header("Interaction")]
    [SerializeField]
    private Camera interactionCamera;

    [SerializeField]
    private float maxDistance = 4f;

    private bool isOpen;
    private float currentAngle;
    private Collider cachedCollider;

    private void Awake()
    {
        cachedCollider = GetComponent<Collider>();
        currentAngle = closedAngle;
        ApplyAngleInstant(currentAngle);
    }

    private void Update()
    {
        float targetAngle = isOpen ? openAngle : closedAngle;
        currentAngle = Mathf.MoveTowards(currentAngle, targetAngle, openSpeed * Time.deltaTime);
        ApplyAngleInstant(currentAngle);
    }

    public void ToggleDoor()
    {
        isOpen = !isOpen;
    }

    public void RotateDoor(float degrees)
    {
        closedAngle += degrees;
        openAngle += degrees;
        currentAngle += degrees;
    }

    private void HandlePointerInput()
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                TryToggleFromScreenPoint(touch.position);
            }

            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            TryToggleFromScreenPoint(Input.mousePosition);
        }
    }

    private void TryToggleFromScreenPoint(Vector3 screenPoint)
    {
        Camera activeCamera = interactionCamera != null ? interactionCamera : Camera.main;
        if (activeCamera == null || cachedCollider == null)
        {
            return;
        }

        Ray ray = activeCamera.ScreenPointToRay(screenPoint);
        if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance))
        {
            return;
        }

        if (hit.collider == cachedCollider)
        {
            ToggleDoor();
        }
    }

    private void ApplyAngleInstant(float angle)
    {
        transform.localRotation = Quaternion.Euler(0f, angle, 0f);
    }
}
