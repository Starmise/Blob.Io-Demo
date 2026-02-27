using UnityEngine;
using UnityEngine.UI;

public class SkinsPanel : MonoBehaviour
{
    [Header("Botones de color")]
    public Button[] colorButtons;

    // Colores disponibles
    private string[] _colors = new string[]
    {
        "#FFD700", // Amarillo (default)
        "#FF4444", // Rojo
        "#44AAFF", // Azul
        "#44FF88", // Verde
        "#FF44FF", // Morado
        "#FF8844", // Naranja
        "#FFFFFF", // Blanco
        "#222222", // Negro
    };

    void Start()
    {
        // Asignar color y listener a cada botón
        for (int i = 0; i < colorButtons.Length && i < _colors.Length; i++)
        {
            string hex = _colors[i];
            Color color;
            ColorUtility.TryParseHtmlString(hex, out color);

            // Pintar el botón con el color que representa
            colorButtons[i].GetComponent<Image>().color = color;

            colorButtons[i].onClick.AddListener(() => SelectColor(hex));
        }
    }

    void SelectColor(string hex)
    {
        NetworkManager.Instance.LocalPlayerColor = hex;

        // Notificar al servidor
        NetworkManager.Instance.Room?.Send("setColor", new { color = hex });

        // Cambiar color visual del jugador local inmediatamente
        var localPlayer = FindLocalPlayer();
        if (localPlayer != null)
        {
            Color color;
            if (ColorUtility.TryParseHtmlString(hex, out color))
                localPlayer.bodyRenderer.material.color = color;
        }
    }

    PlayerController FindLocalPlayer()
    {
        foreach (var p in FindObjectsOfType<PlayerController>())
            if (p._isLocal) return p;
        return null;
    }
}