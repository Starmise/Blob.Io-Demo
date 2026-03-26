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

    [Header("Special item value label")]
    [SerializeField] float specialLabelLocalY = 1.35f;
    [SerializeField] float specialLabelCharacterSize = 0.12f;
    [SerializeField] int specialLabelFontSize = 48;
    [SerializeField] float specialLabelOutlineOffset = 0.015f;

    GameObject _specialValueLabelRoot;
    static Font s_labelFont;

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

            if (data.isSpecial)
                SetupSpecialValueLabel(Mathf.RoundToInt(data.value));
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

    void SetupSpecialValueLabel(int points)
    {
        if (_specialValueLabelRoot != null)
            Destroy(_specialValueLabelRoot);

        if (s_labelFont == null)
            s_labelFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        _specialValueLabelRoot = new GameObject("SpecialValueBillboard");
        _specialValueLabelRoot.transform.SetParent(transform, false);
        _specialValueLabelRoot.transform.localPosition = new Vector3(0f, specialLabelLocalY, 0f);
        _specialValueLabelRoot.transform.localRotation = Quaternion.identity;

        // World-space uGUI Canvas often fails to show on small scaled pickups; TextMesh renders as geometry.
        string text = points.ToString();
        float o = specialLabelOutlineOffset;

        void AddLayer(string name, Color color, Vector3 localOffset, int sortOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_specialValueLabelRoot.transform, false);
            go.transform.localPosition = localOffset;
            go.transform.localRotation = Quaternion.identity;
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.font = s_labelFont;
            tm.fontSize = specialLabelFontSize;
            tm.fontStyle = FontStyle.Bold;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.characterSize = specialLabelCharacterSize;
            tm.color = color;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sortingOrder = sortOrder;
        }

        // Black outline (cardinal + diagonals, slightly in front for z-fight)
        Vector3 zBump = new Vector3(0f, 0f, 0.0005f);
        AddLayer("Outline", Color.black, new Vector3(o, 0f, 0f) + zBump, 0);
        AddLayer("Outline", Color.black, new Vector3(-o, 0f, 0f) + zBump, 0);
        AddLayer("Outline", Color.black, new Vector3(0f, o, 0f) + zBump, 0);
        AddLayer("Outline", Color.black, new Vector3(0f, -o, 0f) + zBump, 0);
        AddLayer("Outline", Color.black, new Vector3(o, o, 0f) + zBump, 0);
        AddLayer("Outline", Color.black, new Vector3(-o, o, 0f) + zBump, 0);
        AddLayer("Outline", Color.black, new Vector3(o, -o, 0f) + zBump, 0);
        AddLayer("Outline", Color.black, new Vector3(-o, -o, 0f) + zBump, 0);

        AddLayer("Fill", Color.white, Vector3.zero, 1);
    }

    static bool TryParseBlobColor(string hex, out Color color)
    {
        color = default;
        if (string.IsNullOrEmpty(hex)) return false;
        return ColorUtility.TryParseHtmlString(hex, out color);
    }
}