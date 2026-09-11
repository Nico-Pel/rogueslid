using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace YsoCorp {
    namespace GameUtils {

        [DefaultExecutionOrder(-15)]
        public sealed class RequestManager : BaseManager {

            static string URL_PROD = "https://games-api.ysocorp.com/v";
            static string URL_DEV = "http://localhost:3000/v";
            static int VERSION = 6;

#if UNITY_EDITOR
            static bool DEV_MODE = true;
#else
            static bool DEV_MODE = false;
#endif
            private enum e_Platforms {
                None = 0,
                Ios = 1,
                Android = 2
            }

            public delegate void OnComplete(string json);

            private Dictionary<UnityWebRequest, OnComplete> _requests = new Dictionary<UnityWebRequest, OnComplete>();

            private void Awake() {
                this.ycManager.requestManager = this;
            }

            private void Start() {
                Application.RequestAdvertisingIdentifierAsync(
                    (string advertisingId, bool trackingEnabled, string error) => {
                        if (advertisingId != null && advertisingId != "") {
                            this.ycManager.dataManager.SetAdvertisingId(advertisingId);
                        }
                    }
                );
            }

            private void Update() {
                foreach (KeyValuePair<UnityWebRequest, OnComplete> request in this._requests) {
                    UnityWebRequest r = request.Key;
                    OnComplete a = request.Value;
                    if (r.isDone) {
                        if (r.result != UnityWebRequest.Result.ConnectionError && r.result != UnityWebRequest.Result.ProtocolError) {
                            a(r.downloadHandler.text);
                        } else {
                            a(null);
                            this.DebugLog("[" + r.GetType() + "][ERROR] " + r.url + " " + r.error);
                        }
                        this._requests.Remove(r);
                        return;
                    }
                }
            }

            private e_Platforms GetPlatform() {
#if UNITY_ANDROID
                return e_Platforms.Android;
#elif UNITY_IOS
                return e_Platforms.Ios;
#else
                return e_Platforms.None;
#endif
            }

            private string GetAdvertisingId() {
                return this.ycManager.dataManager.GetAdvertisingId();
            }

            internal string GetGameKey() { return this.ycManager.ycConfig.gameYcId; }
            internal string GetGameVersion() { return Application.version; }
            internal string GetGameAbTesting() { return this.ycManager.abTestingManager.GetPlayerSample(); }

            internal int GetDevicePlatform() { return (int)this.GetPlatform(); }
            internal string GetDeviceKey() { return this.ycManager.ycConfig.deviceKey; }
            internal string GetDeviceAdvertisingId() { return this.GetAdvertisingId(); }

            public static string GetUrlEmptyStatic(string path, bool prod = false) {
                string url = URL_PROD + VERSION + "/" + path;
                if (DEV_MODE && prod == false) {
                    url = URL_DEV + VERSION + "/" + path;
                }
                return url;
            }

            internal string GetUrlEmpty(string path) {
                return GetUrlEmptyStatic(path);
            }

            internal void SendPost(string uri, string data, OnComplete action = null) {
                UnityWebRequest r = new UnityWebRequest(uri, "POST");
                byte[] bodyRaw = Encoding.UTF8.GetBytes(data);
                r.uploadHandler = new UploadHandlerRaw(bodyRaw);
                r.downloadHandler = new DownloadHandlerBuffer();
                r.SetRequestHeader("Content-Type", "application/json");
                r.SendWebRequest();
                if (action != null) {
                    this._requests.Add(r, action);
                }
                this.DebugLog("[POST] " + uri + " = " + data);
            }

            private void DebugLog(object message) {
                if (this.ycManager.ycConfig.activeLogs.HasFlag(YCConfig.YCLogCategories.GURequests)) {
                    Debug.Log("[GameUtils - Request]" + message);
                }
            }

        }

    }
}
