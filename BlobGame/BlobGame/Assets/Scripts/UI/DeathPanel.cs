using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class DeathPanel : MonoBehaviour
{
    [Header("UI References")]
    public Text txtFinalScore;
    public Text txtMaxScore;
    public Button btnRevive;
    public Button btnRestart;
    public Text txtCountdown;

    private int _maxScore = 0;
    private int _countdownTime = 10;

    void Start()
    {
        gameObject.SetActive(false);

        btnRevive.onClick.AddListener(OnRevive);
        btnRestart.onClick.AddListener(OnRestart);
    }

    void Update()
    {
        // Restart on Space only when panel is active
        if (gameObject.activeSelf && Input.GetKeyDown(KeyCode.Space))
            OnRestart();
    }

    public void Show(int finalScore)
    {
        // Track max score for the session
        if (finalScore > _maxScore) _maxScore = finalScore;

        txtFinalScore.text = $"Score: {PlayerController.FormatNumber(finalScore)}";
        txtMaxScore.text = $"Best: {PlayerController.FormatNumber(_maxScore)}";

        // Disable player input
        var local = FindLocalPlayer();
        if (local != null) local.enabled = false;

        gameObject.SetActive(true);
        StartCoroutine(CountdownRoutine());
    }

    IEnumerator CountdownRoutine()
    {
        int t = _countdownTime;
        while (t > 0)
        {
            txtCountdown.text = $"El juego se reiniciará automáticamente en: {t}";
            yield return new WaitForSeconds(1f);
            t--;
        }
        OnRestart();
    }

    void OnRevive()
    {
        Debug.Log("[AD] Simulated ad watched — player revived!");
        StopAllCoroutines();
        gameObject.SetActive(false);

        // Re-enable player
        var local = FindLocalPlayer();
        if (local != null) local.enabled = true;

        // Respawn immediately via server
        NetworkManager.Instance.Room?.Send("requestRespawn", new { });
    }

    void OnRestart()
    {
        StopAllCoroutines();
        gameObject.SetActive(false);
        NetworkManager.Instance.LeaveGame();
    }

    PlayerController FindLocalPlayer()
    {
        foreach (var p in FindObjectsOfType<PlayerController>())
            if (p._isLocal) return p;
        return null;
    }
}