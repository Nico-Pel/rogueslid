using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using YsoCorp.GameUtils;

public static class GameUtilsIntegrationValidator
{
    [MenuItem("Tools/Rogue Slide/YSO/Check Configuration")]
    public static void CheckConfiguration()
    {
        var config = YCConfig.Create();
        var issues = new List<string>();
        if (config == null)
        {
            Debug.LogError("[Rogue Slide][YSO] Missing Resources/YCConfigData.");
            return;
        }

        if (string.IsNullOrWhiteSpace(config.gameYcId)) issues.Add("Create/select Rogue Slide in GameUtils, then import its game ID and configuration.");
        var prefab = Resources.Load<GameObject>("YCManager");
        if (prefab == null || prefab.GetComponent<YCManager>()?.ycConfig != config)
            issues.Add("YCManager prefab must reference this YCConfigData.");
        if (!config.SoundEffect || !config.SoundMusic) issues.Add("Enable SoundEffect and SoundMusic for the game's audio settings.");
        if (string.IsNullOrWhiteSpace(config.AndroidInterstitial)) issues.Add("Android interstitial ad unit is missing.");
        if (string.IsNullOrWhiteSpace(config.IosInterstitial)) issues.Add("iOS interstitial ad unit is missing (required if releasing on iOS).");
        if (string.IsNullOrWhiteSpace(config.AdMobAndroidAppId)) issues.Add("Android AdMob app ID is missing; confirm mediation requirements with YSO.");
        if (string.IsNullOrWhiteSpace(config.AdMobIosAppId)) issues.Add("iOS AdMob app ID is missing; confirm mediation requirements with YSO.");
        if (string.IsNullOrWhiteSpace(AppLovinSettings.Instance.SdkKey)) issues.Add("MAX SDK key is missing in AppLovinSettings.");

        Debug.Log("[Rogue Slide][YSO] Android bundle: " + PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android)
            + "\niOS bundle: " + PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.iOS)
            + "\nLocal saves retain their original keys. GameUtils analytics does not upload or restore player progression.");
        if (issues.Count == 0)
            Debug.Log("[Rogue Slide][YSO] Local configuration checks passed. Verify consent, ads and analytics on a device before release.");
        else
            Debug.LogWarning("[Rogue Slide][YSO] Configuration pending:\n- " + string.Join("\n- ", issues));
        Selection.activeObject = config;
    }
}
