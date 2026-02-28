using UnityEngine;

/// <summary>
///  View for a blob pickup item. It scales and colors itself based on the value of the pickup.
/// </summary>
public class BlobPickupView : MonoBehaviour
{
    public void Init(BlobPickup data)
    {
        float s = data.isGolden ? 0.4f : 0.1f + data.value * 0.06f;
        transform.localScale = Vector3.one * s;

        var rend = GetComponentInChildren<MeshRenderer>();
        if (rend == null) return;

        if (data.isGolden)
        {
            rend.material.color = new Color(1f, 0.84f, 0f); // Golden color
            return;
        }

        rend.material.color = data.value switch
        {
            1 => Color.white,
            2 => Color.cyan,
            5 => Color.yellow,
            10 => Color.magenta,
            _ => Color.green
        };
    }
}