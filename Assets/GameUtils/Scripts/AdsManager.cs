using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

namespace YsoCorp {
    namespace GameUtils {

        [DefaultExecutionOrder(-10)]
        public sealed class AdsManager : BaseManager {
            public enum BooleanOverride {
                NoOverride,
                True,
                False
            }

            public static string DATA_PRIVACY_URL = "https://www.ysocorp.com/privacy-policy";

            [SerializeField] private Image iInterstitial;
            [SerializeField] private Image iBanner;
            [SerializeField] private Button bRemoveAds;

            private bool _rewardedPending;
            private bool _interstitialPending;
            private bool _timeScalePaused;
            public bool IsFullscreenAdPending => this._rewardedPending || this._interstitialPending;

            private bool _rewardedVisible;
            private bool _interstitialVisible;

            private Action<bool> _aRewarded = null;
            private Action _aInterstitial = null;
            private bool _finishRewarded = false;
            private float _delayInterstitial = 0f;

            public float delayInterstitial { get { return this._delayInterstitial; } }
            [HideInInspector] public float delayInterstitialOverride = -1f;
            [HideInInspector] public BooleanOverride rewardedResetDelayOverride = BooleanOverride.NoOverride;
            [HideInInspector] public BooleanOverride useCustomFirstInterDelayOverride = BooleanOverride.NoOverride;
            [HideInInspector] public float customFirstInterDelayOverride = -1;
            private BooleanOverride _bannerTransparencyOverride = BooleanOverride.NoOverride;
            [HideInInspector]
            public BooleanOverride bannerTransparencyOverride {
                get { return this._bannerTransparencyOverride; }
                set {
                    this._bannerTransparencyOverride = value;
                    this.SetBannerTransparency();
                }
            }

#if !UNITY_EDITOR
            private bool _isEnableRewarded = false;
#endif
            private bool _isEnableInterstitial = false;
            private bool _isEnableBanner = false;

            private bool _canDisplayInterstitial = false;

            private MaxSdkBase.SdkConfiguration _sdkConfiguration;

            private float _onDisplayTimeScale;

            private Dictionary<string, double> _tooSmallRevenuesRR;

            private void Awake() {
                this.ycManager.adsManager = this;
                this.bRemoveAds.onClick.AddListener(() => {
                    this.ycManager.inAppManager.BuyProductIDAdsRemove();
                });
                this.iBanner.gameObject.SetActive(false);
                this.iInterstitial.gameObject.SetActive(false);
                this._canDisplayInterstitial = !this.ycManager.ycConfig.SkipFirstInterCall;
                if (this.GetInterstitialKey() != "" || this.GetRewardedKey() != "" || this.GetBannerKey() != "") {
                    this.InitMax();
                } else {
                    this.ycManager.ycConfig.LogWarning("[Ads] not init");
                }
            }

            private void InitMax() {
                MaxSdkCallbacks.OnSdkInitializedEvent += this.OnSdkInitializedEvent;
                MaxSdk.SetUserId(this.ycManager.ycConfig.deviceKey);
                MaxSdk.SetVerboseLogging(this.ycManager.ycConfig.activeLogs.HasFlag(YCConfig.YCLogCategories.ApplovinMax));
                MaxSdk.SetExtraParameter("disable_all_logs", (!this.ycManager.ycConfig.activeLogs.HasFlag(YCConfig.YCLogCategories.ApplovinMax)).ToString().ToLower());
                MaxSdk.InitializeSdk();
            }

            /// <summary>
            /// Check if an interstitial or a rewarded is being shown.
            /// </summary>
            /// <returns>Returns true if an interstitial or a rewarded is being shown.</returns>
            public bool IsInterstitialOrRewardedVisible() {
                return this._rewardedVisible || this._interstitialVisible;
            }

            internal string GetAppTrackingStatus() {
#if UNITY_IOS
                if (this._sdkConfiguration != null) {
                    return this._sdkConfiguration.AppTrackingStatus.ToString();
                }
#endif
                return "";
            }

            private AnalyticsManager.AdData OnAdRevenuePaidEvent(string adUnitId, MaxSdkBase.AdInfo adInfo) {
                AnalyticsManager.AdData adData = new AnalyticsManager.AdData();
                adData.revenue = (float)adInfo.Revenue;
                adData.country_code = MaxSdk.GetSdkConfiguration().CountryCode;
                adData.network_name = adInfo.NetworkName;
                adData.network_placement = adInfo.NetworkPlacement;
                adData.placement = adInfo.Placement;
                adData.ad_unit_id = adInfo.AdUnitIdentifier;
                adData.creative_id = adInfo.CreativeIdentifier;
                return adData;
            }

            private void OnSdkInitializedEvent(MaxSdkBase.SdkConfiguration sdkConfiguration) {
                this._sdkConfiguration = sdkConfiguration;
                this.ycManager.mmpManager.Init();
                this.InitAds();
            }

#if IN_APP_PURCHASING
            private void Start() {
                this.ycManager.inAppManager.AddListener(this.ycManager.ycConfig.InAppRemoveAds, () => this.BuyAdsShow());
            }
#endif

            private void Update() {
                this.bRemoveAds.gameObject.SetActive(
                    this.ycManager.ycConfig.InAppRemoveAdsCanRemoveInBanner &&
                    this.ycManager.ycConfig.InAppRemoveAds != "" &&
                    this.ycManager.dataManager.GetAdsShow()
                );

                if (this._delayInterstitial > 0f) {
                    this._delayInterstitial -= Time.unscaledDeltaTime;
                }
            }

            private void InitAds() {
                this._tooSmallRevenuesRR = this.ycManager.dataManager.GetRedRockRevenue();
                if (this._tooSmallRevenuesRR == null) this._tooSmallRevenuesRR = new Dictionary<string, double>();

                if (this.ycManager.ycConfig.UseBanners) {
#if UNITY_EDITOR
                    if (this.ycManager.ycConfig.UseBannersInEditor) {
                        this.InitializeBannerAds();
                    }
#else
                    this.InitializeBannerAds();
#endif
                }
                if (this.GetInterstitialDelay() >= 0) {
                    this.InitializeInterstitialAds();
                }
                this.InitializeRewardedAds();
            }

            private void TimeScaleOff() {
                if (this.ycManager.ycConfig.PauseGameDuringAd && !this._timeScalePaused) {
                    this._timeScalePaused = true;
                    this._onDisplayTimeScale = Time.timeScale;
                    Time.timeScale = 0f;
                }
            }

            private void TimeScaleOn() {
                if (this._timeScalePaused) {
                    this._timeScalePaused = false;
                    Time.timeScale = this._onDisplayTimeScale;
                }
            }

            public void DisplayGDPR() {
                if (MaxSdk.GetSdkConfiguration().ConsentFlowUserGeography == MaxSdkBase.ConsentFlowUserGeography.Gdpr) {
                    MaxSdk.CmpService.ShowCmpForExistingUser(error => {
                        if (error != null) {
                            Application.OpenURL(DATA_PRIVACY_URL);
                        }
                    });
                } else {
                    Application.OpenURL(DATA_PRIVACY_URL);
                }
            }

            private void OnAdRevenuePaidEventFirebase(string adUnitId, MaxSdkBase.AdInfo adInfo) {
#if FIREBASE
                string adUnitIdentifier = adInfo.AdUnitIdentifier;
                string adFormat = adInfo.AdFormat;
                string networkName = adInfo.NetworkName;
                string countryCode = MaxSdk.GetSdkConfiguration().CountryCode;
                double revenue = adInfo.Revenue;

                string dictionaryKey = adFormat + networkName + countryCode;
                bool hasKey = this._tooSmallRevenuesRR.ContainsKey(dictionaryKey);
                if (hasKey) {
                    revenue += this._tooSmallRevenuesRR[dictionaryKey];
                }

                if (revenue >= 0.0001) {
                    if (hasKey) {
                        this._tooSmallRevenuesRR.Remove(dictionaryKey);
                    }

                    var impressionParameters = new[] {
                        new Firebase.Analytics.Parameter("ad_platform", "AppLovin"),
                        new Firebase.Analytics.Parameter("ad_source", networkName),
                        new Firebase.Analytics.Parameter("ad_unit_name",adUnitIdentifier),
                        new Firebase.Analytics.Parameter("ad_format", adFormat),
                        new Firebase.Analytics.Parameter("value", revenue),
                        new Firebase.Analytics.Parameter("currency", "USD")
                    };
                    Firebase.Analytics.FirebaseAnalytics.LogEvent("ad_impression", impressionParameters);
                } else {
                    this._tooSmallRevenuesRR[dictionaryKey] = revenue;
                }
                this.ycManager.dataManager.SetRedRockRevenue(this._tooSmallRevenuesRR);
#endif
            }

            // REWARDED

            private void InitializeRewardedAds() {
                if (this.GetRewardedKey() != "") {
#if !UNITY_EDITOR
                    this._isEnableRewarded = true;
#endif

                    MaxSdkCallbacks.Rewarded.OnAdLoadedEvent += (string adUnitId, MaxSdkBase.AdInfo adInfo) => { };
                    MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent += (string adUnitId, MaxSdkBase.Reward reward, MaxSdkBase.AdInfo adInfo) => {
                        if (this._rewardedPending) this._finishRewarded = true;
                    };
                    MaxSdkCallbacks.Rewarded.OnAdHiddenEvent += (string adUnitId, MaxSdkBase.AdInfo adInfo) => {
                        this.CompleteRewarded(this._finishRewarded);
                    };
                    MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent += (string adUnitId, MaxSdkBase.ErrorInfo errorInfo) => {
                        this.Invoke("LoadRewardedAd", 2f);
                    };
                    MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent += (string adUnitId, MaxSdkBase.ErrorInfo errorInfo, MaxSdkBase.AdInfo adInfo) => {
                        this.CompleteRewarded(false);
                    };
                    MaxSdkCallbacks.Rewarded.OnAdDisplayedEvent += (string adUnitId, MaxSdkBase.AdInfo adInfo) => {
                        if (!this._rewardedPending || this._rewardedVisible) return;
                        this._rewardedVisible = true;
                        this.TimeScaleOff();
                        this.ycManager.analyticsManager.RewardedShow();
                        MaxSdkEventTracking.SendMaxEventAdView();
                        this.ycManager.dataManager.IncrementRewardedsNb();
                    };
                    MaxSdkCallbacks.Rewarded.OnAdClickedEvent += (string adUnitId, MaxSdkBase.AdInfo adInfo) => {
                        MaxSdkEventTracking.SendMaxEventAdClicked();
                        this.ycManager.analyticsManager.RewardedClick();
                    };
                    MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent += (string adUnitId, MaxSdkBase.AdInfo adInfo) => {
                        this.ycManager.analyticsManager.AddRewardedAdData(this.OnAdRevenuePaidEvent(adUnitId, adInfo));
                    };
                    MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent += this.OnAdRevenuePaidEventFirebase;
                    this.LoadRewardedAd();
                }
            }

            private void LoadRewardedAd() {
                MaxSdk.LoadRewardedAd(this.GetRewardedKey());
            }

            /// <summary>
            /// Gets if a rewarded ad is loaded and ready to be displayed.
            /// </summary>
            /// <returns>Returns true if an ad is ready.</returns>
            public bool IsRewardedAdReady() {
#if UNITY_EDITOR
                return true;
#else
                return this._isEnableRewarded == true && MaxSdk.IsRewardedAdReady(this.GetRewardedKey());
#endif
            }

            [Obsolete("This will be removed in future releases, please use IsRewardedAdReady instead.", true)] // 1.51.0
            public bool IsRewardBasedVideo() {
                return this.IsRewardedAdReady();
            }

            /// <summary>
            /// Shows a rewarded ad if one is ready to be displayed.
            /// </summary>
            /// <param name="action">The action to be called when the ad closes</param>
            /// <returns>Returns true if an ad was successfully displayed.</returns>
            public bool ShowRewarded(Action<bool> action) {
                if (this.IsFullscreenAdPending) return false;
                if (this.IsRewardedAdReady()) {
                    this._aRewarded = action;
                    this._rewardedPending = true;
                    this.ycManager.soundManager.PauseMusic();
                    this._finishRewarded = false;
                    MaxSdk.ShowRewardedAd(this.GetRewardedKey());
                    return true;
                }
                action?.Invoke(false);
                return false;
            }


            // INTERSTITIAL

            private float GetInterstitialDelay() {
                if (this.ShouldUseFirstInterDelay()) {
                    if (this.customFirstInterDelayOverride >= 0) {
                        return this.customFirstInterDelayOverride;
                    } else {
                        return this.ycManager.ycConfig.firstInterstitialDelay;
                    }
                } else if (this.delayInterstitialOverride >= 0) {
                    return this.delayInterstitialOverride;
                } else {
                    return this.ycManager.ycConfig.InterstitialInterval;
                }
            }

            private bool ShouldUseFirstInterDelay() {
                return this.ycManager.IsFirstTimeAppLaunched() 
                    && this.ycManager.dataManager.GetInterstitialsNb() == 0 
                    && (this.ycManager.ycConfig.useCustomFirstInterDelay || this.useCustomFirstInterDelayOverride == BooleanOverride.True) 
                    && (this.ycManager.ycConfig.firstInterstitialDelay > 0 || this.customFirstInterDelayOverride >= 0);
            }

            /// <summary>
            /// Resets the interstitial delay.
            /// </summary>
            public void ResetInterstitialDelay() {
                this._delayInterstitial = this.GetInterstitialDelay();
            }

            private void InitializeInterstitialAds() {
                if (this.GetInterstitialKey() != "") {
                    this._isEnableInterstitial = true;
                    this.ResetInterstitialDelay();
                    MaxSdkCallbacks.Interstitial.OnAdLoadedEvent += (string adUnitId, MaxSdkBase.AdInfo adInfo) => { };
                    MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent += (string adUnitId, MaxSdkBase.ErrorInfo errorInfo) => {
                        this.Invoke("LoadInterstitial", 2f);
                    };
                    MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent += (string adUnitId, MaxSdkBase.ErrorInfo errorInfo, MaxSdkBase.AdInfo adInfo) => {
                        this.CompleteInterstitial();
                    };
                    MaxSdkCallbacks.Interstitial.OnAdHiddenEvent += (string adUnitId, MaxSdkBase.AdInfo adInfo) => {
                        this.CompleteInterstitial();
                    };
                    MaxSdkCallbacks.Interstitial.OnAdDisplayedEvent += (string adUnitId, MaxSdkBase.AdInfo adInfo) => {
                        if (!this._interstitialPending || this._interstitialVisible) return;
                        this._interstitialVisible = true;
                        this.TimeScaleOff();
                        this.ycManager.analyticsManager.InterstitialShow();
                        MaxSdkEventTracking.SendMaxEventAdView();
                        this.ycManager.dataManager.IncrementInterstitialsNb();
                    };
                    MaxSdkCallbacks.Interstitial.OnAdClickedEvent += (string adUnitId, MaxSdkBase.AdInfo adInfo) => {
                        MaxSdkEventTracking.SendMaxEventAdClicked();
                        this.ycManager.analyticsManager.InterstitialClick();
                    };
                    MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent += (string adUnitId, MaxSdkBase.AdInfo adInfo) => {
                        this.ycManager.analyticsManager.AddInterstitialAdData(this.OnAdRevenuePaidEvent(adUnitId, adInfo));
                    };
                    MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent += this.OnAdRevenuePaidEventFirebase;
                    this.LoadInterstitial();
                }
            }

            // Both close and display failure must release the game, exactly once.
            private void CompleteInterstitial() {
                if (!this._interstitialPending) return;
                this._interstitialPending = false;
                this._interstitialVisible = false;
                Action callback = this._aInterstitial;
                this._aInterstitial = null;
                this.ycManager.analyticsManager.FullscreenAdClosed();
                this.TimeScaleOn();
                this.ycManager.soundManager.UnPauseMusic();
                this.iInterstitial.gameObject.SetActive(false);
                this.ResetInterstitialDelay();
                this.LoadInterstitial();
                try { callback?.Invoke(); }
                catch (Exception e) { this.DebugLogError("Error during interstitial end callback: " + e.Message); }
            }

            private void CompleteRewarded(bool rewarded) {
                if (!this._rewardedPending) return;
                this._rewardedPending = false;
                this._rewardedVisible = false;
                Action<bool> callback = this._aRewarded;
                this._aRewarded = null;
                this.ycManager.analyticsManager.FullscreenAdClosed();
                this.TimeScaleOn();
                this.ycManager.soundManager.UnPauseMusic();
                if (((this.ycManager.ycConfig.rewardedResetDelay && this.rewardedResetDelayOverride != BooleanOverride.False)
                    || this.rewardedResetDelayOverride == BooleanOverride.True) && !this.ShouldUseFirstInterDelay()) {
                    this.ResetInterstitialDelay();
                }
                this.LoadRewardedAd();
                try { callback?.Invoke(rewarded); }
                catch (Exception e) { this.DebugLogError("Error during rewarded end callback: " + e.Message); }
            }

            private void LoadInterstitial() {
                MaxSdk.LoadInterstitial(this.GetInterstitialKey());
            }

            /// <summary>
            /// Gets if an interstitial ad is loaded and ready to be displayed, and if the delay is respected
            /// </summary>
            /// <param name="ignoreDelay">Set to true to ignore the delay</param>
            /// <returns>Returns true if an ad is ready.</returns>
            public bool IsInterstitialAdReady(bool ignoreDelay = false) {
                bool cond = this._canDisplayInterstitial && this.IsAdsShow() && this._isEnableInterstitial && (this._delayInterstitial <= 0f || ignoreDelay == true);
#if !UNITY_EDITOR
                if (cond) {
                    return MaxSdk.IsInterstitialReady(this.GetInterstitialKey());
                }
#endif
                return cond;
            }

            private void _ShowInterstitial() {
                MaxSdk.ShowInterstitial(this.GetInterstitialKey());
            }

            /// <summary>
            /// Shows an interstitial ad if one is ready to be displayed.
            /// </summary>
            /// <param name="action">The action to be called when the ad closes</param>
            /// <param name="delay">An internal delay after which the ad must be shown</param>
            /// <param name="ignoreDelay">Set to true to ignore the global interstitial delay</param>
            /// <returns>Returns true if an ad was successfully displayed.</returns>
            public bool ShowInterstitial(Action action = null, float delay = -1f, bool ignoreDelay = false) {
                if (this.IsFullscreenAdPending) return false;
                if (this.IsInterstitialAdReady(ignoreDelay)) {
                    this._interstitialPending = true;
                    this.iInterstitial.gameObject.SetActive(delay >= 0f);
                    this.ycManager.soundManager.PauseMusic();
                    this._aInterstitial = action;
                    if (delay <= 0f) {
                        this._ShowInterstitial();
                    } else {
                        this.Invoke("_ShowInterstitial", delay);
                    }
                    return true;
                }
                this._canDisplayInterstitial = true;
                action?.Invoke();
                return false;
            }

            // BANNER

            private void SetBannerTransparency() {
                bool baseValue = this.ycManager.ycConfig.TransparentBannerBackground;
                bool setClear = (baseValue && this.bannerTransparencyOverride != BooleanOverride.False) || this.bannerTransparencyOverride == BooleanOverride.True;
                this.iBanner.color = setClear ? Color.clear : Color.white;
            }

            private void InitializeBannerAds() {
                this.SetBannerTransparency();
                this.iBanner.color = this.ycManager.ycConfig.TransparentBannerBackground ? Color.clear : Color.white;
                if (this.GetBannerKey() != "") {
                    this._isEnableBanner = true;
                    this.CreateMaxBanner();
                    MaxSdkCallbacks.Banner.OnAdClickedEvent += (string adUnitId, MaxSdkBase.AdInfo adInfo) => {
                        MaxSdkEventTracking.SendMaxEventAdClicked();
                        this.ycManager.analyticsManager.BannerClick();
                    };
                    MaxSdkCallbacks.Banner.OnAdLoadedEvent += (string adUnitId, MaxSdkBase.AdInfo adInfo) => {
                        MaxSdkEventTracking.SendMaxEventAdView();
                        this.ycManager.analyticsManager.BannerShow();
                    };
                    MaxSdkCallbacks.Banner.OnAdRevenuePaidEvent += (string adUnitId, MaxSdkBase.AdInfo adInfo) => {
                        this.ycManager.analyticsManager.AddBannerAdData(this.OnAdRevenuePaidEvent(adUnitId, adInfo));
                    };
                    
                    this.HideBanner(!this.ycManager.ycConfig.displayBannerOnStart);

                    MaxSdkCallbacks.Banner.OnAdRevenuePaidEvent += this.OnAdRevenuePaidEventFirebase;
                }
            }

            private void CreateMaxBanner() {
                var adViewConfiguration = new MaxSdk.AdViewConfiguration(MaxSdkBase.AdViewPosition.BottomCenter) {
                    IsAdaptive = false,
                };
                MaxSdk.CreateBanner(this.GetBannerKey(), adViewConfiguration);
                MaxSdk.SetBannerPlacement(this.GetBannerKey(), "MY_BANNER_PLACEMENT");
            }

            /// <summary>
            /// Hides or displays the banner
            /// </summary>
            /// <param name="hide">Set to true to hide the banner</param>
            public void HideBanner(bool hide) {
                if (this._isEnableBanner == true) {
                    if (this.IsAdsShow()) {
                        if (hide == true) {
                            MaxSdk.HideBanner(this.GetBannerKey());
                        } else {
                            MaxSdk.ShowBanner(this.GetBannerKey());
                        }
                        this.iBanner.gameObject.SetActive(!hide);
                    } else {
                        MaxSdk.HideBanner(this.GetBannerKey());
                        this.iBanner.gameObject.SetActive(false);
                    }
                } else {
                    this.ycManager.ycConfig.LogWarning("[Ads] not init");
                }
            }

            // ADS
            /// <summary>
            /// Check if the interstitials or banners can be displayed.
            /// </summary>
            /// <returns>Returns true if the ads can be displayed.</returns>
            public bool IsAdsShow() {
                return this.ycManager.dataManager.GetAdsShow();
            }

            /// <summary>
            /// Permanently disables the interstitials and banners.
            /// </summary>
            public void BuyAdsShow() {
                this.ycManager.dataManager.BuyAdsShow();
                this.HideBanner(true);
            }

            private string GetRewardedKey() {
#if UNITY_ANDROID
                return this.ycManager.ycConfig.AndroidRewarded;
#elif UNITY_IOS || UNITY_IPHONE
                return this.ycManager.ycConfig.IosRewarded;
#else
                return "";
#endif
            }

            private string GetInterstitialKey() {
#if UNITY_ANDROID
                return this.ycManager.ycConfig.AndroidInterstitial;
#elif UNITY_IOS || UNITY_IPHONE
                return this.ycManager.ycConfig.IosInterstitial;
#else
                return "";
#endif
            }

            private string GetBannerKey() {
#if UNITY_ANDROID
                return this.ycManager.ycConfig.AndroidBanner;
#elif UNITY_IOS || UNITY_IPHONE
                return this.ycManager.ycConfig.IosBanner;
#else
                return "";
#endif
            }

            private void DebugLogError(object message) {
                Debug.LogError("[GameUtils - Ads] " + message);
            }
        }
    }
}
