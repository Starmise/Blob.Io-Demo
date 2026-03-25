using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Add to each root Canvas that has buttons (Lobby screen canvas, Game HUD canvas, etc.).
/// Adds <see cref="UiButtonPointerDownSound"/> to every <see cref="Button"/> so clicks play on pointer down,
/// before <c>onClick</c> (fixes unreliable sound when the same click toggles mute).
/// </summary>
[DisallowMultipleComponent]
public class UiButtonSoundBinder : MonoBehaviour
{
    void Awake()
    {
        var buttons = GetComponentsInChildren<Button>(true);
        foreach (var b in buttons)
        {
            if (b.GetComponent<UiButtonPointerDownSound>() != null)
                continue;
            b.gameObject.AddComponent<UiButtonPointerDownSound>();
        }
    }
}
