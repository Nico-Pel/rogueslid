using UnityEngine;
using System;
using System.Collections.Generic;

namespace YsoCorp {
    namespace GameUtils {

        [DefaultExecutionOrder(-15)]
        public sealed class AnalyticsManager : BaseManager {

            private static bool SHOW_DEBUG = false;
            private static int TIME_LOAD_NB = 10;

            private class AppData {
                public string device_key;
                public int device_platform;
                public string device_advertising_id;
                public string device_model;
                public string device_os_version;
                public int device_memory_size;
                public int device_processor_count;
                public int device_processor_frequency;
                public string device_processor_type;
                public string game_key;
                public string game_version;
                public string game_ab_testing;
                public string sdk_version;
                public SessionData session;
            }

            [Serializable]
            internal class SessionData {
                public string key;
                public bool is_first;
                public bool is_consent_flow_shown;
                public int consent_flow_status;
                public LevelsData levelsData = new LevelsData();
                public int nb_start = 0;
                public int nb_win = 0;
                public int nb_lose = 0;
                public int playtime = 0;
                public int time_load;
                public int time_no_ads;
                public int time_ads;
                public int fps;
                public int fps_no_ads;
                public int fps_ads;
                public string app_tracking_status;
                public string push_notif_status;
                public TenjinData tenjin;
                public string bundleID;
                public string mmp;
                public string mediation;
                public string installerName;
                public string subplatform;
                public Dictionary<string, int> events = new Dictionary<string, int>();
                public List<CustomEvent> customEvents = new List<CustomEvent>();
                public List<GameData> games = new List<GameData>();
                public List<InAppData> inApps = new List<InAppData>();
                public List<AdData> ads_interstitial = new List<AdData>();
                public List<AdData> ads_rewarded = new List<AdData>();
                public List<AdData> ads_banner = new List<AdData>();
                public List<AdData> ads_inapp = new List<AdData>();
            }

            [Serializable]
            internal class LevelsData {
                public int nb_start = 0;
                public int nb_pause = 0;
                public int nb_unpause = 0;
                public int nb_win = 0;
                public int nb_lose = 0;
                public List<GameData> levels = new List<GameData>();
            }

            [Serializable]
            internal class CustomEvent {
                public string key;
                public int value;

                public CustomEvent(string k, int v) {
                    this.key = k;
                    this.value = v;
                }
            }

            [Serializable]
            internal class GameData {
                public int level = 0;
                public string data = "{}";
                public string status = LevelStatus.START.ToString();
                public int playtime = 0;
            }

            [Serializable]
            internal class InAppData {
                public string id;
                public float price = 0f;
                public string iso_country_code;
            }

            [Serializable]
            public class AdData {
                public float revenue;
                public string country_code;
                public string currency;
                public string network_name;
                public string ad_unit_id;
                public string network_placement;
                public string placement;
                public string creative_id;
            }

            [Serializable]
            internal class TenjinData {
                public bool is_init;
                public string advertising_id;
                public string ad_network;
                public string campaign_id;
                public string campaign_name;
                public string site_id;
                public string referrer;
                public string deferred_deeplink_url;
                public bool clicked_tenjin_link;
                public bool is_first_session;
            }

            internal enum LevelStatus {
                START,
                PAUSE,
                UNPAUSE,
                WIN,
                LOSE
            }

            private bool _onStart = false;

            private AppData _appData;
            private GameData _gameD;
            private GameData _levelData;

            private float _gamePlaytime;
            private float _levelPlaytime;
            private float _sessionPlaytime;
            private bool _skipPlaytimeDelta;

            private float _timeLoad;
            private int _timeNb;

            private float _fpsNoAdsTime;
            private int _fpsNoAdsNb;

            private float _fpsAdsTime;
            private int _fpsAdsNb;

            private void Awake() {
                this.ycManager.analyticsManager = this;
            }

            private void Start() {
                this._appData = new AppData();
                this._appData.device_key = this.ycManager.requestManager.GetDeviceKey();
                this._appData.device_advertising_id = this.ycManager.requestManager.GetDeviceAdvertisingId();
                this._appData.device_model = SystemInfo.deviceModel;
                this._appData.device_os_version = SystemInfo.operatingSystem;
                this._appData.device_memory_size = SystemInfo.systemMemorySize;
                this._appData.device_processor_count = SystemInfo.processorCount;
                this._appData.device_processor_frequency = SystemInfo.processorFrequency;
                this._appData.device_processor_type = SystemInfo.processorType;
                this._appData.device_platform = this.ycManager.requestManager.GetDevicePlatform();
                this._appData.game_key = this.ycManager.requestManager.GetGameKey();
                this._appData.game_version = this.ycManager.requestManager.GetGameVersion();
                this._appData.game_ab_testing = this.ycManager.requestManager.GetGameAbTesting();
                this._appData.sdk_version = YCConfig.VERSION;
                this.SessionStart();
                this._onStart = true;
            }

            private void Update() {
                float deltaTime = Time.unscaledDeltaTime;
                bool skipTimingDelta = this._skipPlaytimeDelta;
                if (skipTimingDelta) {
                    this._skipPlaytimeDelta = false;
                } else if (!this.ycManager.adsManager.IsInterstitialOrRewardedVisible()) {
                    this._sessionPlaytime += deltaTime;
                    if (this._gameD != null) this._gamePlaytime += deltaTime;
                    if (this._levelData != null) this._levelPlaytime += deltaTime;
                }

                if (skipTimingDelta) return;

                if (this._timeNb < TIME_LOAD_NB) {
                    this._timeLoad += deltaTime;
                    this._timeNb++;
                } else {
                    if (this.ycManager.adsManager.IsInterstitialOrRewardedVisible()) {
                        this._fpsAdsTime += deltaTime;
                        this._fpsAdsNb++;
                    } else {
                        this._fpsNoAdsTime += deltaTime;
                        this._fpsNoAdsNb++;
                    }
                }
            }

            protected override void OnApplicationQuit() {
                base.OnApplicationQuit();
                this.SessionEnd();
            }

            private string CreateGUIDV4() {
                return Guid.NewGuid().ToString();
            }

            private void OnApplicationPause(bool pause) {
                if (this._onStart == true) {
                    if (pause) {
                        this.SessionEnd();
                    } else {
                        this.SessionStart();
                        this._skipPlaytimeDelta = true;
                    }
                }
            }

            private SessionData GetSession() {
                if (this._appData.session == null) {
                    this._appData.session = new SessionData();
                    this._sessionPlaytime = 0f;
                    this.ResetPerformanceCounters();
                    this._appData.session.key = this.CreateGUIDV4();
                    this._appData.session.bundleID = Application.identifier;
                    this._appData.session.mmp = this.GetMMP();
                    this._appData.session.mediation = "APPLOVIN";
                    this._appData.session.installerName = Application.installerName;
#if UNITY_ANDROID
                    this._appData.session.subplatform = "Google";
#elif UNITY_IOS
                    this._appData.session.subplatform = "Apple";
#endif
                    if (this._gameD != null) {
                        this._appData.session.games.Add(this._gameD);
                    }
                }
                this._appData.session.tenjin = this.ycManager.mmpManager.tenjinManager.tenjinData;
                return this._appData.session;
            }

            internal void SessionStart() {
                if (this._appData.session == null) {
                    this.GetSession();
                    this.DebugSession();
                }

                if (this._levelData != null) {
                    this._levelPlaytime = 0f;
                    GameData levelData = new GameData();
                    levelData.level = this._levelData.level;
                    levelData.status = LevelStatus.UNPAUSE.ToString();
                    this.GetSession().levelsData.nb_unpause++;
                    this.GetSession().levelsData.levels.Add(levelData);
                }
            }

            private int GetPlaytime(float t) {
                return (int)(t * 1000);
            }

            private void ResetPerformanceCounters() {
                this._timeLoad = 0f;
                this._timeNb = 0;
                this._fpsNoAdsTime = 0f;
                this._fpsNoAdsNb = 0;
                this._fpsAdsTime = 0f;
                this._fpsAdsNb = 0;
            }

            private float GetFps() {
                if ((this._fpsAdsNb + this._fpsNoAdsNb) == 0 || (this._fpsAdsTime + this._fpsNoAdsTime) == 0) {
                    return 0f;
                }
                return (float)Math.Round((this._fpsAdsNb + this._fpsNoAdsNb) / (this._fpsAdsTime + this._fpsNoAdsTime));
            }

            private float GetFpsNoAds() {
                if (this._fpsNoAdsNb == 0 || this._fpsNoAdsTime == 0) {
                    return 0f;
                }
                return (float)Math.Round(this._fpsNoAdsNb / this._fpsNoAdsTime);
            }

            private float GetFpsAds() {
                if (this._fpsAdsNb == 0 || this._fpsAdsTime == 0) {
                    return 0f;
                }
                return (float)Math.Round(this._fpsAdsNb / this._fpsAdsTime);
            }

            private string GetMMP() {
                if (this.ycManager.ycConfig.MmpTenjin) return "TENJIN";
                else return "";
            }

            internal void SessionEnd() {
                if (this._appData.session != null) {
                    this._appData.device_advertising_id = this.ycManager.requestManager.GetDeviceAdvertisingId();
                    this.GetSession().is_first = this.ycManager.IsFirstTimeAppLaunched();
                    this.GetSession().is_consent_flow_shown = false;
                    this.GetSession().consent_flow_status = 0;
                    this.GetSession().playtime = this.GetPlaytime(this._sessionPlaytime);
                    this.GetSession().time_load = (int)Math.Round(this._timeLoad);
                    this.GetSession().time_ads = (int)Math.Round(this._fpsAdsTime);
                    this.GetSession().time_no_ads = (int)Math.Round(this._fpsNoAdsTime);
                    this.GetSession().fps = (int)this.GetFps();
                    this.GetSession().fps_ads = (int)this.GetFpsAds();
                    this.GetSession().fps_no_ads = (int)this.GetFpsNoAds();
                    this.GetSession().app_tracking_status = this.ycManager.adsManager.GetAppTrackingStatus();
                    this.GetSession().push_notif_status = this.ycManager.pushNotificationManager.GetStatus();
                    if (this.GetSession().games.Count > 0) {
                        GameData gameData = this.GetSession().games[^1];
                        if (gameData.playtime == 0) {
                            gameData.playtime = this.GetPlaytime(this._gamePlaytime);
                        }
                    }
                    if (this._levelData != null) {
                        GameData levelData = new GameData();
                        levelData.level = this._levelData.level;
                        levelData.status = LevelStatus.PAUSE.ToString();
                        levelData.playtime = this.GetPlaytime(this._levelPlaytime);
                        this.GetSession().levelsData.nb_pause++;
                        this.GetSession().levelsData.levels.Add(levelData);
                    }
                    this.ycManager.requestManager.SendPost(this.GetUrlEmpty("session"), this.GetAppJson());
                    this.DebugSession();
                    this._appData.session = null;
                    this._gameD = null;
                }
            }

            private string GetUrlEmpty(string action) {
                return this.ycManager.requestManager.GetUrlEmpty("analytics/" + action);
            }

            internal void OnGameStarted(int level = 1) {
                if (this._gameD != null) {
                    this._gameD.playtime = this.GetPlaytime(this._gamePlaytime);
                }
                this._gameD = new GameData();
                this._gameD.level = level;
                this._gamePlaytime = 0f;
                this._levelPlaytime = 0f;
                this.GetSession().games.Add(this._gameD);
                this.GetSession().nb_start++;

                this._levelData = new GameData();
                this._levelData.level = level;
                this.GetSession().levelsData.levels.Add(this._levelData);
                this.GetSession().levelsData.nb_start++;

                this.DebugSession();
            }

            internal void OnGameFinished(bool win, string data) {
                if (this._gameD != null) {
                    this._gameD.data = data;
                    this._gameD.playtime = this.GetPlaytime(this._gamePlaytime);
                    this.GetSession().nb_start--;
                    if (win) {
                        this._gameD.status = LevelStatus.WIN.ToString();
                        this.GetSession().nb_win++;
                    } else {
                        this._gameD.status = LevelStatus.LOSE.ToString();
                        this.GetSession().nb_lose++;
                    }
                    this._gameD = null;
                    this.DebugSession();
                }

                if (this._levelData != null) {
                    GameData levelData = new GameData();
                    levelData.level = this._levelData.level;
                    levelData.data = data;
                    levelData.playtime = this.GetPlaytime(this._levelPlaytime);
                    if (win) {
                        levelData.status = LevelStatus.WIN.ToString();
                        this.GetSession().levelsData.nb_win++;
                    } else {
                        levelData.status = LevelStatus.LOSE.ToString();
                        this.GetSession().levelsData.nb_lose++;
                    }
                    this.GetSession().levelsData.levels.Add(levelData);
                    this.DebugSession();

                    this._levelData = null;
                }
            }

            internal void InAppBought(string id, float price, string isoCountryCode) {
                this.GetSession().inApps.Add(new InAppData() {
                    id = id,
                    price = price,
                    iso_country_code = isoCountryCode
                });
            }

            private void AddEvent(string key, int amount = 1) {
                if (this.GetSession().events.ContainsKey(key)) {
                    this.GetSession().events[key] += amount;
                } else {
                    this.GetSession().events.Add(key, amount);
                }
                this.DebugSession();
            }

            /// <summary>
            /// Adds a custom event to the session.
            /// </summary>
            /// <param name="key">An int between 0 and 63 included</param>
            /// <param name="amount">The amount added</param>
            [Obsolete("GameUtils now uses strings as keys. Please use the other method overload", true)]
            public void AddCustomEvent(int key, int amount = 1) {
                return;
            }

            /// <summary>
            /// Adds a custom event to the session.
            /// </summary>
            /// <param name="key">A unique string</param>
            /// <param name="amount">The amount added</param>
            public void AddCustomEvent(string key, int amount = 1) {
                this.GetSession().customEvents.Add(new CustomEvent(key, amount));
            }


            public void AddInterstitialAdData(AdData adData) { this.GetSession().ads_interstitial.Add(adData); }
            internal void InterstitialShow() { this.AddEvent("interstitial_show"); }
            internal void InterstitialClick() { this.AddEvent("interstitial_click"); }

            public void AddRewardedAdData(AdData adData) { this.GetSession().ads_rewarded.Add(adData); }
            internal void RewardedShow() { this.AddEvent("rewarded_show"); }
            internal void RewardedClick() { this.AddEvent("rewarded_click"); }

            internal void FullscreenAdClosed() { this._skipPlaytimeDelta = true; }

            public void AddBannerAdData(AdData adData) { this.GetSession().ads_banner.Add(adData); }
            internal void BannerShow() { this.AddEvent("banner_show"); }
            internal void BannerClick() { this.AddEvent("banner_click"); }

            public void AddInappAdAdData(AdData adData) { this.GetSession().ads_inapp.Add(adData); }
            public void InappAdShow() { this.AddEvent("inapp_show"); }
            public void InappAdClick() { this.AddEvent("inapp_click"); }

            public void SendMaxEventAdRewardedOpportunity() => MaxSdkEventTracking.SendMaxEventAdRewardedOpportunity();
            public void SendMaxEventTutorialFinished() => MaxSdkEventTracking.SendMaxEventTutorialFinished();
            public void SendMaxEventVirtualResource(int value, MaxSdkEventTracking.VirtualResourceType resourceType = MaxSdkEventTracking.VirtualResourceType.NONE) => MaxSdkEventTracking.SendMaxEventVirtualResource(value, resourceType);
            public void SendMaxEventShopOpen() => MaxSdkEventTracking.SendMaxEventShopOpen();

            private string GetAppJson() {
#if YC_NEWTONSOFT
                return Newtonsoft.Json.JsonConvert.SerializeObject(this._appData);
#else
                return "";
#endif
            }

            private string GetSessionJson() {
#if YC_NEWTONSOFT
                return Newtonsoft.Json.JsonConvert.SerializeObject(this.GetSession(), Newtonsoft.Json.Formatting.Indented);
#else
                return "";
#endif
            }

            private void DebugSession() {
                if (SHOW_DEBUG) {
                    Debug.Log("DebugSession " + this.GetSessionJson());
                }
            }
        }
    }
}
