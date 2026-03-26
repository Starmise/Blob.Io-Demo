using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "SkinDatabase", menuName = "BlobGame/SkinDatabase")]
public class SkinDatabase : ScriptableObject
{
    public enum SkinCategory
    {
        Seamless = 0,
        Face = 1
    }

    [System.Serializable]
    public class SkinEntry
    {
        public string skinId;
        public Material material;
        public Sprite previewSprite; // Variable for teh UI preview
        public int killCost;
        public SkinCategory category = SkinCategory.Seamless;
        [Tooltip("Used by face skins: sprite texture assigned to _FaceTex on the material.")]
        [HideInInspector] public Sprite faceSprite;
    }

    public SkinEntry[] skins;

    private Dictionary<string, SkinEntry> _lookup;

    public void Init()
    {
        _lookup = new Dictionary<string, SkinEntry>();
        foreach (var skin in skins)
            _lookup[skin.skinId] = skin;
    }

    public Material GetMaterial(string skinId)
{
    if (_lookup == null) Init();

    if (string.IsNullOrEmpty(skinId))
        return skins != null && skins.Length > 0 ? skins[0].material : null;

    if (_lookup != null && _lookup.ContainsKey(skinId))
        return _lookup[skinId].material;

    return skins != null && skins.Length > 0 ? skins[0].material : null;
}

    public SkinEntry GetSkin(string skinId)
    {
        if (_lookup == null) Init();

        if (string.IsNullOrEmpty(skinId)) return skins != null && skins.Length > 0 ? skins[0] : null;
        
        if (_lookup.TryGetValue(skinId, out var entry))
            return entry;

        return skins != null && skins.Length > 0 ? skins[0] : null;
    }
}