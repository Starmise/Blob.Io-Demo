using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SkinsPanel : MonoBehaviour
{
    [Header("References")]
    public SkinDatabase skinDatabase;
    public GameObject skinItemPrefab;
    public Transform container;
    public Text txtFeedback;

    private List<SkinDatabase.SkinEntry> _skins = new();

    void Start()
    {
        if (skinDatabase == null || skinItemPrefab == null || container == null)
            return;

        foreach (var skin in skinDatabase.skins)
        {
            var go = Instantiate(skinItemPrefab, container);

            string skinId = skin.skinId;
            int cost = skin.killCost;

            var img = go.transform.Find("Skin_img")?.GetComponent<Image>();
            var btn = go.transform.Find("SkinPurchase_btn")?.GetComponent<Button>();
            var costTxt = go.transform.Find("SkinPurchase_btn/SkinPrice_txt")?.GetComponent<Text>();
            var nameTxt = go.transform.Find("SkinName_txt")?.GetComponent<Text>();

            if (img != null)
                img.sprite = skin.previewSprite;

            if (costTxt != null)
                costTxt.text = $"0{cost}";

            if (nameTxt != null)
                nameTxt.text = skinId;

            if (btn != null)
                btn.onClick.AddListener(() => BuySkin(skinId, cost));
        }
    }

    void BuySkin(string skinId, int killCost)
    {
        var room = NetworkManager.Instance.Room;
        if (room == null) return;

        PlayerState local = null;
        room.State.players.ForEach((id, state) =>
        {
            if (id == room.SessionId) local = state;
        });

        if (local == null) return;

        if (local.kills < killCost)
        {
            if (txtFeedback != null)
                txtFeedback.text = $"Not enough kills! Need {killCost}.";
            return;
        }

        PlayerPrefs.SetString("SelectedSkin", skinId);
        PlayerPrefs.Save();

        NetworkManager.Instance.Room?.Send("buySkin", new
        {
            skinId,
            killCost
        });

        if (txtFeedback != null)
            txtFeedback.text = "Skin equipped!";
    }
}