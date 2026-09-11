using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace YsoCorp {
    namespace GameUtils {

        public class YCRatePopup : BaseManager {

            [System.Serializable]
            protected struct StarButton {
                public Image image;
                public Button button;
            }

            [Header("Scene References")]
            [SerializeField] protected GameObject _starsWindow;
            [SerializeField] protected StarButton[] _bStars;
            [SerializeField] protected Button _bStarClose;
            [SerializeField] protected GameObject _improvementWindow;
            [SerializeField] protected Button _bImprovementSubmit;

            [Header("Resources")]
            [SerializeField] protected Sprite _iEmptyStar;
            [SerializeField] protected Sprite _iStar;

            [Header("Configuration")]
            [Range(0, 5)] public int initialValue;
            [Range(1, 5)] public int minValueForRedirect;
            public bool showOnce;
            public UnityEvent<bool> onComplete = new UnityEvent<bool>();

            protected virtual void Awake() {
                this.InitPopup();
                this.InitButtons();
            }

            public virtual void InitPopup(int initialValue = -1) {
                this._starsWindow.SetActive(true);
                this._improvementWindow.SetActive(false);
                if (initialValue > -1) {
                    this.SelectStar(initialValue, false);
                } else {
                    this.SelectStar(this.initialValue, false);
                }
            }

            protected virtual void InitButtons() {
                this._bStarClose.onClick.AddListener(() => {
                    this.gameObject.SetActive(false);
                    this.onComplete?.Invoke(false);
                });

                this._bImprovementSubmit.onClick.AddListener(() => {
                    this.gameObject.SetActive(false);
                    this.Submit();
                });

                for (int i = 0; i < this._bStars.Length; i++) {
                    int index = i;
                    this._bStars[i].button.onClick.AddListener(() => {
                        this.SelectStar(index);
                    });
                }
            }

            public virtual void Show(int initialValue = -1) {
                if (this.GetRatePopupDone() || (this.showOnce && this.GetRatePopupShown())) {
                    return;
                }

                this.gameObject.SetActive(true);
                this.InitPopup(initialValue);
                this.SetRatePopupShown(true);
            }

            protected virtual void SelectStar(int index, bool submit = true) {
                for (int i = 0; i < this._bStars.Length; i++) {
                    this._bStars[i].image.sprite = i <= index ? this._iStar : this._iEmptyStar;
                }
                if (submit) {
                    if (index < this.minValueForRedirect - 1) {
                        this._improvementWindow.SetActive(true);
                        this._starsWindow.SetActive(false);
                    } else {
                        this.Submit();
                        this.gameObject.SetActive(false);
                        this.OpenStorePage();
                    }
                }
            }

            protected virtual void Submit() {
                this._starsWindow.SetActive(false);
                this.onComplete?.Invoke(true);
                this.SetRatePopupDone(true);
            }

            protected virtual void OpenStorePage() {
#if UNITY_EDITOR && UNITY_ANDROID
                Application.OpenURL("https://play.google.com/store/apps/details?id=" + Application.identifier);
#elif UNITY_ANDROID
                Application.OpenURL("market://details?id=" + Application.identifier);
#elif UNITY_IOS
                Application.OpenURL("https://apps.apple.com/us/app/name/id" + this.ycManager.ycConfig.appleId);
#endif
            }

            //DATA MANAGER
            private static string RATE_POPUP_SHOWN = "RATE_POPUP_SHOWN";
            private static string RATE_POPUP_DONE = "RATE_POPUP_DONE";

            public void SetRatePopupShown(bool shown) {
                ADataManager.SetBool(RATE_POPUP_SHOWN, shown);
            }
            public bool GetRatePopupShown() {
                return ADataManager.GetBool(RATE_POPUP_SHOWN);
            }

            public void SetRatePopupDone(bool done) {
                ADataManager.SetBool(RATE_POPUP_DONE, done);
            }
            public bool GetRatePopupDone() {
                return ADataManager.GetBool(RATE_POPUP_DONE);
            }
        }
    }
}
