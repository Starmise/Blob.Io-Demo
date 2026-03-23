using UnityEngine;

/// <summary>
///  View for a blob pickup item. It scales and colors itself based on the value of the pickup.
/// </summary>
public class BlobPickupView : MonoBehaviour
{
    [Header("Special Item")]
    public Sprite specialSprite;
    public SpriteRenderer spriteRenderer;

    public void Init(BlobPickup data)
    {
        if (data.isSpecial)
        {
            // Flat sprite on the ground, and reduce size
            float s = 0.3f;
            transform.localScale = Vector3.one * s;

            var rend = GetComponentInChildren<MeshRenderer>();
            if (rend != null) rend.enabled = false; // Hide sphere

            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = true;
                if (specialSprite != null) spriteRenderer.sprite = specialSprite;
            }
            return;
        }

        // Hide sprite renderer for normal blobs
        if (spriteRenderer != null) spriteRenderer.enabled = false;

        float size = 0.1f + data.value * 0.06f;
        transform.localScale = Vector3.one * size;

        var meshRend = GetComponentInChildren<MeshRenderer>();
        if (meshRend == null) return;

        meshRend.material.color = data.value switch
        {
            1 => Color.white,
            2 => Color.cyan,
            5 => Color.red,
            10 => Color.magenta,
            _ => Color.green
        };
    }
}