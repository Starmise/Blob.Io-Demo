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
    public GameObject invincibilityEffect;
    public bool _isLocal;

    private string _sessionId;
    private PlayerState _state;
    private Vector3 _targetPos;

    // Initializes the player controller with the given session ID, player state, and whether this is the local player.
    public void Init(string sessionId, PlayerState state, bool isLocal)
    {
        _sessionId = sessionId;
        _state = state;
        _isLocal = isLocal;
        _targetPos = new Vector3(state.x, state.y, state.z);

        Color color;
        if (ColorUtility.TryParseHtmlString(state.color, out color))
            bodyRenderer.material.color = color;

        /* I learned that in Colyseus 0.17, instead of using OnChange callbacks for state changes, we can just read
         the updated state directly in the Update method. The state object is automatically updated with the latest 
         values from the server, so we can just access _state.x, _state.y, etc. in Update and it will have the current values. 
         This simplifies the code a lot since we don't need to set up callbacks for every property we want to track. */
    }

    /// <summary>
    /// This method is called whenever the player state changes. 
    /// We can update the target position and other visual elements based on the new state.
    /// </summary>
    void OnStateChanged()
    {
        _targetPos = new Vector3(_state.x, _state.y, _state.z);

        // Name label shows the player's name and score. The score is formatted to be more readable (e.g. 1.2K instead of 1200).
        if (nameLabel != null)
            nameLabel.text = $"{_state.name}\n{FormatNumber(_state.score)}";

        // Invincibility effect is shown when the player is invincible.
        if (invincibilityEffect != null)
            invincibilityEffect.SetActive(_state.isInvincible);

        // Hide dead remote players. For the local player, we keep the game object active even when dead, 
        // so we can show the death screen and allow the player to respawn without needing to destroy and re-instantiate the player object.
        if (!_isLocal)
            gameObject.SetActive(_state.isAlive);
    }

    void Update()
    {
        if (_state == null) return;
        if (_isLocal) HandleInput();

        // Read updated position from the state and interpolate the player's position and scale for a smoother visual effect.
        _targetPos = new Vector3(_state.x, _state.y, _state.z);

        transform.position = Vector3.Lerp(
            transform.position, _targetPos, Time.deltaTime * 15f);

        float s = _state.size * 0.5f;
        transform.localScale = Vector3.Lerp(
            transform.localScale,
            new Vector3(s, s * 0.6f, s),
            Time.deltaTime * 10f);

        if (nameLabel != null)
            nameLabel.text = $"{_state.name}\n{FormatNumber(_state.score)}";

        if (invincibilityEffect != null)
            invincibilityEffect.SetActive(_state.isInvincible);

        if (!_isLocal)
            gameObject.SetActive(_state.isAlive);
    }

    void HandleInput()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        if (Mathf.Abs(h) < 0.01f && Mathf.Abs(v) < 0.01f) return;

        // Relative direction based on camera orientation.
        var cam = Camera.main.transform;
        var forward = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
        var right = Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized;
        var dir = forward * v + right * h;

        NetworkManager.Instance.SendMove(dir.x, dir.z);

        // Local movement for client-side prediction.
        float speed = 8f / (1 + (_state != null ? _state.size * 0.15f : 0f));
        transform.position += new Vector3(dir.x, 0, dir.z) * speed * Time.deltaTime;
    }

    public static string FormatNumber(int n)
    {
        // Format the number to be more readable. For example, 1.2K instead of 1200, 3.4M instead of 3400000, etc.
        if (n >= 1_000_000) return $"{n / 1_000_000f:F1}M";
        else if (n >= 1_000) return $"{n / 1000f:F1}K";
        else return n.ToString();
    }
}