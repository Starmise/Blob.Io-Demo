using UnityEngine;
using UnityEngine.UI;

public class GiftsPanel : MonoBehaviour
{
    [System.Serializable]
    public class GiftEntry
    {
        public Button claimButton;
        public Text timerText;
        public int points;    // 0 if reward is spins or kills
        public int spins;
        public int kills;
        public float unlockAfterSeconds; // Time since session start
    }

    [Header("Gift entries (8 total)")]
    public GiftEntry[] gifts;

    private const string SPINS_KEY = "PlayerSpins";

    // Unlock times: 1 min apart
    private readonly float[] _unlockTimes =
        { 60f, 120f, 180f, 240f, 300f, 360f, 420f, 480f };

    // Rewards per GDD: 4k, 1spin, 9k, 13k, 2spins, 18k, 8kills, 23k
    private readonly int[] _points = { 4000, 0, 9000, 13000, 0, 18000, 0, 23000 };
    private readonly int[] _spins = { 0, 1, 0, 0, 2, 0, 0, 0 };
    private readonly int[] _kills = { 0, 0, 0, 0, 0, 0, 8, 0 };

    private bool[] _claimed;
    private bool _wired;

    /** Set when the game scene loads (SidePanelsUI). Not when GiftsPanel opens — that GameObject starts inactive. */
    private static float _playSessionStartRealtime;
    private static bool _playSessionStarted;

    /// <summary>Call from an always-active object when the game scene is ready (e.g. SidePanelsUI.Start).</summary>
    public static void InitializePlaySession()
    {
        if (_playSessionStarted) return;
        _playSessionStartRealtime = Time.realtimeSinceStartup;
        _playSessionStarted = true;
    }

    void OnEnable()
    {
        if (!_playSessionStarted)
            InitializePlaySession();

        if (!_wired)
        {
            _claimed = new bool[gifts.Length];

            for (int i = 0; i < gifts.Length && i < _unlockTimes.Length; i++)
            {
                gifts[i].unlockAfterSeconds = _unlockTimes[i];
                gifts[i].points = _points[i];
                gifts[i].spins = _spins[i];
                gifts[i].kills = _kills[i];

                int index = i;
                gifts[i].claimButton.onClick.AddListener(() => ClaimGift(index));
            }

            _wired = true;
        }
    }

    void Update()
    {
        if (_claimed == null) return;

        float elapsed = Time.realtimeSinceStartup - _playSessionStartRealtime;

        for (int i = 0; i < gifts.Length; i++)
        {
            if (_claimed[i])
            {
                gifts[i].claimButton.interactable = false;
                gifts[i].timerText.text = "Claimed!";
                continue;
            }

            float remaining = gifts[i].unlockAfterSeconds - elapsed;

            if (remaining <= 0)
            {
                gifts[i].claimButton.interactable = true;
                gifts[i].timerText.text = "Ready!";
            }
            else
            {
                gifts[i].claimButton.interactable = false;
                int mins = Mathf.FloorToInt(remaining / 60);
                int secs = Mathf.FloorToInt(remaining % 60);
                gifts[i].timerText.text = $"{mins:00}:{secs:00}";
            }
        }
    }

    void ClaimGift(int index)
    {
        if (_claimed == null || _claimed[index]) return;

        float elapsed = Time.realtimeSinceStartup - _playSessionStartRealtime;
        if (elapsed < gifts[index].unlockAfterSeconds) return;

        _claimed[index] = true;

        var room = NetworkManager.Instance.Room;
        if (room == null) return;

        // Grant points
        if (gifts[index].points > 0)
            room.Send("addScore", new { amount = gifts[index].points });

        // Grant kills
        if (gifts[index].kills > 0)
            room.Send("addKills", new { amount = gifts[index].kills });

        // Grant spins locally
        if (gifts[index].spins > 0)
        {
            int current = PlayerPrefs.GetInt(SPINS_KEY, 0);
            PlayerPrefs.SetInt(SPINS_KEY, current + gifts[index].spins);
            PlayerPrefs.Save();
        }
    }
}