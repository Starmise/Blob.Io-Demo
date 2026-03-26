using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class SpinPanel : MonoBehaviour
{
    [Header("UI References")]
    public Button btnSpin;
    public Text txtSpinsAvailable;
    public Text txtFreeTimer;

    [Header("Wheel")]
    [Tooltip("Assign Spin_Img (wheel). If null, a child named Spin_Img is used.")]
    [SerializeField] RectTransform wheelTransform;

    [Tooltip("Full rotations before landing on the result.")]
    [SerializeField] int minFullRotations = 4;
    [SerializeField] int maxFullRotations = 7;

    [SerializeField] float spinDurationSeconds = 3.5f;

    private const string SPINS_KEY = "PlayerSpins";

    private float _freeTimer = 120f;
    private bool _timerReady;

    private bool _spinning;
    private RectTransform[] _prizeRects;
    private float[] _prizeLocalZ;

    /// <summary>Prize copy and server/local reward (index matches SpinPrize naming: 0 = "SpinPrize", 1 = "SpinPrize (1)", …).</summary>
    private static readonly string[] PrizeLabels =
    {
        "8k",
        "+1",
        "+2",
        "5k",
        "3k",
        "+2",
        "8k",
        "Nothing",
    };

    void Awake()
    {
        if (GetComponent<RectMask2D>() == null)
            gameObject.AddComponent<RectMask2D>();

        if (wheelTransform == null)
        {
            var t = transform.Find("Spin_Img");
            if (t != null) wheelTransform = t as RectTransform;
        }

        CachePrizeSegments();
        PopulatePrizeLabels();

        if (btnSpin != null)
            btnSpin.onClick.AddListener(OnSpinClicked);
    }

    void OnEnable() => RefreshSpinButton();

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

    void OnSpinClicked()
    {
        if (_spinning || wheelTransform == null) return;

        int spins = PlayerPrefs.GetInt(SPINS_KEY, 0);
        bool useFree = spins <= 0 && _timerReady;

        if (spins <= 0 && !_timerReady)
            return;

        if (spins > 0)
        {
            PlayerPrefs.SetInt(SPINS_KEY, spins - 1);
            PlayerPrefs.Save();
        }
        else if (useFree)
        {
            _timerReady = false;
            _freeTimer = 120f;
        }

        RefreshSpinButton();
        StartCoroutine(SpinRoutine(Random.Range(0, PrizeLabels.Length)));
    }

    IEnumerator SpinRoutine(int prizeIndex)
    {
        _spinning = true;
        if (btnSpin != null) btnSpin.interactable = false;

        float startZ = wheelTransform.localEulerAngles.z;
        int extraTurns = Random.Range(minFullRotations, maxFullRotations + 1);
        float prizeZ = _prizeLocalZ[prizeIndex];
        float desiredMod = Mathf.Repeat(-prizeZ, 360f);
        float currentMod = Mathf.Repeat(startZ, 360f);
        float delta = Mathf.DeltaAngle(currentMod, desiredMod);
        float endZ = startZ + 360f * extraTurns + delta;

        float elapsed = 0f;
        while (elapsed < spinDurationSeconds)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / spinDurationSeconds);
            // Ease-out cubic
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            float z = Mathf.Lerp(startZ, endZ, eased);
            wheelTransform.localEulerAngles = new Vector3(0f, 0f, z);
            yield return null;
        }

        wheelTransform.localEulerAngles = new Vector3(0f, 0f, endZ);
        GrantPrize(prizeIndex);

        _spinning = false;
        RefreshSpinButton();
    }

    void GrantPrize(int index)
    {
        var room = NetworkManager.Instance != null ? NetworkManager.Instance.Room : null;

        switch (index)
        {
            case 0:
            case 6:
                if (room != null) room.Send("addScore", new { amount = 8000 });
                break;
            case 1:
                AddSpins(1);
                break;
            case 2:
                if (room != null) room.Send("addKills", new { amount = 2 });
                break;
            case 3:
                if (room != null) room.Send("addScore", new { amount = 5000 });
                break;
            case 4:
                if (room != null) room.Send("addScore", new { amount = 3000 });
                break;
            case 5:
                AddSpins(2);
                break;
            case 7:
            default:
                break;
        }
    }

    static void AddSpins(int amount)
    {
        int current = PlayerPrefs.GetInt(SPINS_KEY, 0);
        PlayerPrefs.SetInt(SPINS_KEY, current + amount);
        PlayerPrefs.Save();
    }

    void CachePrizeSegments()
    {
        if (wheelTransform == null) return;

        _prizeRects = new RectTransform[8];
        _prizeLocalZ = new float[8];

        for (int i = 0; i < 8; i++)
        {
            string name = i == 0 ? "SpinPrize" : $"SpinPrize ({i})";
            var t = wheelTransform.Find(name);
            if (t == null)
            {
                Debug.LogWarning($"SpinPanel: missing child '{name}' under Spin_Img.");
                continue;
            }

            _prizeRects[i] = t as RectTransform;
            _prizeLocalZ[i] = _prizeRects[i].localEulerAngles.z;
        }
    }

    void PopulatePrizeLabels()
    {
        if (_prizeRects == null) return;

        for (int i = 0; i < _prizeRects.Length && i < PrizeLabels.Length; i++)
        {
            if (_prizeRects[i] == null) continue;
            var label = _prizeRects[i].GetComponentInChildren<Text>(true);
            if (label != null)
                label.text = PrizeLabels[i];
        }
    }

    void RefreshSpinButton()
    {
        int spins = PlayerPrefs.GetInt(SPINS_KEY, 0);
        if (txtSpinsAvailable != null)
            txtSpinsAvailable.text = $"Spins: {spins}";

        if (btnSpin != null)
            btnSpin.interactable = !_spinning && (spins > 0 || _timerReady);
    }
}
