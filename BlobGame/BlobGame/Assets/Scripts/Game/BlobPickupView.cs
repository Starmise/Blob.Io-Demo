using UnityEngine;

/// <summary>
/// View for a blob pickup: scale from value, tint from server hex (normal blobs only).
/// Special and speed items use sprites (white tint), billboard to camera and animate.
/// </summary>
public class BlobPickupView : MonoBehaviour
{
    [Header("Sprite Pickups")]
    public Sprite specialSprite;
    public Sprite speedBoostSprite;
    public SpriteRenderer spriteRenderer;
    [Header("Animation")]
    public float pulseAmount = 0.08f;
    public float pulseSpeed = 4f;
    public float floatAmount = 0.08f;
    public float floatSpeed = 2.2f;

    bool _isSpritePickup;
    Vector3 _baseScale;
    Vector3 _basePos;

    [Header("Slow debuff (same sprite as boost, tinted)")]
    [SerializeField] Color slowSpriteTint = new Color(1f, 0.55f, 0.55f, 1f);

    public void Init(BlobPickup data)
    {
        _isSpritePickup = data.isSpecial || data.isSpeedBoost || data.isSpeedSlow;

        if (_isSpritePickup)
        {
            // Flat sprite near the ground, then animate slightly for readability.
            float s = 0.3f;
            transform.localScale = Vector3.one * s;
            _baseScale = transform.localScale;
            _basePos = transform.position;

            var rend = GetComponentInChildren<MeshRenderer>();
            if (rend != null) rend.enabled = false; // Hide sphere

            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = true;
                if (data.isSpecial && specialSprite != null) spriteRenderer.sprite = specialSprite;
                else if (data.isSpeedBoost && speedBoostSprite != null) spriteRenderer.sprite = speedBoostSprite;
                else if (data.isSpeedSlow && speedBoostSprite != null) spriteRenderer.sprite = speedBoostSprite;
                if (data.isSpeedSlow)
                    spriteRenderer.color = slowSpriteTint;
                else
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

    void Update()
    {
        if (!_isSpritePickup) return;

        // Billboard toward camera so the pickup reads from all angles.
        if (Camera.main != null)
        {
            var cam = Camera.main.transform;
            transform.LookAt(transform.position + cam.forward, cam.up);
        }

        // Subtle breathing + floating loop.
        float t = Time.time;
        float pulse = 1f + Mathf.Sin(t * pulseSpeed) * pulseAmount;
        transform.localScale = _baseScale * pulse;
        transform.position = _basePos + Vector3.up * (Mathf.Sin(t * floatSpeed) * floatAmount);
    }

    static bool TryParseBlobColor(string hex, out Color color)
    {
        color = default;
        if (string.IsNullOrEmpty(hex)) return false;
        return ColorUtility.TryParseHtmlString(hex, out color);
    }
}