using UnityEngine;
using UnityEngine.SceneManagement;
using Colyseus;
using System.Collections.Generic;
using System.Threading.Tasks;

public class NetworkManager : MonoBehaviour
{
    public static NetworkManager Instance;

    public string serverAddress = "ws://localhost:2567";

    public string LocalPlayerName = "Player";
    public string LocalPlayerColor = "#4488FF";

    public Client client;
    public Room<GameState> Room { get; private set; }

    private void Awake()
    {
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

    public async Task JoinGame()
    {
        client = new Client(serverAddress);

        Room = await client.JoinOrCreate<GameState>(
            "game_room",
            new Dictionary<string, object>
            {
                { "name", LocalPlayerName },
                { "color", LocalPlayerColor }
            }
        );

        Debug.Log($"Joined room! SessionId: {Room.SessionId}");

        SceneManager.LoadScene("Game");
    }

    public async void LeaveGame()
    {
        if (Room != null)
        {
            await Room.Leave();
            Room = null;
        }

        SceneManager.LoadScene("Lobby");
    }

    public void SendMove(float x, float z)
    {
        Room?.Send("move", new { x, z });
    }
}