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
    [Header("Shadow")]
    [Tooltip("Child that follows blob size on X/Z; leave empty to auto-find \"shadow\".")]
    [SerializeField] Transform shadowTransform;
    [Tooltip("World X/Z size = blob horizontal radius scale (ignores Y breathing).")]
    [SerializeField] float shadowWorldXZMultiplier = 1f;
    [Tooltip("World Y size = blob horizontal scale (set small for a flat decal).")]
    [SerializeField] float shadowWorldYMultiplier = 1f;
    [Header("Speed boost / slow VFX")]
    [Tooltip("Assign VFX_Speedlines prefab. Shown while server reports speedBoostActive (opposite to facing).")]
    public GameObject speedLinesVfxPrefab;
    public GameObject speedLinesRedVfxPrefab;
    [SerializeField] float speedLinesToBlobScaleRatio = 0.3f;
    public OrbitCamera orbitCamera;
    public bool _isLocal;

    private string _sessionId;
    private PlayerState _state;
    private Vector3 _targetPos;
    private GameObject _speedLinesInstance;
    private GameObject _speedLinesRedInstance;
    private Vector3 _lastMoveDirXZ = Vector3.forward;
    private Vector3 _lastStatePosXZ;
    private Vector3 _remoteFacingXZ = Vector3.forward;

    [Header("Skin")]
    public SkinDatabase skinDatabase;

    private string _currentSkinId = "";
    private int _lastKills;
    private int _lastTotalScore;
    private Vector3 _shadowBaseLocalScale = Vector3.one;
    /// <summary>Visual-only split mass (mesh + labels). Never duplicate the player root — it includes Camera and PlayerController.</summary>
    private GameObject _splitCloneRoot;
    private MeshRenderer _splitCloneMeshRenderer;
    private Text _cloneNameLabel;
    private Text _cloneScoreLabel;
    private Transform _splitCloneShadowTransform;
    private Vector3 _splitCloneShadowBaseLocalScale = Vector3.one;

    /// <summary>Combined mass score (leaderboard / leader checks).</summary>
    public static int GetTotalDisplayedScore(PlayerState s)
    {
        if (s == null) return 0;
        return s.score + (s.hasSplit ? s.splitScore : 0);
    }

    /// <summary>Split mass root for crowns etc. Built when hasSplit; null otherwise.</summary>
    public Transform SplitCloneRoot => _splitCloneRoot != null ? _splitCloneRoot.transform : null;

    void Awake()
    {
        if (shadowTransform == null)
        {
            var t = transform.Find("shadow");
            if (t != null) shadowTransform = t;
        }
        if (shadowTransform != null)
            _shadowBaseLocalScale = shadowTransform.localScale;
    }

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
        SetupSpeedLinesRedVfxInstance();

        _lastKills = state.kills;
        _lastTotalScore = GetTotalDisplayedScore(state);

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
            DetectEatPlayerSfx();
            // CheckLocalDeath();
        }

        // Update transform and visuals based on the latest synchronized state
        UpdateTransform();
        UpdateSplitCloneVisual();
        UpdateRemoteFacingFromState();
        UpdateVisualState();
        UpdateLabels();
        BillboardLabels();
    }

    void OnDestroy()
    {
        if (_splitCloneRoot != null)
            Destroy(_splitCloneRoot);
    }

    /// <summary>
    /// Kills from eating a player increase score and kills; gift kills add kills without score.
    /// </summary>
    void DetectEatPlayerSfx()
    {
        int total = GetTotalDisplayedScore(_state);
        if (_state.kills > _lastKills && total > _lastTotalScore)
            AudioManager.Instance?.PlayEatPlayer();
        _lastKills = _state.kills;
        _lastTotalScore = total;
    }

    void LateUpdate()
    {
        UpdateInvincibilityShieldScale();
        UpdateSpeedLinesVfx();
        UpdateShadowScale();
        UpdateSplitCloneShadowScale();
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

    /// <summary>
    /// Blob scale is (t, t�breathe, t). Drive shadow world X/Z from t only; cancel breathe on Y so the shadow does not pulse.
    /// </summary>
    void UpdateShadowScale()
    {
        if (shadowTransform == null) return;

        Vector3 s = transform.localScale;
        float sx = s.x;
        float sz = s.z;
        if (sx <= 1e-4f || sz <= 1e-4f) return;

        float horiz = Mathf.Max(sx, sz);
        float worldXZ = shadowWorldXZMultiplier * horiz;
        float worldY = shadowWorldYMultiplier * horiz;

        var comp = new Vector3(
            worldXZ / sx,
            worldY / s.y,
            worldXZ / sz);
        shadowTransform.localScale = new Vector3(
            _shadowBaseLocalScale.x * comp.x,
            _shadowBaseLocalScale.y * comp.y,
            _shadowBaseLocalScale.z * comp.z);
    }

    void UpdateSplitCloneShadowScale()
    {
        if (_splitCloneShadowTransform == null || _splitCloneRoot == null) return;

        Vector3 s = _splitCloneRoot.transform.localScale;
        float sx = s.x;
        float sz = s.z;
        if (sx <= 1e-4f || sz <= 1e-4f) return;

        float horiz = Mathf.Max(sx, sz);
        float worldXZ = shadowWorldXZMultiplier * horiz;
        float worldY = shadowWorldYMultiplier * horiz;

        var comp = new Vector3(
            worldXZ / sx,
            worldY / s.y,
            worldXZ / sz);
        _splitCloneShadowTransform.localScale = new Vector3(
            _splitCloneShadowBaseLocalScale.x * comp.x,
            _splitCloneShadowBaseLocalScale.y * comp.y,
            _splitCloneShadowBaseLocalScale.z * comp.z);
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

    void SetupSpeedLinesRedVfxInstance()
    {
        if (speedLinesRedVfxPrefab == null) return;

        if (_speedLinesRedInstance == null || _speedLinesRedInstance.transform.parent != transform)
        {
            _speedLinesRedInstance = Instantiate(speedLinesRedVfxPrefab, transform);
            _speedLinesRedInstance.name = "VFX_SpeedlinesRed";
        }

        _speedLinesRedInstance.transform.localPosition = Vector3.zero;
        _speedLinesRedInstance.SetActive(false);
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
        if (_state == null) return;

        Vector3 face = GetFacingDirXZ();
        float yawDeg = Mathf.Atan2(face.x, face.z) * Mathf.Rad2Deg;

        UpdateOneSpeedLineVfx(
            _speedLinesInstance,
            _state.speedBoostActive,
            yawDeg + 180f);
        UpdateOneSpeedLineVfx(
            _speedLinesRedInstance,
            _state.speedSlowActive,
            yawDeg);
    }

    void UpdateOneSpeedLineVfx(GameObject instance, bool show, float yawYDegrees)
    {
        if (instance == null) return;
        if (instance.activeSelf != show)
            instance.SetActive(show);
        if (!show) return;

        Vector3 s = transform.localScale;
        float blobScale = Mathf.Max(s.x, s.z);
        float worldVfxUniform = blobScale * speedLinesToBlobScaleRatio;
        instance.transform.localScale = new Vector3(
            worldVfxUniform / s.x,
            worldVfxUniform / s.y,
            worldVfxUniform / s.z);
        instance.transform.localRotation = Quaternion.Euler(0f, yawYDegrees, 0f);
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

        float targetScale = GetTargetScaleForScore(_state.score);
        float breathe = 1f + Mathf.Sin(Time.time * 2f) * 0.1f;

        transform.localScale = Vector3.Lerp(
            transform.localScale,
            new Vector3(targetScale, targetScale * breathe, targetScale),
            Time.deltaTime * 5f);
    }

    /// <summary>Visual radius scale from mass score (matches server MAX_SCORE_FOR_SCALE curve).</summary>
    static float GetTargetScaleForScore(int score)
    {
        const float MAX_SCORE = 200000f;
        const float MIN_SCALE = 1f;
        const float MAX_SCALE = 10f;
        float t = Mathf.Clamp01((float)score / MAX_SCORE);
        return Mathf.Lerp(MIN_SCALE, MAX_SCALE, t);
    }

    /// <summary>Builds a minimal clone: mesh only + duplicated world Canvas (no camera, no controls). Server moves both masses.</summary>
    void EnsureSplitCloneBuilt()
    {
        if (_splitCloneRoot != null || bodyRenderer == null) return;

        _splitCloneRoot = new GameObject("SplitClone");
        _splitCloneRoot.transform.SetParent(transform.parent);

        var meshGo = new GameObject("BlobMesh");
        meshGo.transform.SetParent(_splitCloneRoot.transform, false);
        var mf = meshGo.AddComponent<MeshFilter>();
        _splitCloneMeshRenderer = meshGo.AddComponent<MeshRenderer>();
        var srcMf = bodyRenderer.GetComponent<MeshFilter>();
        if (srcMf != null)
            mf.sharedMesh = srcMf.sharedMesh;
        _splitCloneMeshRenderer.sharedMaterial = bodyRenderer.sharedMaterial;

        if (shadowTransform != null)
        {
            var sh = Instantiate(shadowTransform.gameObject, _splitCloneRoot.transform);
            sh.name = "shadow";
            _splitCloneShadowTransform = sh.transform;
            _splitCloneShadowBaseLocalScale = _splitCloneShadowTransform.localScale;
        }

        var srcCanvas = transform.Find("Canvas");
        if (srcCanvas != null)
        {
            var canvasGo = Instantiate(srcCanvas.gameObject, _splitCloneRoot.transform);
            canvasGo.name = "Canvas";
            var srcRt = srcCanvas.GetComponent<RectTransform>();
            var dstRt = canvasGo.GetComponent<RectTransform>();
            if (srcRt != null && dstRt != null)
            {
                dstRt.localPosition = srcRt.localPosition;
                dstRt.localRotation = srcRt.localRotation;
                dstRt.localScale = srcRt.localScale;
            }

            var canvas = canvasGo.GetComponent<Canvas>();
            if (canvas != null && Camera.main != null)
                canvas.worldCamera = Camera.main;

            var raycaster = canvasGo.GetComponent<GraphicRaycaster>();
            if (raycaster != null)
                raycaster.enabled = false;

            var texts = canvasGo.GetComponentsInChildren<Text>(true);
            if (texts.Length > 0) _cloneNameLabel = texts[0];
            if (texts.Length > 1) _cloneScoreLabel = texts[1];
        }
    }

    void UpdateSplitCloneVisual()
    {
        if (_state == null || bodyRenderer == null) return;

        if (_state.hasSplit)
        {
            EnsureSplitCloneBuilt();
            if (_splitCloneRoot == null) return;

            // Match primary blob: same position/scale follow smoothing (avoids copy feeling mushy vs. snappy).
            const float posLerp = 15f;
            const float scaleLerp = 12f;
            Vector3 cloneTargetPos = new Vector3(_state.splitX, _state.y, _state.splitZ);
            _splitCloneRoot.transform.position = Vector3.Lerp(
                _splitCloneRoot.transform.position,
                cloneTargetPos,
                Time.deltaTime * posLerp);
            float targetScale = GetTargetScaleForScore(_state.splitScore);
            float breathe = 1f + Mathf.Sin(Time.time * 2f) * 0.1f;
            _splitCloneRoot.transform.localScale = Vector3.Lerp(
                _splitCloneRoot.transform.localScale,
                new Vector3(targetScale, targetScale * breathe, targetScale),
                Time.deltaTime * scaleLerp);
        }
        else if (_splitCloneRoot != null)
        {
            Destroy(_splitCloneRoot);
            _splitCloneRoot = null;
            _splitCloneMeshRenderer = null;
            _cloneNameLabel = null;
            _cloneScoreLabel = null;
            _splitCloneShadowTransform = null;
        }
    }

    Vector3 GetSplitLaunchDirection()
    {
        if (_lastMoveDirXZ.sqrMagnitude > 1e-6f)
            return _lastMoveDirXZ;
        if (Camera.main != null)
        {
            var f = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized;
            if (f.sqrMagnitude > 1e-6f)
                return f;
        }
        return Vector3.forward;
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

        if (_cloneNameLabel != null)
            _cloneNameLabel.text = _state.name;
        if (_cloneScoreLabel != null && _state.hasSplit)
            _cloneScoreLabel.text = FormatNumber(_state.splitScore);
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

        if (_cloneNameLabel != null)
            _cloneNameLabel.transform.LookAt(
                _cloneNameLabel.transform.position + cam.rotation * Vector3.forward,
                cam.rotation * Vector3.up);
        if (_cloneScoreLabel != null)
            _cloneScoreLabel.transform.LookAt(
                _cloneScoreLabel.transform.position + cam.rotation * Vector3.forward,
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
                {
                    bodyRenderer.material = mat;
                    if (_splitCloneMeshRenderer != null)
                        _splitCloneMeshRenderer.material = mat;
                }
            }
        }

        if (invincibilityEffect != null)
            invincibilityEffect.SetActive(_state.isInvincible);

        if (!_isLocal)
            gameObject.SetActive(_state.isAlive);

        if (_splitCloneRoot != null)
            _splitCloneRoot.SetActive(_state.isAlive && _state.hasSplit);
    }

    /// <summary>
    /// Handles movement input for the local player and sends
    /// movement data to the server.
    /// </summary>
    void HandleInput()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        // Relative direction based on camera orientation
        var cam = Camera.main != null ? Camera.main.transform : transform;
        var forward = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
        var right = Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized;

        var dir = forward * v + right * h;
        if (dir.sqrMagnitude > 1e-6f)
            _lastMoveDirXZ = new Vector3(dir.x, 0f, dir.z).normalized;

        if (Input.GetKeyDown(KeyCode.E) && !_state.hasSplit && NetworkManager.Instance != null)
        {
            Vector3 launch = GetSplitLaunchDirection();
            NetworkManager.Instance.SendSplit(launch.x, launch.z);
        }

        if (Mathf.Abs(h) < 0.01f && Mathf.Abs(v) < 0.01f)
            return;

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