using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class DeathPanel : MonoBehaviour
{
    [Header("Panel")]
    [Tooltip("The overlay panel to show/hide on death. Assign the DeathPanel GameObject. If unset, uses this GameObject (will hide GameUI/leaderboard).")]
    public GameObject overlayPanel;

    [Header("UI References")]
    public Text txtFinalScore;
    public Text txtMaxScore;
    public Button btnRevive;
    public Button btnRestart;
    public Text txtCountdown;

    private int _maxScore = 0;
    private int _countdownTime = 10;
    private bool _isShowing = false;
    private float _countdownTimer = 0f;
    private bool _countdownActive = false;

    public GameObject Panel => overlayPanel != null ? overlayPanel : gameObject;

    void Start()
    {
        Panel.SetActive(false);

        btnRevive.onClick.AddListener(OnRevive);
        btnRestart.onClick.AddListener(OnRestart);
    }

    void Update()
    {
        if (Panel.activeSelf && Input.GetKeyDown(KeyCode.Space))
            OnRestart();

        if (!_countdownActive) return;

        _countdownTimer -= Time.deltaTime;
        txtCountdown.text = $"The game will restart automatically in: {Mathf.CeilToInt(_countdownTimer)}";

        if (_countdownTimer <= 0)
        {
            _countdownActive = false;
            OnRestart();
        }
    }

    public void Show(int finalScore)
    {
        if (_isShowing) return;
        _isShowing = true;

        if (finalScore > _maxScore) _maxScore = finalScore;
        txtFinalScore.text = $"Score: {PlayerController.FormatNumber(finalScore)}";
        txtMaxScore.text = $"Best: {PlayerController.FormatNumber(_maxScore)}";

        var local = FindLocalPlayer();
        if (local != null) local.enabled = false;

        _countdownTimer = _countdownTime;
        _countdownActive = true;
        Panel.SetActive(true);
    }

    IEnumerator CountdownRoutine()
    {
        Debug.Log("[DEATH] Countdown started");
        int t = _countdownTime;
        while (t > 0)
        {
            Debug.Log($"[DEATH] Countdown: {t}");
            txtCountdown.text = $"El juego se reiniciará automáticamente en: {t}";
            yield return new WaitForSeconds(1f);
            t--;
        }
        Debug.Log("[DEATH] Countdown finished, calling OnRestart");
        OnRestart();
    }

    void OnRevive()
    {
        _countdownActive = false;
        _isShowing = false;
        Panel.SetActive(false);

        var local = FindLocalPlayer();
        if (local != null) local.enabled = true;

        NetworkManager.Instance.Room?.Send("requestRespawn", new { });
    }

    void OnRestart()
    {
        _countdownActive = false;
        _isShowing = false;
        Panel.SetActive(false);
        NetworkManager.Instance.LeaveGame();
    }

    PlayerController FindLocalPlayer()
    {
        foreach (var p in FindObjectsOfType<PlayerController>())
            if (p._isLocal) return p;
        return null;
    }
}