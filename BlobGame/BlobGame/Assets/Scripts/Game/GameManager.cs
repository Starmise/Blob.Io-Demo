using UnityEngine;
using System.Collections.Generic;
using Colyseus.Schema;

/// <summary>
/// Manages the game state, including spawning players and blob pickups based on the state received from the server.
/// It listens to changes in the players and blobs collections in the state, and updates the scene
/// accordingly by instantiating or destroying player and blob pickup game objects.
/// It also listens for "died" messages from the server to show the death screen when the local player dies.
/// This class is a singleton, so it can be easily accessed from other scripts to get references to players and blobs if needed.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Prefabs")]
    public GameObject playerPrefab;
    public GameObject blobPickupPrefab;

    [Header("Splitter spikes")]
    [Tooltip("Optional. ~10 hazards that split into two smaller copies when a player touches them.")]
    [SerializeField] GameObject splitterSpikePrefab;
    [SerializeField] int splitterSpikeCount = 10;
    [Tooltip("Half arena extent on X/Z (matches server MAP_SIZE).")]
    [SerializeField] float splitterSpikeArenaHalfSize = 75f;
    [SerializeField] float splitterSpikeScaleMin = 1.4f;
    [SerializeField] float splitterSpikeScaleMax = 3.2f;

    [Header("Audio")]
    [Tooltip("Max horizontal distance from local player to blob pickup to play collect SFX (you collected it).")]
    [SerializeField] float pickupSoundRadius = 5f;

    private Dictionary<string, PlayerController> _players = new();
    private Dictionary<string, BlobPickupView> _blobs = new();

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        var room = NetworkManager.Instance.Room;
        var callbacks = Callbacks.Get(room);

        // --- Players ---
        callbacks.OnAdd(state => state.players, (sessionId, player) =>
        {
            SpawnPlayer(sessionId, player);
        });

        callbacks.OnRemove(state => state.players, (sessionId, player) =>
        {
            if (_players.TryGetValue(sessionId, out var ctrl))
            {
                Destroy(ctrl.gameObject);
                _players.Remove(sessionId);
            }
        });

        // --- Blobs ---
        callbacks.OnAdd(state => state.blobs, (id, blob) =>
        {
            SpawnBlob(id, blob);
        });

        callbacks.OnRemove(state => state.blobs, (id, blob) =>
        {
            if (_blobs.TryGetValue(id, out var view))
            {
                PlayerController localPlayer = null;
                foreach (var p in _players.Values)
                    if (p._isLocal) { localPlayer = p; break; }

                bool nearLocal = localPlayer != null &&
                    HorizontalDistance(localPlayer.transform.position, view.transform.position) <= pickupSoundRadius;

                // Only show floating score if local player is nearby (they collected it)
                if (Camera.main != null && nearLocal && localPlayer != null)
                {
                    var canvas = localPlayer.GetComponentInChildren<Canvas>();
                    if (canvas != null)
                        FloatingScore.Spawn(canvas, view.transform.position, (int)blob.value, Camera.main);
                }

                if (nearLocal && AudioManager.Instance != null)
                {
                    if (blob.isSpecial)
                        AudioManager.Instance.PlaySpecialItem();
                    else
                        AudioManager.Instance.PlayPickupBlob();
                }

                if (view != null && view.gameObject != null)
                {
                    Destroy(view.gameObject);
                }
                _blobs.Remove(id);
            }
        });

        // --- Died Message ---
        room.OnMessage<DiedMessage>("died", (msg) =>
        {
            Debug.Log($"[DEATH] Message received: killedBy={msg.killedBy}, score={msg.finalScore}");
            AudioManager.Instance?.PlayDeath();
            UIManager.Instance.ShowDeathScreen(msg.killedBy, msg.finalScore);
        });

        SpawnSplitterSpikes();
    }

    void SpawnSplitterSpikes()
    {
        if (splitterSpikePrefab == null) return;

        float margin = 4f;
        float half = Mathf.Max(5f, splitterSpikeArenaHalfSize - margin);
        for (int i = 0; i < splitterSpikeCount; i++)
        {
            float x = Random.Range(-half, half);
            float z = Random.Range(-half, half);
            float s = Random.Range(splitterSpikeScaleMin, splitterSpikeScaleMax);
            var go = Instantiate(splitterSpikePrefab, new Vector3(x, 0f, z), Quaternion.identity);
            var spike = go.GetComponent<SplitterSpike>();
            if (spike != null)
                spike.InitializeForArena(new Vector3(s, s, s));
        }
    }

    public bool TryGetPlayer(string sessionId, out PlayerController controller)
    {
        return _players.TryGetValue(sessionId, out controller);
    }

    // Spawns a player in the scene based on the given session ID and player state.
    void SpawnPlayer(string sessionId, PlayerState state)
    {
        var go = Instantiate(playerPrefab, new Vector3(state.x, state.y, state.z), Quaternion.identity);
        var ctrl = go.GetComponent<PlayerController>();
        bool isLocal = sessionId == NetworkManager.Instance.Room.SessionId;
        ctrl.Init(sessionId, state, isLocal);
        _players[sessionId] = ctrl;

        // Asign OrbitCamera to SidePanelsUI only for local player.
        if (isLocal)
        {
            var sidePanels = FindAnyObjectByType<SidePanelsUI>();
            if (sidePanels != null)
                sidePanels.orbitCamera = ctrl.GetComponentInChildren<OrbitCamera>();
        }
    }

    // Spawns a blob pickup in the scene based on the given ID and blob pickup state.
    void SpawnBlob(string id, BlobPickup blob)
    {
        float halfScale = blobPickupPrefab.transform.localScale.y * 0.5f;

        var go = Instantiate(blobPickupPrefab, new Vector3(blob.x, 0f, blob.z), Quaternion.identity);
        var view = go.GetComponent<BlobPickupView>();
        view.Init(blob);
        _blobs[id] = view;
    }

    static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }
}

[System.Serializable]
public class DiedMessage
{
    public string killedBy;
    public int finalScore;
}