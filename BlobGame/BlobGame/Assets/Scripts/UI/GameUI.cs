using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

public class GameUI : MonoBehaviour
{
    [Header("Top Left")]
    public Button btnLobby;
    public Text txtScore;
    public Text txtKills;

    [Header("Leaderboard")]
    public GameObject leaderboardEntryPrefab;
    public Transform leaderboardContainer;

    private List<GameObject> _entries = new();

    void Start()
    {
        btnLobby.onClick.AddListener(() => NetworkManager.Instance.LeaveGame());
    }

    void Update()
    {
        if (NetworkManager.Instance?.Room == null) return;

        // Disable lobby button while death panel is showing
        if (UIManager.Instance != null)
            btnLobby.interactable = !UIManager.Instance.deathPanel.Panel.activeSelf;

        UpdateTopLeft();
        UpdateLeaderboard();
    }

    void UpdateTopLeft()
    {
        var room = NetworkManager.Instance.Room;
        PlayerState local = null;

        room.State.players.ForEach((sessionId, state) =>
        {
            if (sessionId == room.SessionId)
                local = state;
        });

        if (local == null) return;

        txtScore.text = $"{PlayerController.FormatNumber(local.score)}";
        txtKills.text = $"{local.kills}";
    }

    void UpdateLeaderboard()
    {
        var room = NetworkManager.Instance.Room;
        var localId = room.SessionId;

        // Organize players by score (include dead players so leaderboard stays visible when dead)
        var sorted = new List<(string id, PlayerState state)>();
        room.State.players.ForEach((sessionId, state) =>
        {
            sorted.Add((sessionId, state));
        });
        sorted = sorted.OrderByDescending(p => p.state.score).ToList();

        int localRank = sorted.FindIndex(p => p.id == localId);
        if (localRank < 0) return;

        // Show the four closest players to the local player
        int start;
        if (localRank == 0) start = 0;
        else if (localRank == 1) start = 0;
        else start = localRank - 2;

        // Make sure to not exceed the range
        int total = sorted.Count;
        int end = Mathf.Min(start + 4, total - 1);
        start = Mathf.Max(0, end - 4);

        var toShow = sorted.Skip(start).Take(5).ToList();

        // Clean old entries
        foreach (var e in _entries) Destroy(e);
        _entries.Clear();

        // Create new entries
        for (int i = 0; i < toShow.Count; i++)
        {
            var p = toShow[i];
            var go = Instantiate(leaderboardEntryPrefab, leaderboardContainer);
            var txt = go.GetComponentInChildren<Text>();
            int rank = start + i + 1;

            txt.text = $"{rank}. {p.state.name}  {PlayerController.FormatNumber(p.state.score)}";

            // highlight local player
            if (p.id == localId)
            {
                txt.fontStyle = FontStyle.Bold;
                txt.color = new Color(0f, 0f, 0f);
            }

            _entries.Add(go);
        }
    }
}