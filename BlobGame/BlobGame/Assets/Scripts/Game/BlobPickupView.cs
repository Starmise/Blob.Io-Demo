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

        Vector3 pos = transform.position;
        pos.y = s * 0.5f;
        transform.position = pos;

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