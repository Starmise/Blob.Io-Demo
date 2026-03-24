using UnityEngine;
using UnityEngine.UI;

public class SpinPanel : MonoBehaviour
{
    [Header("UI References")]
    public Button btnSpin;
    public Text txtSpinsAvailable;
    public Text txtFreeTimer;

    private const string SPINS_KEY = "PlayerSpins";
    private float _freeTimer = 120f; // 2 minutes
    private bool _timerReady = false;

    void Awake()
    {
        if (GetComponent<RectMask2D>() == null)
            gameObject.AddComponent<RectMask2D>();
    }

    void OnEnable() => RefreshSpinButton();
    void OnDisable() { }

    void Update()
    {
        if (_timerReady) return;

        _freeTimer -= Time.deltaTime;
        if (_freeTimer <= 0)
        {
            _timerReady = true;
            txtFreeTimer.text = "Free spin ready!";
            RefreshSpinButton();
        }
        else
        {
            int mins = Mathf.FloorToInt(_freeTimer / 60);
            int secs = Mathf.FloorToInt(_freeTimer % 60);
            txtFreeTimer.text = $"Free spin in: {mins:00}:{secs:00}";
        }

        RefreshSpinButton();
    }

    void RefreshSpinButton()
    {
        int spins = PlayerPrefs.GetInt(SPINS_KEY, 0);
        txtSpinsAvailable.text = $"Spins: {spins}";
        btnSpin.interactable = spins > 0 || _timerReady;
    }
}