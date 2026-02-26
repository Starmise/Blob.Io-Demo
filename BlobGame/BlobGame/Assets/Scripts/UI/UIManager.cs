using UnityEngine;

// Temporal UI Manager
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    void Awake()
    {
        Instance = this;
    }

    public void ShowDeathScreen(string killedBy, int finalScore)
    {
        Debug.Log($"Killed by {killedBy} with a score of: {finalScore}");
        // I will complete this method later to show a proper death screen UI, but for now it just logs the message to the console.
    }
}
