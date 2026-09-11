using System;
using System.Collections.Generic;
using UnityEngine;
#if YC_NEWTONSOFT
using Newtonsoft.Json;
#endif

namespace YsoCorp {
    namespace GameUtils {

        [DefaultExecutionOrder(-15)]
        public sealed class I18nResourcesManager : BaseManager {

            [SerializeField] private I18nCatalog catalog;

            private Dictionary<string, string> _currentStrings;

            internal IReadOnlyList<string> AvailableLanguages => this.catalog != null ? this.catalog.Languages : Array.Empty<string>();
            internal string CurrentLanguage { get; private set; }

            internal bool HasLanguage(string lang) {
                return this.catalog != null && this.catalog.Contains(lang);
            }

            internal bool LoadLanguage(string lang) {
                if (!this.HasLanguage(lang)) {
                    return false;
                }
                if (this.CurrentLanguage == lang && this._currentStrings != null) {
                    return true;
                }

#if YC_NEWTONSOFT
                Dictionary<string, string> strings = this.LoadStrings("I18n/" + lang);
                if (strings == null) {
                    return false;
                }

                Dictionary<string, string> baseStrings = this.LoadStrings("I18nBase/" + lang, false);
                if (baseStrings != null) {
                    foreach (KeyValuePair<string, string> entry in baseStrings) {
                        if (!strings.ContainsKey(entry.Key)) {
                            strings[entry.Key] = entry.Value;
                        }
                    }
                }

                this._currentStrings = strings;
                this.CurrentLanguage = lang;
                return true;
#else
                Debug.LogError("[YCManager.i18nManager] YC_NEWTONSOFT is required to load translations");
                return false;
#endif
            }

#if YC_NEWTONSOFT
            private Dictionary<string, string> LoadStrings(string path, bool logMissing = true) {
                TextAsset asset = AResourcesManager.Load<TextAsset>(path);
                if (asset == null) {
                    if (logMissing) {
                        Debug.LogError("[YCManager.i18nManager] Translation file not found: " + path);
                    }
                    return null;
                }

                try {
                    return JsonConvert.DeserializeObject<Dictionary<string, string>>(asset.text);
                } catch (Exception exception) {
                    Debug.LogError("[YCManager.i18nManager] Invalid translation file " + path + ": " + exception.Message);
                    return null;
                } finally {
                    Resources.UnloadAsset(asset);
                }
            }
#endif

            internal string GetString(string key, GameObject debugObject = null) {
                if (this._currentStrings != null && this._currentStrings.TryGetValue(key, out string value)) {
                    return value;
                }
                if (this._currentStrings != null && this._currentStrings.Count > 0) {
                    string error = "[YCManager.i18nManager] Couldn't find the " + this.CurrentLanguage + " translation for \"" + key + "\"";
                    if (debugObject != null) {
                        Debug.LogError(error, debugObject);
                    } else {
                        Debug.LogError(error);
                    }
                }
                return key;
            }

            internal Sprite GetSprite(string lang) {
                return AResourcesManager.Load<Sprite>("Sprites/I18n/" + lang);
            }
        }

    }
}
