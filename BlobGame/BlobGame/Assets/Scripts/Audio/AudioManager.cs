using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Central audio singleton: background music and one-shot SFX. Lives in Lobby (DontDestroyOnLoad) and applies
/// to the Game scene — use Settings in Lobby only; toggles are stored in PlayerPrefs and re-applied on load.
/// Sound toggle = master (music + SFX + UI). Music toggle = music only (SFX still obey master).
/// </summary>
[DefaultExecutionOrder(-50)]
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    const string PrefsSound = "Audio_SoundEnabled";
    const string PrefsMusic = "Audio_MusicEnabled";

    [Header("Sources")]
    [SerializeField] AudioSource musicSource;
    [SerializeField] AudioSource sfxSource;

    [Header("Clips — assign in Inspector")]
    [SerializeField] AudioClip backgroundMusic;
    [SerializeField] AudioClip eatPlayer;
    [SerializeField] AudioClip pickupBlob;
    [SerializeField] AudioClip death;
    [SerializeField] AudioClip specialItem;
    [SerializeField] AudioClip uiClick;
    [SerializeField] AudioClip splitterSpikeTouch;

    [Header("Levels")]
    [Range(0f, 1f)] [SerializeField] float musicVolume = 0.6f;
    [Range(0f, 1f)] [SerializeField] float sfxVolume = 1f;

    /// <summary>Master: when false, no SFX and no music.</summary>
    public bool SoundEnabled { get; private set; } = true;
    /// <summary>Music layer: when false, music is silent (SFX still play if SoundEnabled).</summary>
    public bool MusicEnabled { get; private set; } = true;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
        }
        else
        {
            musicSource.loop = true;
            musicSource.playOnAwake = false;
        }

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.loop = false;
            sfxSource.playOnAwake = false;
        }
        else
        {
            sfxSource.loop = false;
            sfxSource.playOnAwake = false;
        }

        LoadPrefs();
        ApplyVolumesAndMusic();

        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureSingleAudioListener();
    }

    void Start()
    {
        // Catch cameras/listeners created after our Awake (first scene).
        EnsureSingleAudioListener();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        LoadPrefs();
        ApplyVolumesAndMusic();
        EnsureSingleAudioListener();
    }

    /// <summary>
    /// Lobby's listener lives on the lobby camera (destroyed when Game loads). The Game scene has no listener.
    /// Keep one listener on this DontDestroyOnLoad object so SFX/music work in every scene.
    /// </summary>
    void EnsureSingleAudioListener()
    {
        var mine = GetComponent<AudioListener>();
        if (mine == null)
            mine = gameObject.AddComponent<AudioListener>();
        mine.enabled = true;

        var all = FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var l in all)
        {
            if (l != null && l != mine)
                l.enabled = false;
        }
    }

    void LoadPrefs()
    {
        SoundEnabled = PlayerPrefs.GetInt(PrefsSound, 1) != 0;
        MusicEnabled = PlayerPrefs.GetInt(PrefsMusic, 1) != 0;
    }

    public void SetSoundEnabled(bool on)
    {
        SoundEnabled = on;
        PlayerPrefs.SetInt(PrefsSound, on ? 1 : 0);
        PlayerPrefs.Save();
        ApplyVolumesAndMusic();
    }

    public void SetMusicEnabled(bool on)
    {
        MusicEnabled = on;
        PlayerPrefs.SetInt(PrefsMusic, on ? 1 : 0);
        PlayerPrefs.Save();
        ApplyVolumesAndMusic();
    }

    public void ToggleSound() => SetSoundEnabled(!SoundEnabled);
    public void ToggleMusic() => SetMusicEnabled(!MusicEnabled);

    void ApplyVolumesAndMusic()
    {
        bool playMusic = SoundEnabled && MusicEnabled && backgroundMusic != null;

        musicSource.volume = playMusic ? musicVolume : 0f;
        sfxSource.volume = SoundEnabled ? sfxVolume : 0f;

        if (playMusic)
        {
            if (musicSource.clip != backgroundMusic)
                musicSource.clip = backgroundMusic;
            if (!musicSource.isPlaying)
                musicSource.Play();
        }
        else
        {
            musicSource.Stop();
        }
    }

    void PlaySfx(AudioClip clip)
    {
        if (clip == null || !SoundEnabled || sfxSource == null) return;
        sfxSource.PlayOneShot(clip);
    }

    public void PlayEatPlayer() => PlaySfx(eatPlayer);
    public void PlayPickupBlob() => PlaySfx(pickupBlob);
    public void PlayDeath() => PlaySfx(death);
    public void PlaySpecialItem() => PlaySfx(specialItem);
    public void PlayUiClick() => PlaySfx(uiClick);
    public void PlaySplitterSpikeTouch() => PlaySfx(splitterSpikeTouch);

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this)
            Instance = null;
    }
}
