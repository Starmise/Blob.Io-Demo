using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.EventSystems;

public class LobbyManager : MonoBehaviour
{
    [Header("Buttons")]
    public Button btnWebsite;
    public Button btnSettings;

    [Header("Panel Settings")]
    public GameObject settingsPanel;
    public Button btnMusicMute;
    public Button btnMute;
    //public Text txtMuteLabel;

    [Header("Prompt and Name")]
    public Text txtStartPrompt;
    public InputField nameInput;

    private bool _settingsOpen = false;
    private bool _starting = false;

    void Start()
    {
        settingsPanel.SetActive(false);

        btnWebsite.onClick.AddListener(() =>
            Application.OpenURL("https://legionplatforms.com/"));

        btnSettings.onClick.AddListener(ToggleSettings);

        // Sound_btn = master (music + SFX). Music_btn = music only.
        btnMute.onClick.AddListener(ToggleAllSound);
        btnMusicMute.onClick.AddListener(ToggleMusicOnly);

        // Precompute lobby preview values so the lobby player can show them.
        // This must match what StartGame() will use.
        if (NetworkManager.Instance != null)
        {
            string enteredName = nameInput != null ? nameInput.text.Trim() : "";
            if (string.IsNullOrEmpty(enteredName))
                NetworkManager.Instance.LocalPlayerName = $"blob{Random.Range(100, 9999)}";
            else
                NetworkManager.Instance.LocalPlayerName = enteredName;

            NetworkManager.Instance.LocalSkinId = PlayerPrefs.GetString("SelectedSkin", "default");
        }

        StartCoroutine(BlinkPrompt());
    }

    void Update()
    {
        if (_starting) return;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            StartGame();
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            // Igonre clicks on Canvas components
            if (EventSystem.current.IsPointerOverGameObject())
                return;

            //if (_settingsOpen) return;

            StartGame();
        }
    }

    void ToggleSettings()
    {
        _settingsOpen = !_settingsOpen;
        settingsPanel.SetActive(_settingsOpen);
    }

    void ToggleAllSound()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.ToggleSound();
    }

    void ToggleMusicOnly()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.ToggleMusic();
    }

    async void StartGame()
    {
        _starting = true;
        txtStartPrompt.text = "Connecting...";

        // Assign name only if the user entered one; otherwise keep the precomputed lobby value.
        string enteredName = nameInput != null ? nameInput.text.Trim() : "";
        if (!string.IsNullOrEmpty(enteredName))
            NetworkManager.Instance.LocalPlayerName = enteredName;

        // Load saved skin, fallback to default
        NetworkManager.Instance.LocalSkinId =
            PlayerPrefs.GetString("SelectedSkin", "default");

        await NetworkManager.Instance.JoinGame();
    }

    IEnumerator BlinkPrompt()
    {
        Color c = txtStartPrompt.color;

        while (true)
        {
            float alpha = Mathf.PingPong(Time.time * 0.8f, 1f);
            c.a = Mathf.Lerp(0.2f, 1f, alpha); // A lerp for a smoother blinking
            txtStartPrompt.color = c;

            yield return null;
        }
    }
}