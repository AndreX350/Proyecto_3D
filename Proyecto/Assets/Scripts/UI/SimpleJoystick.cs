using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SimpleJoystick : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    [SerializeField]
    private RectTransform background;

    [SerializeField]
    private RectTransform handle;

    [SerializeField]
    private float handleRange = 55f;

    [SerializeField]
    private float deadZone = 0.08f;

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

        // El joystick debe quedar por encima de las zonas de look/otros panels
        // para que reciba los eventos de pointer aunque exista UI transparente encima.
        transform.SetAsLastSibling();

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        NormalizeHandleLayout();
        GenerateCircularHandleSprite();
        ResetJoystick();

        ApplyIdleVisualState();
    }

    private void NormalizeHandleLayout()
    {
        if (handle == null)
        {
            return;
        }

        handle.anchorMin = new Vector2(0.5f, 0.5f);
        handle.anchorMax = new Vector2(0.5f, 0.5f);
        handle.pivot = new Vector2(0.5f, 0.5f);
        handle.anchoredPosition = Vector2.zero;
    }

    private void GenerateCircularHandleSprite()
    {
        if (handle == null) return;

        Image handleImage = handle.GetComponent<Image>();
        if (handleImage == null) return;

        if (handleImage.sprite != null) return;

        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        float center = size * 0.5f;
        float radius = size * 0.45f;
        float radiusSq = radius * radius;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float distSq = dx * dx + dy * dy;
                Color c = distSq <= radiusSq ? Color.white : Color.clear;

                if (distSq > radiusSq * 0.85f && distSq <= radiusSq)
                {
                    c = Color.Lerp(Color.white, Color.clear, (distSq - radiusSq * 0.85f) / (radiusSq * 0.15f));
                }

                tex.SetPixel(x, y, c);
            }
        }

        tex.Apply();

        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, 100f);
        sprite.name = "GeneratedCircleHandle";
        handleImage.sprite = sprite;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        OnDrag(eventData);
        ApplyActiveVisualState();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (background == null || handle == null)
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

        float radius = GetEffectiveRadius();
        Vector2 normalized = localPoint / radius;
        Vector2 clamped = Vector2.ClampMagnitude(normalized, 1f);

        float magnitude = clamped.magnitude;
        if (magnitude <= deadZone)
        {
            input = Vector2.zero;
        }
        else
        {
            float remappedMagnitude = Mathf.InverseLerp(deadZone, 1f, magnitude);
            input = clamped.normalized * remappedMagnitude;
        }

        handle.anchoredPosition = input * radius;
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

    private float GetEffectiveRadius()
    {
        if (background == null)
        {
            return Mathf.Max(1f, handleRange);
        }

        float maxRadius = Mathf.Min(background.rect.width, background.rect.height) * 0.5f;
        if (handle != null)
        {
            maxRadius -= Mathf.Min(handle.rect.width, handle.rect.height) * 0.5f;
        }

        if (handleRange > 0f)
        {
            maxRadius = Mathf.Min(maxRadius, handleRange);
        }

        return Mathf.Max(1f, maxRadius);
    }
}
