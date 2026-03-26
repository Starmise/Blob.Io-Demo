using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lobby-only visual preview for the player.
/// Shows shadow + name + current skin, without any movement/network logic.
/// </summary>
[DisallowMultipleComponent]
public class PlayerLobby : MonoBehaviour
{
    [Header("Optional overrides (auto-filled from PlayerController if empty)")]
    public MeshRenderer bodyRenderer;
    public Text nameLabel;
    public GameObject invincibilityEffect;
    public Transform shadowTransform;
    public SkinDatabase skinDatabase;

    static readonly int FaceDirOSId = Shader.PropertyToID("_FaceDirOS");

    PlayerController _pc;
    Vector3 _baseLocalScale = Vector3.one;

    [Header("Breathing (visual only)")]
    [Tooltip("Breathing speed; matches in-game Time.time * 2f.")]
    public float breathSpeed = 2f;
    [Tooltip("Breathing amplitude; matches in-game 0.1f.")]
    public float breathAmplitude = 0.1f;

    void Awake()
    {
        _pc = GetComponent<PlayerController>();
        _baseLocalScale = transform.localScale;

        // Auto-fill from PlayerController to avoid manual scene wiring.
        if (_pc != null)
        {
            if (bodyRenderer == null) bodyRenderer = _pc.bodyRenderer;
            if (nameLabel == null) nameLabel = _pc.nameLabel;
            if (invincibilityEffect == null) invincibilityEffect = _pc.invincibilityEffect;
            if (shadowTransform == null) shadowTransform = _pc.shadowTransform;
            if (skinDatabase == null) skinDatabase = _pc.skinDatabase;
        }
    }

    void Update()
    {
        // Visual-only breathing: X/Z stay fixed, Y breathes like the in-game blob.
        float breathe = 1f + Mathf.Sin(Time.time * breathSpeed) * breathAmplitude;
        transform.localScale = new Vector3(
            _baseLocalScale.x,
            _baseLocalScale.y * breathe,
            _baseLocalScale.z
        );
    }

    public void SetupLobbyPreview()
    {
        // Defensive checks.
        if (NetworkManager.Instance == null) return;

        // Name: must match the value used when joining (LobbyManager precomputes LocalPlayerName).
        if (nameLabel != null)
        {
            string n = NetworkManager.Instance.LocalPlayerName;
            if (string.IsNullOrEmpty(n)) n = "Player";
            nameLabel.text = n;
        }

        // Apply selected skin material.
        if (bodyRenderer != null && skinDatabase != null)
        {
            string skinId = NetworkManager.Instance.LocalSkinId;
            if (string.IsNullOrEmpty(skinId)) skinId = PlayerPrefs.GetString("SelectedSkin", "default");

            var mat = skinDatabase.GetMaterial(skinId);
            if (mat != null)
                bodyRenderer.material = mat;
        }

        // Hide invincibility shield in lobby preview.
        if (invincibilityEffect != null)
            invincibilityEffect.SetActive(false);

        // Ensure shadow is visible if it exists.
        if (shadowTransform != null)
            shadowTransform.gameObject.SetActive(true);

        // Face skins need a direction vector; pick a stable default.
        if (bodyRenderer != null)
        {
            var mpb = new MaterialPropertyBlock();
            mpb.SetVector(FaceDirOSId, new Vector4(0f, 0f, 1f, 0f));
            bodyRenderer.SetPropertyBlock(mpb);
        }
    }
}

