using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using YsoCorp;
using YsoCorp.GameUtils;

public static class YsoIntegrationChecks
{
    [Serializable] public class Save { public int coins; }
    static int checks;
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); checks++; Debug.Log("PASS " + name); }
    static void Set(object o, string field, object value) => o.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(o, value);
    static void Call(object o, string method, params object[] args) => o.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(o, args);
    public static void Run()
    {
        try
        {
            if (!Application.dataPath.EndsWith("/rogueslid-yso-tests/Assets"))
                throw new InvalidOperationException("Run only in the disposable rogueslid-yso-tests project; this fixture writes synthetic PlayerPrefs.");
            string key = "rogueslid.test." + Guid.NewGuid();
            PlayerPrefs.SetString(key, "{\"coins\":42}");
            Check(GameSaveStore.Load<Save>(key).coins == 42, "Legacy key read without migration");
            GameSaveStore.Save(key, new Save { coins = 70 });
            Check(GameSaveStore.Load<Save>(key).coins == 70, "Save round trip");
            PlayerPrefs.SetString(key, "{not-json");
            Check(GameSaveStore.Load<Save>(key).coins == 42, "Corrupt primary recovers last valid backup");
            Check(PlayerPrefs.GetString(key + ".unreadable") == "{not-json", "Corrupt payload retained");
            GameSaveStore.Save(key, new Save { coins = 90 });
            Check(PlayerPrefs.GetString(key + ".backup").Contains("42"), "Corruption cannot replace backup");
            GameSaveStore.Delete(key);
            Check(!PlayerPrefs.HasKey(key) && !PlayerPrefs.HasKey(key+".backup"), "Clear does not resurrect backup");

            // This runner is executed only in a disposable validation project.
            PlayerPrefs.SetString("rogueslid.character-progression", "{\"Characters\":[{\"CharacterId\":\"hector\",\"OrbCount\":19,\"Upgrades\":null}],\"UnlockedCharacterIds\":null}");
            Check(CharacterProgressionSaveManager.GetOrbCount("hector") == 19, "Legacy character progression retained");
            Check(CharacterProgressionSaveManager.GetUpgradeUnlockCount("hector", "upgrade") == 0, "Null legacy upgrade list normalized");
            CharacterProgressionSaveManager.AddOrbs("hector", 4);
            Check(CharacterProgressionSaveManager.GetOrbCount("hector") == 23, "Progression remains writable");
            PlayerPrefs.SetString("rogueslid.equipment-builds", "{\"Builds\":null}");
            Check(EquipmentBuildLibrary.GetBuilds("hector").Count == 0, "Null build collection normalized");

            var go = new GameObject("Audio checks");
            var sound = go.AddComponent<global::SoundManager>();
            Call(sound, "Awake");
            var clip = AudioClip.Create("test", 44100, 1, 44100, false);
            ADataManager.SetBool("SOUNDEFFECT", true);
            ADataManager.SetFloat("SOUNDEFFECT_VOLUME", .5f);
            var effect = sound.Play2DSound(clip, .8f, loop:true);
            Check(Mathf.Approximately(effect.Source.volume, .4f), "Effect volume multiplied by SDK preference");
            ADataManager.SetBool("SOUNDEFFECT", false);
            Call(sound, "LateUpdate");
            Check(effect.Source.mute, "Already playing effect muted by SDK setting");
            ADataManager.SetBool("SOUNDEFFECT", true);
            ADataManager.SetFloat("SOUNDEFFECT_VOLUME", .25f);
            Call(sound, "LateUpdate");
            Check(!effect.Source.mute && Mathf.Approximately(effect.Source.volume, .2f), "Effect restored without compounded volume");
            Call(sound, "OnApplicationPause", true);
            Check(effect.Source.mute, "Audio muted while application paused");
            Call(sound, "OnApplicationPause", false);
            Check(!effect.Source.mute, "Audio restored on resume");
            ADataManager.SetBool("SOUNDMUSIC", false);
            Set(sound, "victoryJingle", clip);
            sound.PlayVictoryJingle();
            var jingle = (AudioSource)sound.GetType().GetField("jingleSource", BindingFlags.NonPublic|BindingFlags.Instance).GetValue(sound);
            Check(jingle.mute && !effect.Source.mute, "Jingle follows music independently of effects");
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(clip);
            var sdkRoot = new GameObject("SDK checks");
            sdkRoot.SetActive(false);
            var sdk = sdkRoot.AddComponent<YCManager>();
            sdk.ycConfig = ScriptableObject.CreateInstance<YCConfig>();
            YCManager.instance = sdk;
            typeof(BaseManager).GetField("YCMANAGER", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, sdk);
            var sdkSound = sdkRoot.AddComponent<YsoCorp.GameUtils.SoundManager>();
            var ads = sdkRoot.AddComponent<AdsManager>();
            var analytics = sdkRoot.AddComponent<AnalyticsManager>();
            var data = sdkRoot.AddComponent<YCDataManager>();
            typeof(YCManager).GetProperty("soundManager").SetValue(sdk, sdkSound);
            typeof(YCManager).GetProperty("adsManager").SetValue(sdk, ads);
            typeof(YCManager).GetProperty("analyticsManager").SetValue(sdk, analytics);
            typeof(YCManager).GetProperty("dataManager").SetValue(sdk, data);
            var overlay = new GameObject("overlay", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            Set(ads, "iInterstitial", overlay.GetComponent<UnityEngine.UI.Image>());
            int callbacks = 0;
            Check(!ads.ShowInterstitial(() => callbacks++), "Unconfigured ad returns no fill");
            Check(callbacks == 1 && !ads.IsFullscreenAdPending, "No fill invokes callback once");
            Time.timeScale = .3f;
            Call(ads, "TimeScaleOff");
            Call(ads, "TimeScaleOff");
            Call(ads, "TimeScaleOn");
            Check(Mathf.Approximately(Time.timeScale, .3f), "Duplicate display preserves original time scale");
            Set(ads, "_interstitialPending", true);
            Set(ads, "_aInterstitial", (Action)(() => callbacks++));
            Call(ads, "CompleteInterstitial");
            Call(ads, "CompleteInterstitial");
            Check(callbacks == 2 && !ads.IsFullscreenAdPending, "Display failure or close completes once");
            Check(Mathf.Approximately(Time.timeScale, .3f), "Failure before display preserves time scale");
            bool reward = true;
            Set(ads, "_rewardedPending", true);
            Set(ads, "_aRewarded", (Action<bool>)(value => reward = value));
            Call(ads, "CompleteRewarded", false);
            Check(!reward && !ads.IsFullscreenAdPending, "Rewarded failure releases without reward");
            Time.timeScale = 1;
            YCManager.instance = null;
            UnityEngine.Object.DestroyImmediate(sdk.ycConfig);
            UnityEngine.Object.DestroyImmediate(sdkRoot);
            UnityEngine.Object.DestroyImmediate(overlay);
            Debug.Log("YSO_CHECKS_PASSED " + checks);
            EditorApplication.Exit(0);
        }
        catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
