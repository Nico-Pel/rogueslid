using UnityEngine;
#if YC_FACEBOOK
using Facebook.Unity;
#endif
using System.Runtime.InteropServices;

namespace YsoCorp {

    namespace GameUtils {
        [DefaultExecutionOrder(-15)]
        public sealed class FBManager : BaseManager {

            private bool _isEnable = false;

            private void Awake() {
                this.ycManager.fbManager = this;
#if YC_FACEBOOK
                if (this.ycManager.ycConfig.FbAppId != "") {
                    this._isEnable = true;
                    if (!FB.IsInitialized) {
                        FB.Init(this.InitCallback, this.OnHideUnity);
                    } else {
                        this.InitCallback();
                    }
                } else {
                    this.ycManager.ycConfig.LogWarning("[Facebook] not init");
                }
#endif
            }

            private void InitCallback() {
#if YC_FACEBOOK
                if (this._isEnable == true) {
                    if (FB.IsInitialized) {
                        FB.ActivateApp();
#if UNITY_IOS
                        FB.Mobile.SetAdvertiserTrackingEnabled(true);
#endif
                    } else {
                        Debug.Log("Failed to Initialize the Facebook SDK");
                    }
                }
#endif
            }

            private void OnHideUnity(bool isGameShown) {
                if (this._isEnable == true) {
                    if (!isGameShown) {
                        Time.timeScale = 0;
                    } else {
                        Time.timeScale = 1;
                    }
                }
            }
        }
    }

}
