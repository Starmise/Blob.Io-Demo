using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class SidePanelsUI : MonoBehaviour
{
    [Header("Lateral Button")]
    public Button btnSkins;
    public Button btnDailyBonus;
    public Button btnLuckySpin;
    public Button btnGifts;

    [Header("Panels")]
    public GameObject skinsPanel;
    public GameObject dailyBonusPanel;
    public GameObject luckySpinPanel;
    public GameObject giftsPanel;

    [HideInInspector]
    public OrbitCamera orbitCamera;

    private GameObject _currentPanel;

    void Start()
    {
        // Close all panels at the start
        skinsPanel.SetActive(false);
        dailyBonusPanel.SetActive(false);
        luckySpinPanel.SetActive(false);
        giftsPanel.SetActive(false);

        btnSkins.onClick.AddListener(() => TogglePanel(skinsPanel));
        btnDailyBonus.onClick.AddListener(() => TogglePanel(dailyBonusPanel));
        btnLuckySpin.onClick.AddListener(() => TogglePanel(luckySpinPanel));
        btnGifts.onClick.AddListener(() => TogglePanel(giftsPanel));
    }

    void TogglePanel(GameObject panel)
    {
        // Close the panel if is open
        if (_currentPanel == panel)
        {
            panel.SetActive(false);
            _currentPanel = null;
            orbitCamera?.SetRotationEnabled(true);
            return;
        }

        // In case there were already another panel, close it first
        if (_currentPanel != null)
            _currentPanel.SetActive(false);

        // Open new panel
        _currentPanel = panel;
        panel.SetActive(true);

        orbitCamera?.SetRotationEnabled(false);
    }

    public void CloseCurrentPanel()
    {
        if (_currentPanel != null)
        {
            _currentPanel.SetActive(false);
            _currentPanel = null;
            orbitCamera?.SetRotationEnabled(true);
        }
    }
}