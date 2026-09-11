using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace YsoCorp {
    namespace GameUtils {

        public sealed class I18nElement : BaseManager {

            private string _key;
            private bool usingTMPro = false;
            private Text _text;
            private TMP_Text _textTMPro;

            private void Awake() {
                this._text = this.GetComponent<Text>();
                if (this._text == null) {
                    this._textTMPro = this.GetComponent<TMP_Text>();
                    if (this._textTMPro != null) {
                        this._key = this._textTMPro.text;
                        this.usingTMPro = true;
                    } else {
                        Debug.LogError("Text component not found for translation");
                    }
                } else {
                    this._key = this._text.text;
                }
                this.UpdateText();
                this.ycManager.i18nManager.onChange.AddListener(this.UpdateText);
            }

            protected override void OnDestroyNotQuitting() {
                this.ycManager.i18nManager.onChange.RemoveListener(this.UpdateText);
            }

            public void UpdateText() {
                if (this.usingTMPro) {
                    this._textTMPro.text = this.ycManager.i18nManager.GetString(this._key, this.gameObject);
                } else {
                    this._text.text = this.ycManager.i18nManager.GetString(this._key, this.gameObject);
                }
            }

        }

    }
}