using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Decides whether to show touch UI (mobile / mobile WebGL). PC / desktop WebGL hides it.
/// Optional PlayerPrefs: ForceMobileControls=1, ForcePCControls=1 (PC wins if both set).
/// </summary>
public static class MobileControlsDetector
{
    public const string PrefsForceMobile = "ForceMobileControls";
    public const string PrefsForcePC = "ForcePCControls";

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern int MobileControls_IsMobileBrowser();
#endif

    public static bool ShouldShowMobileControls()
    {
        if (PlayerPrefs.GetInt(PrefsForcePC, 0) != 0) return false;
        if (PlayerPrefs.GetInt(PrefsForceMobile, 0) != 0) return true;

        if (Application.isMobilePlatform) return true;
        if (SystemInfo.deviceType == DeviceType.Handheld) return true;

#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            if (MobileControls_IsMobileBrowser() != 0) return true;
        }
        catch
        {
            // Plugin missing in some local builds
        }
        if (Input.touchSupported && Mathf.Min(Screen.width, Screen.height) <= 900)
            return true;
#endif
        return false;
    }
}
