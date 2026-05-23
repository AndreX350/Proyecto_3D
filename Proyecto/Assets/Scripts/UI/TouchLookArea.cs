using UnityEngine;
using UnityEngine.EventSystems;

public class TouchLookArea : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    [SerializeField]
    private float dragMultiplier = 1f;

    private Vector2 lookDelta;
    private int activePointerId = int.MinValue;

    public Vector2 LookDelta => lookDelta;

    public void OnPointerDown(PointerEventData eventData)
    {
        activePointerId = eventData.pointerId;
        lookDelta = Vector2.zero;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointerId)
        {
            return;
        }

        lookDelta = eventData.delta * dragMultiplier;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointerId)
        {
            return;
        }

        activePointerId = int.MinValue;
        lookDelta = Vector2.zero;
    }

    private void LateUpdate()
    {
        // Consume one-frame delta so camera reads only fresh drag.
        lookDelta = Vector2.zero;
    }
}
