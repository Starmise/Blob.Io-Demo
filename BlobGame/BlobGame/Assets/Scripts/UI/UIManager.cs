using UnityEngine;

// Temporal UI Manager
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    public DeathPanel deathPanel;

    void Awake()
    {
        Instance = this;
    }

    public void ShowDeathScreen(string killedBy, int finalScore)
    {
        Debug.Log($"Killed by {killedBy}");
        deathPanel.Show(finalScore);
    }

    //public void ResetPlayerPrefs()
    //{
    //    PlayerPrefs.DeleteAll();
    //    PlayerPrefs.Save();
    //    Debug.Log("PlayerPrefs reset!");
    //}
}
