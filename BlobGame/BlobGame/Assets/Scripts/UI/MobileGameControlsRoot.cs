using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Put on the mobile controls panel root (e.g. MobileController). Shows only on mobile / mobile WebGL,
/// wires Split_btn to the same split action as keyboard E.
/// </summary>
[DisallowMultipleComponent]
public class MobileGameControlsRoot : MonoBehaviour
{
    [Tooltip("When enabled in the Editor, the panel stays visible for testing (does not affect builds).")]
    [SerializeField] bool forceShowInEditor;

    Button _splitButton;

    void Awake()
    {
        bool show = MobileControlsDetector.ShouldShowMobileControls();
#if UNITY_EDITOR
        if (forceShowInEditor) show = true;
#endif
        if (show)
            WireSplitButton();
        if (!show)
            gameObject.SetActive(false);
    }

    void WireSplitButton()
    {
        var splitTf = transform.Find("Split_btn");
        if (splitTf == null) return;
        _splitButton = splitTf.GetComponent<Button>();
        if (_splitButton == null) return;
        _splitButton.onClick.AddListener(OnSplitClicked);
    }

    void OnDestroy()
    {
        if (_splitButton != null)
            _splitButton.onClick.RemoveListener(OnSplitClicked);
    }

    void OnSplitClicked()
    {
        if (NetworkManager.Instance?.Room == null || GameManager.Instance == null) return;
        if (!GameManager.Instance.TryGetPlayer(NetworkManager.Instance.Room.SessionId, out var pc)) return;
        pc.TryManualSplit();
    }
}
