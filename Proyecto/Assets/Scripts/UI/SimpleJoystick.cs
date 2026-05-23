using UnityEngine;
using UnityEngine.EventSystems;

public class SimpleJoystick : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    [SerializeField]
    private RectTransform background;

    [SerializeField]
    private RectTransform handle;

    [SerializeField]
    private float handleRange = 55f;

    [SerializeField]
    private bool hideWhenIdle = false;

    [SerializeField]
    private CanvasGroup canvasGroup;

    private Vector2 input;

    public Vector2 Input => input;

    private void Awake()
    {
        if (background == null)
        {
            background = transform as RectTransform;
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        ApplyIdleVisualState();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        OnDrag(eventData);
        ApplyActiveVisualState();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (background == null)
        {
            return;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                background,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint))
        {
            return;
        }

        float radius = Mathf.Max(1f, handleRange);
        Vector2 normalized = localPoint / radius;
        input = Vector2.ClampMagnitude(normalized, 1f);

        if (handle != null)
        {
            handle.anchoredPosition = input * radius;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        ResetJoystick();
    }

    public void ResetJoystick()
    {
        input = Vector2.zero;

        if (handle != null)
        {
            handle.anchoredPosition = Vector2.zero;
        }

        ApplyIdleVisualState();
    }

    private void ApplyIdleVisualState()
    {
        if (canvasGroup == null)
        {
            return;
        }

        if (hideWhenIdle)
        {
            canvasGroup.alpha = 0.35f;
        }
    }

    private void ApplyActiveVisualState()
    {
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.alpha = 1f;
    }
}
