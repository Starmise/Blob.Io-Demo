using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class SimulatedPanels : MonoBehaviour
{
    [Header("Daily Bonus")]
    public Text txtDailyBonus;
    public Button btnClaimBonus;
    private bool _bonusClaimed = false;

    [Header("Lucky Spin")]
    public Text txtSpinResult;
    public Button btnSpin;
    private bool _spinning = false;

    [Header("Gifts")]
    public Text txtGiftTimer;
    public Button btnClaimGift;
    private float _giftTimer = 300f; // 5 minutes
    private bool _giftReady = false;

    void Start()
    {
        btnClaimBonus.onClick.AddListener(ClaimDailyBonus);
        btnSpin.onClick.AddListener(DoSpin);
        btnClaimGift.onClick.AddListener(ClaimGift);

        UpdateGiftButton();
    }

    void Update()
    {
        // Gift Timer
        if (!_giftReady)
        {
            _giftTimer -= Time.deltaTime;
            if (_giftTimer <= 0)
            {
                _giftReady = true;
                UpdateGiftButton();
            }
            else
            {
                int mins = Mathf.FloorToInt(_giftTimer / 60);
                int secs = Mathf.FloorToInt(_giftTimer % 60);
                txtGiftTimer.text = $"Next gift in: {mins:00}:{secs:00}";
            }
        }
    }

    // === Daily Bonus ===
    void ClaimDailyBonus()
    {
        if (_bonusClaimed) return;
        _bonusClaimed = true;
        txtDailyBonus.text = "Bonus claimed! +4k points";
        btnClaimBonus.interactable = false;

        // Simulate points (this should be in the server)
        Debug.Log("[BONUS] Daily bonus claimed: +500 points");
    }

    // === Lucky Spin ===
    void DoSpin()
    {
        if (_spinning) return;
        StartCoroutine(SpinRoutine());
    }

    IEnumerator SpinRoutine()
    {
        _spinning = true;
        btnSpin.interactable = false;
        txtSpinResult.text = "Spinning...";

        yield return new WaitForSeconds(1.5f);

        // Resultado aleatorio
        int roll = Random.Range(0, 3);
        txtSpinResult.text = roll switch
        {
            0 => "+1000 points!",
            1 => "Extra spin!",
            _ => "Better luck next time"
        };

        yield return new WaitForSeconds(3f);
        btnSpin.interactable = true;
        _spinning = false;
    }

    // === Gifts ===
    void ClaimGift()
    {
        if (!_giftReady) return;
        _giftReady = false;
        _giftTimer = 300f;
        txtGiftTimer.text = "Gift claimed! +200 points";
        UpdateGiftButton();
    }

    void UpdateGiftButton()
    {
        btnClaimGift.interactable = _giftReady;
    }
}