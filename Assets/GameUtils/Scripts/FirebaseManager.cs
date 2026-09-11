using System;
using UnityEngine;
#if FIREBASE
using Firebase;
using Firebase.Analytics;
using Firebase.Extensions;
#endif

namespace YsoCorp {
    namespace GameUtils {

        [DefaultExecutionOrder(-15)]
        public sealed class FirebaseManager : BaseManager {

            public enum InitState {
                NotInit = 1,
                InitSuccessful = 2,
                InitFailed = 3
            }

            private InitState _initState;
            private event Action<InitState> _onInitTried;

#if FIREBASE
            private void Start() {
                FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread((task) => {

                    var dependencyStatus = task.Result;
                    if (dependencyStatus == Firebase.DependencyStatus.Available) {
                        FirebaseAnalytics.SetAnalyticsCollectionEnabled(true);
                        
                        this._initState = InitState.InitSuccessful;
                    } else {
                        UnityEngine.Debug.LogError($"[GameUtils-Firebase] Could not resolve all Firebase dependencies: {dependencyStatus}");
                        this._initState = InitState.InitFailed;
                    }
                    this._onInitTried?.Invoke(this.GetCurrentState());
                });
            }
#endif

            public bool IsInitialized() {
                return this._initState == InitState.InitSuccessful;
            }

            public InitState GetCurrentState() {
                return this._initState;
            }

            /// <summary>
            /// Handles both pre-init (subscribing to an OnInit event) and post-init (directly executing the given action)
            /// </summary>
            /// <param name="action">The action to execute in either case. The InitState parameter is the init state.</param>
            /// <param name="executeIfAlreadyInit">Should the action execute if the init is already done.</param>
            public void OnInitHandling(Action<InitState> action, bool executeIfAlreadyInit = true) {
                if (this._initState == 0) {
                    this._onInitTried += action;
                } else if (executeIfAlreadyInit) {
                    action?.Invoke(this.GetCurrentState());
                }
            }
        }

    }
}
