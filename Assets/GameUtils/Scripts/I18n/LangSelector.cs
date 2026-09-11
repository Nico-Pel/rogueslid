using System;
using UnityEngine;
using UnityEngine.UI;

namespace YsoCorp {
    namespace GameUtils {

        public class LangSelector : MonoBehaviour {
            [SerializeField] private Button bLang;
            [SerializeField] private Image icon;

            private string _lang;

            public void Init(string lang, Action onChoose) {
                this._lang = lang;
                this.icon.sprite = YCManager.instance.i18nManager.GetLangSprite(this._lang);
                this.bLang.onClick.RemoveAllListeners();
                this.bLang.onClick.AddListener(() => {
                    YCManager.instance.i18nManager.SetLanguage(this._lang);
                    onChoose?.Invoke();
                });
            }
        }
    }
}
