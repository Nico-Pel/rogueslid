using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace YsoCorp {
    namespace GameUtils {

        [DefaultExecutionOrder(-10)]
        public sealed class SettingManager : BaseManager {

            [SerializeField] private SettingsWindow defaultSettingsWindowPrefab;
            [HideInInspector] public SettingsWindow settingsWindow;

            private void Awake() {
                this.ycManager.settingManager = this;
                this.InstantiateWindow();
            }

            private void InstantiateWindow() {
                if (this.ycManager.ycConfig.overrideSettingsWindow && this.ycManager.ycConfig.newSettingsWindowPrefab != null) {
                    this.settingsWindow = Instantiate(this.ycManager.ycConfig.newSettingsWindowPrefab, this.transform);
                } else {
                    this.settingsWindow = Instantiate(this.defaultSettingsWindowPrefab, this.transform);
                }
                this.settingsWindow.Init();
            }

            /// <summary>
            /// Displays the settings window.
            /// </summary>
            public void Show() {
                this.settingsWindow.Show();
            }

        }
    }
}