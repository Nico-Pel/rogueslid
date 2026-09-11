using System.Collections;
using System.Collections.Generic;

using UnityEngine;

namespace YsoCorp {
    namespace GameUtils {
        public static class YCBootstrapper {

            [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
            public static void ExecuteYCManager() {
                YCConfig config = YCConfig.Create();
                if (config == null || string.IsNullOrWhiteSpace(config.gameYcId)) {
                    Debug.Log("[GameUtils] Rogueslid configuration pending; online services disabled.");
                    return;
                }
                if (YCManager.instance == null) {
                    Object.DontDestroyOnLoad(Object.Instantiate(Resources.Load("YCManager")));
                }
            }
        }
    }
}
