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

    [SerializeField]
    private Transform movementRoot;

    [SerializeField]
    private CharacterController characterController;

    [Header("Look")]
    [SerializeField]
    private float mouseLookSensitivity = 2.2f;

    [SerializeField]
    private float touchLookSensitivity = 0.11f;

    [SerializeField]
    private float minPitch = -25f;

    [SerializeField]
    private float maxPitch = 35f;

    [SerializeField]
    private bool useMobileUiControls = true;

    [SerializeField]
    private SimpleJoystick movementJoystick;

    [SerializeField]
    private TouchLookArea touchLookArea;

    private float yaw;
    private float pitch;
    private bool isLookingWithMouse;
    private float cameraLocalHeight;

    private void Start()
    {
        ResolveMovementReferences();

        Transform lookTransform = GetLookTransform();
        Transform bodyTransform = GetMovementRoot();

        if (bodyTransform != null && bodyTransform != lookTransform)
        {
            cameraLocalHeight = lookTransform.localPosition.y;
            lookTransform.localPosition = new Vector3(0f, cameraLocalHeight, 0f);
        }
        else
        {
            cameraLocalHeight = 0f;
        }

        Vector3 euler = lookTransform.rotation.eulerAngles;
        yaw = euler.y;
        pitch = NormalizePitch(euler.x);

        if (bodyTransform != null)
        {
            Vector3 bodyEuler = bodyTransform.rotation.eulerAngles;
            yaw = bodyEuler.y;
        }

        ApplyLookRotation();

        if (lockHeight)
        {
            Vector3 p = bodyTransform != null ? bodyTransform.position : transform.position;
            p.y = fixedHeight;
            if (bodyTransform != null)
            {
                bodyTransform.position = p;
            }
            else
            {
                transform.position = p;
            }
        }
    }

    private void Update()
    {
        ResolveMovementReferences();
        NormalizeCameraLocalOffset();
        UpdateDesktopLook();
        UpdateTouchLook();
        UpdateDesktopMovement();
        UpdateTouchMovement();
        ApplyPositionConstraints();
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
        ApplyLookRotation();
    }

    private void MoveByInput(Vector3 input, float speed)
    {
        Transform bodyTransform = GetMovementRoot();
        Transform basisTransform = bodyTransform != null ? bodyTransform : transform;

        Vector3 move = (basisTransform.right * input.x + basisTransform.forward * input.z);
        move.y = 0f;
        Vector3 delta = move * speed * Time.deltaTime;

        if (characterController != null && characterController.enabled)
        {
            characterController.Move(delta);
            return;
        }

        if (bodyTransform != null)
        {
            bodyTransform.position += delta;
            return;
        }

        transform.position += delta;
    }

    private void ApplyPositionConstraints()
    {
        if (!lockHeight)
        {
            return;
        }

        Transform bodyTransform = GetMovementRoot();
        if (bodyTransform == null)
        {
            return;
        }

        Vector3 p = bodyTransform.position;
        if (lockHeight)
        {
            p.y = fixedHeight;
        }

        bodyTransform.position = p;
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

    private void ResolveMovementReferences()
    {
        if (movementRoot == null)
        {
            movementRoot = transform.parent != null ? transform.parent : transform;
        }

        if (characterController == null && movementRoot != null)
        {
            characterController = movementRoot.GetComponent<CharacterController>();
        }
    }

    private void NormalizeCameraLocalOffset()
    {
        Transform bodyTransform = GetMovementRoot();
        if (bodyTransform == null || bodyTransform == transform)
        {
            return;
        }

        Vector3 localPosition = transform.localPosition;
        if (Mathf.Abs(localPosition.x) > 0.0001f ||
            Mathf.Abs(localPosition.z) > 0.0001f ||
            Mathf.Abs(localPosition.y - cameraLocalHeight) > 0.0001f)
        {
            transform.localPosition = new Vector3(0f, cameraLocalHeight, 0f);
        }
    }

    private Transform GetMovementRoot()
    {
        return movementRoot != null ? movementRoot : transform;
    }

    private Transform GetLookTransform()
    {
        return transform;
    }

    private void ApplyLookRotation()
    {
        Transform bodyTransform = GetMovementRoot();
        if (bodyTransform != null && bodyTransform != transform)
        {
            bodyTransform.rotation = Quaternion.Euler(0f, yaw, 0f);
            transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            return;
        }

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }
}
