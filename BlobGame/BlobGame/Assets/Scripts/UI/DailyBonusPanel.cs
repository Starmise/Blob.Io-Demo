using UnityEngine;
using UnityEngine.UI;
using System;

public class DailyBonusPanel : MonoBehaviour
{
    [System.Serializable]
    public class DayRewards
    {
        public Button claimButton;
        public Text statusText;  // Shows reward description and claim status
    }

    [Header("Day rewards (7 total)")]
    public DayRewards[] days;

    private readonly string[] _rewardLabels =
        { "3K pts", "1 spin", "12K pts", "2 spins", "20K pts", "3 spins", "50K pts" };
    private readonly int[] _points =
        { 3000, 0, 12000, 0, 20000, 0, 50000 };
    private readonly int[] _spins =
        { 0, 1, 0, 2, 0, 3, 0 };

    private const string LAST_CLAIM_KEY = "DailyLastClaim";
    private const string CURRENT_DAY_KEY = "DailyCurrentDay";
    private const string SPINS_KEY = "PlayerSpins";

    private int _currentDay;
    private bool _claimedToday;

    void OnEnable() => RefreshPanel();

    void RefreshPanel()
    {
        LoadState();

        for (int i = 0; i < days.Length; i++)
        {
            int dayIndex = i;

            // Remove previous listeners to avoid stacking
            days[i].claimButton.onClick.RemoveAllListeners();
            days[i].claimButton.onClick.AddListener(() => ClaimDay(dayIndex));

            // Always show reward in statusText
            days[i].statusText.text = _rewardLabels[i];

            if (i < _currentDay || (i == _currentDay && _claimedToday))
            {
                // Already claimed day
                days[i].claimButton.interactable = false;
                days[i].claimButton.GetComponentInChildren<Text>().text = "Claimed";
            }
            else if (i == _currentDay && !_claimedToday)
            {
                // Available today
                days[i].claimButton.interactable = true;
                days[i].claimButton.GetComponentInChildren<Text>().text = "Claim";
            }
            else
            {
                // Locked future day
                days[i].claimButton.interactable = false;
                days[i].claimButton.GetComponentInChildren<Text>().text = "Locked";
            }
        }
    }

    void ClaimDay(int index)
    {
        if (index != _currentDay || _claimedToday) return;

        // Grant reward
        if (_points[index] > 0)
            NetworkManager.Instance.Room?.Send("addScore", new { amount = _points[index] });

        if (_spins[index] > 0)
        {
            int current = PlayerPrefs.GetInt(SPINS_KEY, 0);
            PlayerPrefs.SetInt(SPINS_KEY, current + _spins[index]);
        }

        // Mark as claimed today BEFORE incrementing day
        _claimedToday = true;
        PlayerPrefs.SetString(LAST_CLAIM_KEY, DateTime.UtcNow.ToString("yyyy-MM-dd"));

        // Advance day
        _currentDay++;
        if (_currentDay >= 7) _currentDay = 0;
        PlayerPrefs.SetInt(CURRENT_DAY_KEY, _currentDay);

        PlayerPrefs.Save();
        RefreshPanel();
    }

    void LoadState()
    {
        _currentDay = PlayerPrefs.GetInt(CURRENT_DAY_KEY, 0);

        string lastClaim = PlayerPrefs.GetString(LAST_CLAIM_KEY, "");

        if (string.IsNullOrEmpty(lastClaim))
        {
            _claimedToday = false;
            return;
        }

        DateTime last = DateTime.Parse(lastClaim).Date;
        DateTime today = DateTime.UtcNow.Date;

        if (last == today)
        {
            // Already claimed today — currentDay is already the NEXT day
            _claimedToday = true;
            // Step back to show current claimed day correctly
            _currentDay = Mathf.Max(0, _currentDay - 1);
        }
        else if ((today - last).TotalDays > 1)
        {
            // Missed a day — reset
            _currentDay = 0;
            _claimedToday = false;
            PlayerPrefs.SetInt(CURRENT_DAY_KEY, 0);
            PlayerPrefs.DeleteKey(LAST_CLAIM_KEY);
            PlayerPrefs.Save();
        }
        else
        {
            // New day, not yet claimed
            _claimedToday = false;
        }
    }
}