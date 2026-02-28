using UnityEngine;
using UnityEngine.UI;

public class SkinsPanel : MonoBehaviour
{
    [System.Serializable]
    public class SkinEntry
    {
        public Image skinPreview;  // Read color from this
        public Button buyButton;
        public int killCost = 3;
    }

    [Header("Skin entries (8 total)")]
    public SkinEntry[] skins;

    [Header("Feedback text")]
    public Text txtFeedback;

    void Start()
    {
        for (int i = 0; i < skins.Length; i++)
        {
            int index = i;
            skins[i].buyButton.onClick.AddListener(() => BuySkin(index));
        }
        RefreshButtons();
    }

    void BuySkin(int index)
    {
        var room = NetworkManager.Instance.Room;
        if (room == null) return;

        PlayerState local = null;
        room.State.players.ForEach((id, state) =>
        {
            if (id == room.SessionId) local = state;
        });

        if (local == null) return;

        SkinEntry skin = skins[index];

        if (local.kills < skin.killCost)
        {
            if (txtFeedback != null)
                txtFeedback.text = $"Not enough kills! Need {skin.killCost}.";
            return;
        }

        // Instead of reading colorHex string, read from the preview image
        Color skinColor = skin.skinPreview.color;
        string hex = "#" + ColorUtility.ToHtmlStringRGB(skinColor);

        NetworkManager.Instance.Room?.Send("buySkin", new
        {
            color = hex,
            killCost = skin.killCost
        });

        // Apply color immediately on local player
        Color color;
        if (ColorUtility.TryParseHtmlString(hex, out color))
        {
            var localPlayer = FindLocalPlayer();
            if (localPlayer != null)
                localPlayer.bodyRenderer.material.color = color;
        }

        if (txtFeedback != null)
            txtFeedback.text = "Skin equipped!";

        RefreshButtons();
    }

    PlayerController FindLocalPlayer()
    {
        foreach (var p in FindObjectsOfType<PlayerController>())
            if (p._isLocal) return p;
        return null;
    }

    void RefreshButtons()
    {
        // Refresh button interactability based on kills (updated each time panel opens)
        var room = NetworkManager.Instance?.Room;
        if (room == null) return;

        PlayerState local = null;
        room.State.players.ForEach((id, state) =>
        {
            if (id == room.SessionId) local = state;
        });

        if (local == null) return;

        foreach (var skin in skins)
            skin.buyButton.interactable = local.kills >= skin.killCost;
    }

    // Called when panel opens so buttons are up to date
    void OnEnable() => RefreshButtons();
}