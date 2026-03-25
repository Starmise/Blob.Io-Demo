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

        // Asign random name automatically if it wasn't sellected manually
        int randomId = Random.Range(100, 9999);
        string enteredName = nameInput != null ? nameInput.text.Trim() : "";
        NetworkManager.Instance.LocalPlayerName =
            string.IsNullOrEmpty(enteredName) ? $"blob{Random.Range(100, 9999)}" : enteredName;

        // Load saved skin, fallback to default
        string savedSkin = PlayerPrefs.GetString("SelectedSkin", "default");
        NetworkManager.Instance.LocalSkinId = savedSkin;

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