using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Binds Sound/Music toggle buttons to the persistent AudioManager and updates their visual state.
/// Works in Game scene even when AudioManager was created in Lobby (DontDestroyOnLoad).
/// </summary>
[DisallowMultipleComponent]
public class AudioTogglesUI : MonoBehaviour
{
    [Header("Buttons (assign any subset)")]
    public Button btnSound;
    public Button btnMusic;

    [Header("Behavior")]
    [Tooltip("If true, this component wires button clicks to AudioManager toggles.")]
    public bool bindClickHandlers = true;

    [Header("Visual state")]
    [Range(0f, 1f)] public float onAlpha = 1f;
    [Range(0f, 1f)] public float offAlpha = 0.4f;

    AudioManager _bound;
    bool _hooksAdded;

    void Awake()
    {
        if (bindClickHandlers && !_hooksAdded)
        {
            if (btnSound != null) btnSound.onClick.AddListener(OnSoundClicked);
            if (btnMusic != null) btnMusic.onClick.AddListener(OnMusicClicked);
            _hooksAdded = true;
        }
    }

    void OnEnable()
    {
        StartCoroutine(BindWhenAudioReady());
    }

    void OnDisable()
    {
        Unbind();
    }

    IEnumerator BindWhenAudioReady()
    {
        while (AudioManager.Instance == null)
            yield return null;

        if (_bound == AudioManager.Instance) yield break;

        Unbind();
        _bound = AudioManager.Instance;
        _bound.AudioStateChanged += RefreshVisualState;
        RefreshVisualState();
    }

    void Unbind()
    {
        if (_bound != null)
            _bound.AudioStateChanged -= RefreshVisualState;
        _bound = null;
    }

    void OnSoundClicked()
    {
        AudioManager.Instance?.ToggleSound();
        RefreshVisualState();
    }

    void OnMusicClicked()
    {
        AudioManager.Instance?.ToggleMusic();
        RefreshVisualState();
    }

    void RefreshVisualState()
    {
        var am = AudioManager.Instance;
        if (am == null) return;

        // Sound button reflects master on/off.
        if (btnSound != null)
            SetButtonAlpha(btnSound, am.SoundEnabled ? onAlpha : offAlpha);

        // Music button reflects effective music state.
        bool musicOn = am.SoundEnabled && am.MusicEnabled;
        if (btnMusic != null)
            SetButtonAlpha(btnMusic, musicOn ? onAlpha : offAlpha);
    }

    void SetButtonAlpha(Button b, float alpha)
    {
        if (b == null) return;
        var graphics = b.GetComponentsInChildren<Graphic>(true);
        foreach (var g in graphics)
        {
            var c = g.color;
            c.a = alpha;
            g.color = c;
        }
    }
}
