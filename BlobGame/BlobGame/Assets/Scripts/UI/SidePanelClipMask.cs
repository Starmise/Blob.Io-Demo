using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Adds a <see cref="RectMask2D"/> so children stay inside the panel. Cell size and column
/// count are configured only on the <see cref="GridLayoutGroup"/> in the scene (fixed pixels).
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SidePanelClipMask : MonoBehaviour
{
    void Awake()
    {
        if (GetComponent<RectMask2D>() == null)
            gameObject.AddComponent<RectMask2D>();
    }
}
