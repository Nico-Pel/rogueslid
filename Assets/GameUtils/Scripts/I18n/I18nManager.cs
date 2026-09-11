using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

namespace YsoCorp {
    namespace GameUtils {

        [DefaultExecutionOrder(-14)]
        public sealed class I18nManager : BaseManager {
            
            [SerializeField] internal I18nResourcesManager i18NResourcesManager;

            [HideInInspector] public UnityEvent onChange = new UnityEvent();

            public IReadOnlyList<string> AvailableLanguages => this.i18NResourcesManager.AvailableLanguages;

            private void Awake() {
                this.ycManager.i18nManager = this;
                this.InitLanguage();
            }

            /// <summary>
            /// Get the translated string for the given string
            /// </summary>
            /// <param name="key">The string to translate</param>
            /// <param name="debugObject">The object to highlight in case of error</param>
            /// <returns>Returns the translated string</returns>
            public string GetString(string key, GameObject debugObject = null) {
                string value = this.i18NResourcesManager.GetString(key, debugObject);
                if (value != null && value != "") {
                    return value;
                }
                return key;
            }

            public Sprite GetLangSprite(string lang) {
                return this.i18NResourcesManager.GetSprite(lang);
            }

            public Sprite GetCurrentSprite() {
                return this.GetLangSprite(this.GetLanguage());
            }

            public string GetLanguage() {
                return this.i18NResourcesManager.CurrentLanguage;
            }

            public void SetLanguage(string lang) {
                if (!this.i18NResourcesManager.LoadLanguage(lang)) {
                    Debug.LogError("[YCManager.i18nManager] Language is not available: " + lang);
                    return;
                }
                this.ycManager.dataManager.SetLanguage(lang);
                this.onChange.Invoke();
            }

            private void InitLanguage() {
                string res = this.ycManager.dataManager.GetLanguage();
                if (this.ycManager.dataManager.HasLanguage() == false) {
                    switch (Application.systemLanguage) {
                    case SystemLanguage.Afrikaans: res = "AF"; break;
                    case SystemLanguage.Arabic: res = "AR"; break;
                    case SystemLanguage.Basque: res = "ES"; break; // EU
                    case SystemLanguage.Belarusian: res = "RU"; break; // BY
                    case SystemLanguage.Bulgarian: res = "BG"; break;
                    case SystemLanguage.Catalan: res = "ES"; break; // CA
                    case SystemLanguage.Chinese: res = "ZH"; break;
                    case SystemLanguage.ChineseSimplified: res = "ZH"; break;
                    case SystemLanguage.ChineseTraditional: res = "ZH"; break;
                    case SystemLanguage.Czech: res = "CS"; break;
                    case SystemLanguage.Danish: res = "DA"; break;
                    case SystemLanguage.Dutch: res = "NL"; break;
                    case SystemLanguage.English: res = "EN"; break;
                    case SystemLanguage.Estonian: res = "ET"; break;
                    case SystemLanguage.Faroese: res = "FO"; break;
                    case SystemLanguage.Finnish: res = "FI"; break;
                    case SystemLanguage.French: res = "FR"; break;
                    case SystemLanguage.German: res = "DE"; break;
                    case SystemLanguage.Greek: res = "EL"; break;
                    case SystemLanguage.Hebrew: res = "IW"; break;
                    case SystemLanguage.Hungarian: res = "HU"; break;
                    case SystemLanguage.Icelandic: res = "IS"; break;
                    case SystemLanguage.Indonesian: res = "IN"; break;
                    case SystemLanguage.Italian: res = "IT"; break;
                    case SystemLanguage.Japanese: res = "JA"; break;
                    case SystemLanguage.Korean: res = "KO"; break;
                    case SystemLanguage.Latvian: res = "LV"; break;
                    case SystemLanguage.Lithuanian: res = "LT"; break;
                    case SystemLanguage.Norwegian: res = "NO"; break;
                    case SystemLanguage.Polish: res = "PL"; break;
                    case SystemLanguage.Portuguese: res = "PT"; break;
                    case SystemLanguage.Romanian: res = "RO"; break;
                    case SystemLanguage.Russian: res = "RU"; break;
                    case SystemLanguage.SerboCroatian: res = "SH"; break;
                    case SystemLanguage.Slovak: res = "SK"; break;
                    case SystemLanguage.Slovenian: res = "SL"; break;
                    case SystemLanguage.Spanish: res = "ES"; break;
                    case SystemLanguage.Swedish: res = "SV"; break;
                    case SystemLanguage.Thai: res = "TH"; break;
                    case SystemLanguage.Turkish: res = "TR"; break;
                    case SystemLanguage.Ukrainian: res = "RU"; break;
                    case SystemLanguage.Unknown: res = "EN"; break;
                    case SystemLanguage.Vietnamese: res = "VI"; break;
					}
                }

                if (!this.i18NResourcesManager.HasLanguage(res)) {
                    res = this.i18NResourcesManager.HasLanguage("EN")
                        ? "EN"
                        : (this.AvailableLanguages.Count > 0 ? this.AvailableLanguages[0] : null);
                }

                if (!string.IsNullOrEmpty(res)) {
                    this.SetLanguage(res);
                }
            }

        }

    }
}
