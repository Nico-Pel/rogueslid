using UnityEngine;
using System;

namespace YsoCorp {
    namespace GameUtils {
        [DefaultExecutionOrder(-20)]
        public sealed class YCManager : BaseManager {

            public static YCManager instance;

            #region Static Field
            public bool HasGameStarted { get; private set; } = false;
            #endregion

            public YCConfig ycConfig;

            public ABTestingManager abTestingManager { get; internal set; }
            public AdsManager adsManager { get; internal set; }
            public AnalyticsManager analyticsManager { get; internal set; }
            public FBManager fbManager { get; internal set; }
            public I18nManager i18nManager { get; internal set; }
            public InAppManager inAppManager { get; internal set; }
            public MmpManager mmpManager { get; internal set; }
            public PushNotificationManager pushNotificationManager { get; internal set; }
            public RequestManager requestManager { get; internal set; }
            public SettingManager settingManager { get; internal set; }
            public SoundManager soundManager { get; internal set; }
            public VibrationManager vibrationManager { get; internal set; }
            public YCDataManager dataManager { get; internal set; }

            private int _currentLevel;
            public int currentLevel => this._currentLevel;

            private void Awake() {
                if (instance != null) {
                    DestroyImmediate(this.gameObject);
                } else {
                    instance = this;
                    this.ycManager = this;
                    DontDestroyOnLoad(this.gameObject);
                    if (IsTestBuild() == false) {
                        this.ycConfig.activeLogs = 0;
                        this.ycConfig.ABDebugLog = false;
                        this.ycConfig.InappDebug = false;
                    }
                    MaxSdkEventTracking.SendMaxEventAppOpen();
                    Debug.Log("[GameUtils] YCManager : Initialize !");
                }
            }

            private void Start() {
                this.ycManager.dataManager.IncrementPlayerLaunchCount();
                this.CheckMmpLaunchCountEvent();
            }

            private void OnApplicationPause(bool pause) {
                if (!pause) {
                    MaxSdkEventTracking.SendMaxEventAppOpen();
                }
            }

            /// <summary>
            /// Checks if the current runtime is a Test build or Editor
            /// </summary>
            /// <returns>Returns true if the current runtime is a Test build or Editor</returns>
            public static bool IsTestBuild() {
#if DEVELOPMENT_BUILD
                return true;
#else
                return Application.installMode == ApplicationInstallMode.Editor || Application.installMode == ApplicationInstallMode.DeveloperBuild || Debug.isDebugBuild;
#endif
            }

            /// <summary>
            /// Get the amount of time the game was launched.
            /// </summary>
            /// <returns>Returns the amount of time the game was launched</returns>
            public int GetPlayerLaunchCount() {
                return this.ycManager.dataManager.GetPlayerLaunchCount();
            }

            /// <summary>
            /// Gets if this is the first time the game was launched.
            /// </summary>
            /// <returns>Returns true if this is the first time the game was launched</returns>
            public bool IsFirstTimeAppLaunched() {
                return this.GetPlayerLaunchCount() == 1;
            }

            private void CheckMmpLaunchCountEvent() {
                if (this.GetPlayerLaunchCount() == 20) {
                    this.mmpManager.OnMmpsInit += () => this.mmpManager.SendEvent("20_game_launch");
                }
            }

            /// <summary>
            /// Begins gathering data for the given level. Index must be between 1 and infinity
            /// </summary>
            /// <param name="level">The level index that starts</param>
            public void OnGameStarted(int level) {
                if (this.HasGameStarted == false) {
                    this.HasGameStarted = true;
                    this.analyticsManager.OnGameStarted(level);
                    MaxSdkEventTracking.SendMaxEventLevelStart(level);
                    this.ycManager.dataManager.IncrementPlayerGameCount();
                    this._currentLevel = level;
                }
            }

            [Obsolete("Obsolete, please use OnGameFinished(bool levelComplete) or OnGameFinished<T>(bool levelComplete, T data) instead")]
            public void OnGameFinished(bool levelComplete, float score) {
                this._OnGameFinished(levelComplete, new { score });
            }

            private void _OnGameFinished(bool levelComplete, object data = null) {
                if (this.HasGameStarted == true) {
                    string json = "{}";
#if YC_NEWTONSOFT
                    if (data != null) {
                        try {
                            json = Newtonsoft.Json.JsonConvert.SerializeObject(data);
                        } catch {
                            Debug.LogError($"Could not generate Json from the data given at the end of level {this._currentLevel}");
                        }
                    }
#endif
                    this.HasGameStarted = false;
                    this.analyticsManager.OnGameFinished(levelComplete, json);
                    MaxSdkEventTracking.SendMaxEventLevelFinished(_currentLevel);
                    this.dataManager.IncrementLevelPlayed(this._currentLevel, levelComplete);
                }
            }

            /// <summary>
            /// Stops gathering data for the level previously started with OnGameStarted
            /// </summary>
            /// <param name="levelComplete">Was the level won (true) or lost (false)</param>
            public void OnGameFinished(bool levelComplete) {
                this._OnGameFinished(levelComplete, null);
            }

            /// <summary>
            /// Stops gathering data for the level previously started with OnGameStarted
            /// </summary>
            /// <param name="levelComplete">Was the level won (true) or lost (false)</param>
            /// <param name="data">The data class or struct for the level. Must be serializable</param>
            public void OnGameFinished<T>(bool levelComplete, T data) where T : class, new() {
                this._OnGameFinished(levelComplete, data);
            }

        }
    }
}
