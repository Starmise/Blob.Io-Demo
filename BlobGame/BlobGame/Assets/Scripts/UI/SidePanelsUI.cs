using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class SidePanelsUI : MonoBehaviour
{
    [Header("Botones laterales")]
    public Button btnSkins;
    public Button btnDailyBonus;
    public Button btnLuckySpin;
    public Button btnGifts;

    [Header("Paneles")]
    public GameObject skinsPanel;
    public GameObject dailyBonusPanel;
    public GameObject luckySpinPanel;
    public GameObject giftsPanel;

    [Header("Referencias")]
    public OrbitCamera orbitCamera;

    private GameObject _currentPanel;

    void Start()
    {
        // Cerrar todos los paneles al inicio
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
        // Si el panel ya está abierto, cerrarlo
        if (_currentPanel == panel)
        {
            panel.SetActive(false);
            _currentPanel = null;
            orbitCamera?.SetRotationEnabled(true);
            return;
        }

        // Cerrar panel anterior si había uno
        if (_currentPanel != null)
            _currentPanel.SetActive(false);

        // Abrir nuevo panel
        _currentPanel = panel;
        panel.SetActive(true);

        // Deshabilitar rotación de cámara mientras hay panel abierto
        orbitCamera?.SetRotationEnabled(false);
    }
}