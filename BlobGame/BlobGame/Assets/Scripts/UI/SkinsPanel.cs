using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class SkinsPanel : MonoBehaviour
{
    [Header("References (single ScrollView setup)")]
    public SkinDatabase skinDatabase;
    public GameObject skinItemPrefab;
    public Transform container; // ScrollView/Viewport/Content
    public ScrollRect scrollRect;
    public Scrollbar verticalScrollbar;
    public Text txtFeedback;

    [Header("Category buttons")]
    public Button btnSeamlessSkins;
    public Button btnFaceSkins;

    private SkinDatabase.SkinCategory _activeCategory = SkinDatabase.SkinCategory.Seamless;
    private const string PurchasedSkinPrefPrefix = "PurchasedSkin_";

    void Start()
    {
        if (skinDatabase == null || skinItemPrefab == null || container == null)
            return;

        if (scrollRect == null) scrollRect = GetComponentInChildren<ScrollRect>(true);
        if (verticalScrollbar == null && scrollRect != null) verticalScrollbar = scrollRect.verticalScrollbar;

        if (btnSeamlessSkins != null)
            btnSeamlessSkins.onClick.AddListener(() => SetCategory(SkinDatabase.SkinCategory.Seamless));
        if (btnFaceSkins != null)
            btnFaceSkins.onClick.AddListener(() => SetCategory(SkinDatabase.SkinCategory.Face));

        RebuildList();
    }

    void SetCategory(SkinDatabase.SkinCategory category)
    {
        _activeCategory = category;
        RebuildList();
    }

    void RebuildList()
    {
        for (int i = container.childCount - 1; i >= 0; i--)
            Destroy(container.GetChild(i).gameObject);

        foreach (var skin in skinDatabase.skins)
        {
            if (skin.category != _activeCategory) continue;

            var go = Instantiate(skinItemPrefab, container);
            string skinId = skin.skinId;
            int cost = skin.killCost;

            var img = go.transform.Find("Skin_img")?.GetComponent<Image>();
            var btn = go.transform.Find("SkinPurchase_btn")?.GetComponent<Button>();
            var costTxt = go.transform.Find("SkinPurchase_btn/SkinPrice_txt")?.GetComponent<Text>();
            var nameTxt = go.transform.Find("SkinName_txt")?.GetComponent<Text>();

            if (img != null) img.sprite = skin.previewSprite;
            if (costTxt != null) costTxt.text = IsSkinPurchased(skinId) ? "-" : $"0{cost}";
            if (nameTxt != null) nameTxt.text = skinId;
            if (btn != null) btn.onClick.AddListener(() => BuySkin(skinId, cost));
        }

        StopAllCoroutines();
        StartCoroutine(RefreshScrollAfterLayout());
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

        MarkSkinPurchased(skinId);
        PlayerPrefs.SetString("SelectedSkin", skinId);
        PlayerPrefs.Save();

        NetworkManager.Instance.Room?.Send("buySkin", new { skinId, killCost });

        if (txtFeedback != null)
            txtFeedback.text = "Skin equipped!";

        RebuildList();
    }

    IEnumerator RefreshScrollAfterLayout()
    {
        var rt = container as RectTransform;
        if (rt == null) yield break;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        yield return null;
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

        // Force content height to preferred layout height so ScrollRect bounds include all items.
        float preferredH = LayoutUtility.GetPreferredHeight(rt);
        if (preferredH > 0f)
        {
            var size = rt.sizeDelta;
            size.y = preferredH;
            rt.sizeDelta = size;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }

        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 1f;
            // Recalculate bounds after dynamic rebuild to avoid cutting off the last item.
            scrollRect.StopMovement();
            scrollRect.velocity = Vector2.zero;
            Canvas.ForceUpdateCanvases();
        }

        if (verticalScrollbar != null && scrollRect != null && scrollRect.viewport != null)
        {
            float contentH = rt.rect.height;
            float viewH = scrollRect.viewport.rect.height;
            verticalScrollbar.gameObject.SetActive(contentH > viewH + 1f);
        }
    }

    bool IsSkinPurchased(string skinId)
    {
        if (string.IsNullOrEmpty(skinId)) return false;
        return PlayerPrefs.GetInt(PurchasedSkinPrefPrefix + skinId, 0) == 1;
    }

    void MarkSkinPurchased(string skinId)
    {
        if (string.IsNullOrEmpty(skinId)) return;
        PlayerPrefs.SetInt(PurchasedSkinPrefPrefix + skinId, 1);
    }
}