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

        // --- Players --------------------------------------------------------
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

        // --- Blobs --------------------------------------------------------
        callbacks.OnAdd(state => state.blobs, (id, blob) =>
        {
            SpawnBlob(id, blob);
        });

        callbacks.OnRemove(state => state.blobs, (id, blob) =>
        {
            if (_blobs.TryGetValue(id, out var view))
            {
                Destroy(view.gameObject);
                _blobs.Remove(id);
            }
        });

        // --- Died Message ---------------------------------------------
        room.OnMessage<DiedMessage>("died", (msg) =>
        {
            UIManager.Instance.ShowDeathScreen(msg.killedBy, msg.finalScore);
        });
    }

    // Spawns a player in the scene based on the given session ID and player state.
    void SpawnPlayer(string sessionId, PlayerState state)
    {
        var go = Instantiate(playerPrefab, new Vector3(state.x, state.y, state.z), Quaternion.identity);
        var ctrl = go.GetComponent<PlayerController>();
        bool isLocal = sessionId == NetworkManager.Instance.Room.SessionId;
        ctrl.Init(sessionId, state, isLocal);
        _players[sessionId] = ctrl;
    }

    // Spawns a blob pickup in the scene based on the given ID and blob pickup state.
    void SpawnBlob(string id, BlobPickup blob)
    {
        var go = Instantiate(blobPickupPrefab, new Vector3(blob.x, 0.2f, blob.z), Quaternion.identity);
        var view = go.GetComponent<BlobPickupView>();
        view.Init(blob);
        _blobs[id] = view;
    }
}

[System.Serializable]
public class DiedMessage
{
    public string killedBy;
    public int finalScore;
}