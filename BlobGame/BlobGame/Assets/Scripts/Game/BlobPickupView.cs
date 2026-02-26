using UnityEngine;

/// <summary>
///  View for a blob pickup item. It scales and colors itself based on the value of the pickup.
/// </summary>
public class BlobPickupView : MonoBehaviour
{
    public void Init(BlobPickup data)
    {
        float s = 0.1f + data.value * 0.06f;
        transform.localScale = Vector3.one * s;

        var rend = GetComponentInChildren<MeshRenderer>();
        if (rend == null) return;

        // Set the color based on the value of the pickup. This is just an example, I can choose any colors I like.
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