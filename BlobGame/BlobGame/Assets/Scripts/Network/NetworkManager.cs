using UnityEngine;
using UnityEngine.SceneManagement;
using Colyseus;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Manages the network connection to the Colyseus server, including joining and leaving game rooms
/// and sending player input for movement. It holds a reference to the Colyseus client and the current game room,
/// and provides methods for joining a game, leaving a game, and sending movement input to the server.
/// </summary>
public class NetworkManager : MonoBehaviour
{
    public static NetworkManager Instance;

    public string serverAddress = "wss://blob-io-demo.onrender.com/";

    public string LocalPlayerName = "Player";
    public string LocalPlayerColor = "#4488FF";
    public string LocalSkinId = "default";

    public bool IsInGame { get; private set; } = false;

    public Client client;
    public Room<GameState> Room { get; private set; }

    private void Awake()
    {
        //#if UNITY_EDITOR
        //        serverAddress = "ws://localhost:2567";
        //#else
        //    serverAddress = "wss://unparched-censurably-desmond.ngrok-free.dev";
        //#endif
        // Singleton pattern to ensure only one instance of NetworkManager exists and it persists across scenes.
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Joins a game room on the server with the specified player name and color. If
    /// the room doesn't exist, it will be created. After joining the room, it loads the game scene.
    /// </summary> 
    /// <returns>  
    /// A task that represents the asynchronous operation of joining a game room.
    /// </returns>
    public async Task JoinGame()
    {
        client = new Client(serverAddress);

        Room = await client.JoinOrCreate<GameState>(
            "game_room",
            new Dictionary<string, object>
            {
                { "name",   LocalPlayerName  },
                { "color",  LocalPlayerColor },
                { "skinId", LocalSkinId      }
            }
        );

        Debug.Log($"Joined room! SessionId: {Room.SessionId}");

        var tcs = new TaskCompletionSource<bool>();

        Room.OnStateChange += (state, isFirstState) =>
        {
            if (isFirstState)
            {
                tcs.TrySetResult(true);
            }
        };

        await tcs.Task;

        IsInGame = true;
        SceneManager.LoadScene("Game");
    }

    /// <summary>
    /// Leaves the current game room and returns to the lobby scene. It also disposes of
    /// the Colyseus client to clean up the connection to the server.
    /// </summary>
    public async void LeaveGame()
    {
        if (Room != null)
        {
            await Room.Leave();
            Room = null;
        }

        if (!IsInGame) return;
        IsInGame = false;
        Room?.Leave();
        Room = null;
        SceneManager.LoadScene("Lobby");
    }

    public void SendMove(float x, float z)
    {
        // Send a "move" message to the server with the desired movement direction.
        Room?.Send("move", new { x, z });
    }
}