using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls the player character in the scene. It updates the player's position, scale, name label and invincibility 
/// effect based on the player state received from the server. It also handles player input for movement,
/// and applies client-side prediction for smoother movement. The player controller can be initialized with the 
/// player state and whether it's the local player or a remote player.
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Header("References")]
    public MeshRenderer bodyRenderer;
    public Text nameLabel;
    public Text scoreLabel;
    public GameObject invincibilityEffect;
    [Tooltip("Shield visual diameter vs player horizontal scale (max of X/Z).")]
    [SerializeField] float invincibilityShieldScaleFactor = 1.3f;
    [Header("Speed boost VFX")]
    [Tooltip("Assign VFX_Speedlines prefab. Shown while server reports speedBoostActive.")]
    public GameObject speedLinesVfxPrefab;
    [Tooltip("Uniform world scale: VFX size = blob horizontal scale � this. Example: blob 4 ? VFX 0.8 when 0.2.")]
    [SerializeField] float speedLinesToBlobScaleRatio = 0.3f;
    public OrbitCamera orbitCamera;
    public bool _isLocal;

    private string _sessionId;
    private PlayerState _state;
    private Vector3 _targetPos;
    private GameObject _speedLinesInstance;
    private Vector3 _lastMoveDirXZ = Vector3.forward;
    private Vector3 _lastStatePosXZ;
    private Vector3 _remoteFacingXZ = Vector3.forward;

    [Header("Skin")]
    public SkinDatabase skinDatabase;

    private string _currentSkinId = "";

    /// <summary>
    /// Initializes the player controller with the given session ID, player state,
    /// and whether this is the local player.
    /// </summary>
    public void Init(string sessionId, PlayerState state, bool isLocal)
    {
        _sessionId = sessionId;
        _state = state;
        _isLocal = isLocal;
        _targetPos = new Vector3(state.x, state.y, state.z);
        _lastStatePosXZ = new Vector3(state.x, 0f, state.z);
        // _deathHandled = false;

        // For fallback
        Color color;
        if (ColorUtility.TryParseHtmlString(state.color, out color))
            bodyRenderer.material.color = color;

        // Initialize labels immediately
        UpdateLabels();

        if (orbitCamera != null)
            orbitCamera.Setup(transform, isLocal);

        SetupInvincibilityShieldInstance();
        SetupSpeedLinesVfxInstance();

        /* I learned that in Colyseus 0.17, instead of using OnChange callbacks for state changes, we can just read
         the updated state directly in the Update method. The state object is automatically updated with the latest 
         values from the server, so we can just access _state.x, _state.y, etc. in Update and it will have the current values. 
         This simplifies the code a lot since we don't need to set up callbacks for every property we want to track. */
    }

    void Update()
    {
        if (_state == null) return;

        // Handle local input only for the local player
        if (_isLocal)
        {
            HandleInput();
            // CheckLocalDeath();
        }

        // Update transform and visuals based on the latest synchronized state
        UpdateTransform();
        UpdateRemoteFacingFromState();
        UpdateVisualState();
        UpdateLabels();
        BillboardLabels();
    }

    void LateUpdate()
    {
        UpdateInvincibilityShieldScale();
        UpdateSpeedLinesVfx();
    }

    /// <summary>
    /// If the inspector references the shield prefab asset (not a child), instantiate it under the player.
    /// Keeps Z = 90 degrees and centers on the blob so it tracks growth.
    /// </summary>
    void SetupInvincibilityShieldInstance()
    {
        if (invincibilityEffect == null) return;

        if (invincibilityEffect.transform.parent != transform)
        {
            invincibilityEffect = Instantiate(invincibilityEffect, transform);
            invincibilityEffect.name = "InvincibilityShield";
        }

        invincibilityEffect.transform.localPosition = Vector3.zero;
        invincibilityEffect.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
    }

    /// <summary>
    /// Player scale is non-uniform (breathing on Y). Set shield local scale so world scale stays uniform
    /// at <see cref="invincibilityShieldScaleFactor"/> horizontal player size.
    /// </summary>
    void UpdateInvincibilityShieldScale()
    {
        if (_state == null || invincibilityEffect == null) return;
        if (!_state.isInvincible || !invincibilityEffect.activeInHierarchy) return;

        Vector3 s = transform.localScale;
        float w = invincibilityShieldScaleFactor * Mathf.Max(s.x, s.z);
        if (w <= 1e-4f) return;

        invincibilityEffect.transform.localScale = new Vector3(w / s.x, w / s.y, w / s.z);
    }

    void SetupSpeedLinesVfxInstance()
    {
        if (speedLinesVfxPrefab == null) return;

        if (_speedLinesInstance == null || _speedLinesInstance.transform.parent != transform)
        {
            _speedLinesInstance = Instantiate(speedLinesVfxPrefab, transform);
            _speedLinesInstance.name = "VFX_Speedlines";
        }

        _speedLinesInstance.transform.localPosition = Vector3.zero;
        _speedLinesInstance.SetActive(false);
    }

    void UpdateRemoteFacingFromState()
    {
        if (_isLocal || _state == null) return;

        Vector3 cur = new Vector3(_state.x, 0f, _state.z);
        Vector3 delta = cur - _lastStatePosXZ;
        _lastStatePosXZ = cur;
        if (delta.sqrMagnitude > 1e-6f)
            _remoteFacingXZ = delta.normalized;
    }

    void UpdateSpeedLinesVfx()
    {
        if (_state == null || _speedLinesInstance == null) return;

        bool show = _state.speedBoostActive;
        if (_speedLinesInstance.activeSelf != show)
            _speedLinesInstance.SetActive(show);
        if (!show) return;

        Vector3 s = transform.localScale;
        float blobScale = Mathf.Max(s.x, s.z);
        float worldVfxUniform = blobScale * speedLinesToBlobScaleRatio;
        _speedLinesInstance.transform.localScale = new Vector3(
            worldVfxUniform / s.x,
            worldVfxUniform / s.y,
            worldVfxUniform / s.z);

        Vector3 face = GetFacingDirXZ();
        float yawDeg = Mathf.Atan2(face.x, face.z) * Mathf.Rad2Deg;
        _speedLinesInstance.transform.localRotation = Quaternion.Euler(0f, yawDeg + 180f, 0f);
    }

    Vector3 GetFacingDirXZ()
    {
        if (_isLocal)
            return _lastMoveDirXZ.sqrMagnitude > 1e-6f ? _lastMoveDirXZ : Vector3.forward;
        return _remoteFacingXZ.sqrMagnitude > 1e-6f ? _remoteFacingXZ : Vector3.forward;
    }

    /// <summary>
    /// Updates position and scale smoothly using interpolation
    /// based on the synchronized server state.
    /// </summary>
    void UpdateTransform()
    {
        _targetPos = new Vector3(_state.x, _state.y, _state.z);

        transform.position = Vector3.Lerp(
            transform.position,
            _targetPos,
            Time.deltaTime * 15f);

        // Must match server `MAX_SCORE_FOR_SCALE` in MyRoom.ts (visual growth vs score).
        const float MAX_SCORE = 200000f;
        const float MIN_SCALE = 1f;
        const float MAX_SCALE = 10f;

        float t = Mathf.Clamp01((float)_state.score / MAX_SCORE);
        float targetScale = Mathf.Lerp(MIN_SCALE, MAX_SCALE, t);

        // Breathing animation subtle Y squish using a sine wave
        float breathe = 1f + Mathf.Sin(Time.time * 2f) * 0.1f;

        transform.localScale = Vector3.Lerp(
            transform.localScale,
            new Vector3(targetScale, targetScale * breathe, targetScale),
            Time.deltaTime * 5f);
    }

    /// <summary>
    /// Updates name and score labels using the latest state values.
    /// </summary>
    void UpdateLabels()
    {
        if (nameLabel != null)
            nameLabel.text = _state.name;

        if (scoreLabel != null)
            scoreLabel.text = FormatNumber(_state.score);
    }

    /// <summary>
    /// Makes both labels always face the camera (billboard effect).
    /// </summary>
    void BillboardLabels()
    {
        if (Camera.main == null) return;

        Transform cam = Camera.main.transform;

        if (nameLabel != null)
            nameLabel.transform.LookAt(
                nameLabel.transform.position + cam.rotation * Vector3.forward,
                cam.rotation * Vector3.up);

        if (scoreLabel != null)
            scoreLabel.transform.LookAt(
                scoreLabel.transform.position + cam.rotation * Vector3.forward,
                cam.rotation * Vector3.up);
    }

    /// <summary>
    /// Updates visual elements such as invincibility effect
    /// and alive state visibility.
    /// </summary>
    void UpdateVisualState()
    {
        // Update skin material using skinId when itchanges
        if (!string.IsNullOrEmpty(_state.skinId) && _state.skinId != _currentSkinId)
        {
            _currentSkinId = _state.skinId;
            if (skinDatabase != null)
            {
                var mat = skinDatabase.GetMaterial(_currentSkinId);
                if (mat != null)
                    bodyRenderer.material = mat;
            }
        }

        if (invincibilityEffect != null)
            invincibilityEffect.SetActive(_state.isInvincible);

        if (!_isLocal)
            gameObject.SetActive(_state.isAlive);
    }

    /// <summary>
    /// Handles movement input for the local player and sends
    /// movement data to the server.
    /// </summary>
    void HandleInput()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        if (Mathf.Abs(h) < 0.01f && Mathf.Abs(v) < 0.01f)
            return;

        // Relative direction based on camera orientation
        var cam = Camera.main.transform;
        var forward = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
        var right = Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized;

        var dir = forward * v + right * h;
        if (dir.sqrMagnitude > 1e-6f)
            _lastMoveDirXZ = new Vector3(dir.x, 0f, dir.z).normalized;

        // Send movement to server
        NetworkManager.Instance.SendMove(dir.x, dir.z);
    }

    /// <summary>
    /// Formats large numbers into readable form (1.2K, 3.4M, etc.).
    /// </summary>
    public static string FormatNumber(int n)
    {
        if (n >= 1_000_000) return $"{n / 1_000_000f:F1}M";
        else if (n >= 1_000) return $"{n / 1000f:F1}K";
        else return n.ToString();
    }
}