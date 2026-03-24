using UnityEngine;

/// <summary>
/// View for a blob pickup: scale from value, tint from server hex (normal blobs only).
/// Special items use <see cref="specialSprite"/> with no tint.
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
                spriteRenderer.color = Color.white;
            }
            return;
        }

        // Hide sprite renderer for normal blobs
        if (spriteRenderer != null) spriteRenderer.enabled = false;

        float size = 0.1f + data.value * 0.06f;
        transform.localScale = Vector3.one * size;

        var meshRend = GetComponentInChildren<MeshRenderer>();
        if (meshRend == null) return;

        if (TryParseBlobColor(data.color, out Color blobCol))
            meshRend.material.color = blobCol;
        else
            meshRend.material.color = Color.HSVToRGB(Random.value, 0.55f, 0.95f);
    }

    static bool TryParseBlobColor(string hex, out Color color)
    {
        color = default;
        if (string.IsNullOrEmpty(hex)) return false;
        return ColorUtility.TryParseHtmlString(hex, out color);
    }
}