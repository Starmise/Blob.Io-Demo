using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Drag on the pad to move <see cref="JoystickNub"/>; feeds <see cref="MobileMoveInput"/> like Horizontal/Vertical axes.
/// </summary>
[DisallowMultipleComponent]
public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [SerializeField] RectTransform nub;
    [Tooltip("1 = nub can reach pad edge.")]
    [SerializeField] float handleRange = 1f;

    RectTransform _padRect;
    int _pointerId = -99999;

    void Awake()
    {
        _padRect = transform as RectTransform;
        if (nub == null)
        {
            var t = transform.Find("JoystickNub");
            if (t != null) nub = t as RectTransform;
        }
        if (nub != null)
        {
            var g = nub.GetComponent<Graphic>();
            if (g != null) g.raycastTarget = false;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _pointerId = eventData.pointerId;
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.pointerId != _pointerId || nub == null || _padRect == null) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _padRect, eventData.position, eventData.pressEventCamera, out var localPoint))
            return;

        float radius = Mathf.Min(_padRect.rect.width, _padRect.rect.height) * 0.5f;
        if (radius < 4f) radius = 50f;

        Vector2 clamped = Vector2.ClampMagnitude(localPoint, radius * handleRange);
        nub.anchoredPosition = clamped;
        MobileMoveInput.SetAxis(new Vector2(clamped.x / radius, clamped.y / radius));
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != _pointerId) return;
        _pointerId = -99999;
        if (nub != null) nub.anchoredPosition = Vector2.zero;
        MobileMoveInput.Clear();
    }

    void OnDisable()
    {
        MobileMoveInput.Clear();
        if (nub != null) nub.anchoredPosition = Vector2.zero;
    }
}
