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
    public OrbitCamera orbitCamera;
    public bool _isLocal;

    private string _sessionId;
    private PlayerState _state;
    private Vector3 _targetPos;

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

        Color color;
        if (ColorUtility.TryParseHtmlString(state.color, out color))
            bodyRenderer.material.color = color;

        // Initialize labels immediately
        UpdateLabels();

        if (orbitCamera != null)
            orbitCamera.Setup(transform, isLocal);

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
            HandleInput();

        // Update transform and visuals based on the latest synchronized state
        UpdateTransform();
        UpdateVisualState();
        UpdateLabels();
        BillboardLabels();
    }

    /// <summary>
    /// Updates position and scale smoothly using interpolation
    /// based on the synchronized server state.
    /// </summary>
    void UpdateTransform()
    {
        // Interpolate position for smoother movement
        _targetPos = new Vector3(_state.x, _state.y, _state.z);

        transform.position = Vector3.Lerp(
            transform.position,
            _targetPos,
            Time.deltaTime * 15f);

        // Scale player based on score (normalized growth)
        const float MAX_SCORE = 500000f;
        const float MIN_SCALE = 1f;
        const float MAX_SCALE = 10f;

        float t = Mathf.Clamp01((float)_state.score / MAX_SCORE);
        float targetScale = Mathf.Lerp(MIN_SCALE, MAX_SCALE, t);

        transform.localScale = Vector3.Lerp(
            transform.localScale,
            Vector3.one * targetScale,
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
        // Invincibility effect is shown when the player is invincible.
        if (invincibilityEffect != null)
            invincibilityEffect.SetActive(_state.isInvincible);

        // Hide dead remote players.
        // For the local player, we keep the GameObject active so we can
        // show the death screen without destroying the player object.
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