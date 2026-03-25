using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Plays UI click on pointer down (before <see cref="UnityEngine.UI.Button.onClick"/>), so mute toggles
/// cannot zero the SFX bus before the click sound is triggered.
/// </summary>
[DisallowMultipleComponent]
public class UiButtonPointerDownSound : MonoBehaviour, IPointerDownHandler
{
    public void OnPointerDown(PointerEventData eventData)
    {
        AudioManager.Instance?.PlayUiClick();
    }
}
