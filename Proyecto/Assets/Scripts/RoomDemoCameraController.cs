using UnityEngine;
using UnityEngine.EventSystems;

public class RoomDemoCameraController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField]
    private float moveSpeed = 2.5f;

    [SerializeField]
    private float sprintMultiplier = 1.6f;

    [SerializeField]
    private bool lockHeight = true;

    [SerializeField]
    private float fixedHeight = 2.4f;

    [Header("Look")]
    [SerializeField]
    private float mouseLookSensitivity = 2.2f;

    [SerializeField]
    private float touchLookSensitivity = 0.11f;

    [SerializeField]
    private float minPitch = -25f;

    [SerializeField]
    private float maxPitch = 35f;

    [Header("Mobile UI Controls")]
    [SerializeField]
    private bool useMobileUiControls = true;

    [SerializeField]
    private SimpleJoystick movementJoystick;

    [SerializeField]
    private TouchLookArea touchLookArea;

    [Header("Room Bounds")]
    [SerializeField]
    private bool clampToRoomBounds = true;

    [SerializeField]
    private Vector3 roomMin = new Vector3(-4f, 0f, -4f);

    [SerializeField]
    private Vector3 roomMax = new Vector3(4f, 4f, 4f);

    private float yaw;
    private float pitch;
    private bool isLookingWithMouse;

    private void Start()
    {
        Vector3 euler = transform.rotation.eulerAngles;
        yaw = euler.y;
        pitch = NormalizePitch(euler.x);

        if (lockHeight)
        {
            Vector3 p = transform.position;
            p.y = fixedHeight;
            transform.position = p;
        }
    }

    private void Update()
    {
        UpdateDesktopLook();
        UpdateTouchLook();
        UpdateDesktopMovement();
        UpdateTouchMovement();

        if (lockHeight || clampToRoomBounds)
        {
            ApplyPositionConstraints();
        }
    }

    private void UpdateDesktopLook()
    {
        if (Input.touchCount > 0)
        {
            return;
        }

        if (Input.GetMouseButtonDown(1))
        {
            isLookingWithMouse = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else if (Input.GetMouseButtonUp(1))
        {
            isLookingWithMouse = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (!isLookingWithMouse)
        {
            return;
        }

        float lookX = Input.GetAxis("Mouse X") * mouseLookSensitivity;
        float lookY = Input.GetAxis("Mouse Y") * mouseLookSensitivity;
        ApplyLookDelta(lookX, -lookY);
    }

    private void UpdateTouchLook()
    {
        if (useMobileUiControls && touchLookArea != null)
        {
            Vector2 uiDelta = touchLookArea.ConsumeLookDelta();
            if (uiDelta.sqrMagnitude > 0.0001f)
            {
                ApplyLookDelta(uiDelta.x * touchLookSensitivity, -uiDelta.y * touchLookSensitivity);
            }

            return;
        }

        if (Input.touchCount != 1)
        {
            return;
        }

        Touch touch = Input.GetTouch(0);
        if (touch.phase != TouchPhase.Moved)
        {
            return;
        }

        if (IsTouchOverUI(touch.fingerId))
        {
            return;
        }

        Vector2 delta = touch.deltaPosition * touchLookSensitivity;
        ApplyLookDelta(delta.x, -delta.y);
    }

    private void UpdateDesktopMovement()
    {
        if (Input.touchCount > 0)
        {
            return;
        }

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 input = new Vector3(h, 0f, v);
        if (input.sqrMagnitude <= 0.001f)
        {
            return;
        }

        float speed = moveSpeed;
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            speed *= sprintMultiplier;
        }

        MoveByInput(input.normalized, speed);
    }

    private void UpdateTouchMovement()
    {
        if (useMobileUiControls && movementJoystick != null)
        {
            Vector2 stick = movementJoystick.Input;
            if (stick.sqrMagnitude > 0.0001f)
            {
                MoveByInput(new Vector3(stick.x, 0f, stick.y), moveSpeed);
            }

            return;
        }

        if (Input.touchCount < 2)
        {
            return;
        }

        Touch t0 = Input.GetTouch(0);
        Touch t1 = Input.GetTouch(1);
        if (t0.phase != TouchPhase.Moved && t1.phase != TouchPhase.Moved)
        {
            return;
        }

        if (IsTouchOverUI(t0.fingerId) || IsTouchOverUI(t1.fingerId))
        {
            return;
        }

        Vector2 avgDelta = (t0.deltaPosition + t1.deltaPosition) * 0.5f;
        Vector3 input = new Vector3(avgDelta.x, 0f, avgDelta.y * 1.2f);
        if (input.sqrMagnitude <= 0.001f)
        {
            return;
        }

        MoveByInput(input.normalized, moveSpeed * 0.75f);
    }

    private void ApplyLookDelta(float deltaYaw, float deltaPitch)
    {
        yaw += deltaYaw;
        pitch = Mathf.Clamp(pitch + deltaPitch, minPitch, maxPitch);
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void MoveByInput(Vector3 input, float speed)
    {
        Vector3 move = (transform.right * input.x + transform.forward * input.z);
        move.y = 0f;
        transform.position += move * speed * Time.deltaTime;
    }

    private void ApplyPositionConstraints()
    {
        Vector3 p = transform.position;
        if (lockHeight)
        {
            p.y = fixedHeight;
        }

        if (clampToRoomBounds)
        {
            p.x = Mathf.Clamp(p.x, roomMin.x, roomMax.x);
            p.y = Mathf.Clamp(p.y, roomMin.y, roomMax.y);
            p.z = Mathf.Clamp(p.z, roomMin.z, roomMax.z);
        }

        transform.position = p;
    }

    private static float NormalizePitch(float x)
    {
        return x > 180f ? x - 360f : x;
    }

    private static bool IsTouchOverUI(int fingerId)
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(fingerId);
    }

    public void SetLookSensitivity(float sensitivity)
    {
        float safe = Mathf.Max(0.01f, sensitivity);
        mouseLookSensitivity = safe;
        touchLookSensitivity = safe * 0.07f;
    }
}
