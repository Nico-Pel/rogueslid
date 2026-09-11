using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace YsoCorp {
    namespace GameUtils {

        public class SettingsWindow : MonoBehaviour {
            private static Color COLOR_ON = Color.white;
            private static Color COLOR_OFF = new Color(0.7f, 0.7f, 0.7f, 1f);

            [Header("Scene References")]
            [SerializeField] private Button bClose;
            [SerializeField] private Button bCloseBlackBG;
            [SerializeField] private Button bRestorePurchase;
            [SerializeField] private Button bDataPrivacy;
            [SerializeField] private GameObject buttonsParent;
            [SerializeField] private GameObject panelBts;
            [SerializeField] private GameObject soundEffectsParent;
            [SerializeField] private Toggle bSoundEffect;
            [SerializeField] private Slider sSoundEffect;
            [SerializeField] private GameObject soundMusicParent;
            [SerializeField] private Toggle bSoundMusic;
            [SerializeField] private Slider sSoundMusic;
            [SerializeField] private Toggle bVibration;
            [SerializeField] private Button bLang;
            [SerializeField] private Image iLang;
            [SerializeField] private Transform langsContent;
            [SerializeField] private List<LangSelector> langSelectors;
            [SerializeField] private Text tVersion;
            [SerializeField] private Button bVersion;

            [SerializeField] internal SettingsDebugWindow debugWindow;

            private float _versionLastClick;
            private YCManager ycManager;    

            private void Awake() {
                this.ycManager = YCManager.instance;
                this.gameObject.SetActive(false);
            }

            public virtual void Init() {
                this.UpdateCanvasScaler();
                this.InitLangs();
                this.InitButtons();
                this.InitVibration();
                this.InitSounds();
                this.InitVersion();

                this.panelBts.gameObject.SetActive(
                    this.soundEffectsParent.gameObject.activeSelf ||
                    this.soundMusicParent.gameObject.activeSelf ||
                    this.bVibration.gameObject.activeSelf ||
                    this.bLang.gameObject.activeSelf
                );

                this.debugWindow.Init();
            }

            private void InitButtons() {
                this.bClose.onClick.AddListener(() => {
                    this.gameObject.SetActive(false);
                });

                this.bCloseBlackBG.onClick.AddListener(() => {
                    this.gameObject.SetActive(false);
                });

#if !IN_APP_PURCHASING || UNITY_ANDROID && !UNITY_EDITOR 
                this.bRestorePurchase.gameObject.SetActive(false);
#else
                this.bRestorePurchase.gameObject.SetActive(this.ycManager.ycConfig.HasInApps());
#endif
                this.bRestorePurchase.onClick.AddListener(() => {
                    this.ycManager.inAppManager.RestorePurchases();
                });

                this.bDataPrivacy.onClick.AddListener(() => {
                    this.ycManager.adsManager.DisplayGDPR();
                });
            }

            private void InitVibration() {
                this.bVibration.gameObject.SetActive(this.ycManager.ycConfig.Vibration);
                if (this.ycManager.ycConfig.Vibration) {
                    this.bVibration.isOn = this.ycManager.dataManager.GetVibration();
                    this.bVibration.onValueChanged.AddListener((value) => {
                        this.ycManager.dataManager.SetVibration(value);
                        this.ycManager.vibrationManager.LightImpact();
                    });
                }
            }

            private void InitSounds() {
                this.soundEffectsParent.SetActive(this.ycManager.ycConfig.SoundEffect);
                if (this.ycManager.ycConfig.SoundEffect) {
                    this.bSoundEffect.isOn = this.ycManager.soundManager.AreEffectsEnabled();
                    this.sSoundEffect.value = this.ycManager.soundManager.GetEffectsVolume() * 10;
                    this.InitToggleSliderCallbacks(this.bSoundEffect, (value) => this.ycManager.soundManager.EnableEffects(value),
                                                   this.sSoundEffect, (value) => this.ycManager.soundManager.SetEffectsVolume(value / 10));
                }

                this.soundMusicParent.SetActive(this.ycManager.ycConfig.SoundMusic);
                if (this.ycManager.ycConfig.SoundMusic) {
                    this.bSoundMusic.isOn = this.ycManager.soundManager.IsMusicEnabled();
                    this.sSoundMusic.value = this.ycManager.soundManager.GetMusicVolume() * 10;
                    this.InitToggleSliderCallbacks(this.bSoundMusic, (value) => this.ycManager.soundManager.EnableMusic(value),
                                                   this.sSoundMusic, (value) => this.ycManager.soundManager.SetMusicVolume(value / 10));
                }
            }

            private void InitToggleSliderCallbacks(Toggle toggle, Action<bool> onToggle, Slider slider, Action<float> onSlider) {
                toggle.onValueChanged.AddListener((value) => {
                    onToggle?.Invoke(value);
                    if (value) {
                        slider.value = 10f;
                    } else {
                        slider.value = 0f;
                    }
                });
                slider.onValueChanged.AddListener((value) => {
                    toggle.isOn = value != 0;
                    onSlider?.Invoke(value);
                });
            }

            private void InitVersion() {
                this.tVersion.text = "v" + Application.version + "  sdk" + YCConfig.VERSION;
                if (this.ycManager.abTestingManager.GetPlayerSample() != "") {
                    this.tVersion.text += " (" + this.ycManager.abTestingManager.GetPlayerSample() + ")";
                }
                this.bVersion.onClick.AddListener(() => {
                    if (Time.unscaledTime - this._versionLastClick < 0.3f) {
                        this.debugWindow.gameObject.SetActive(true);
                    }
                    this._versionLastClick = Time.unscaledTime;
                });
            }

            private void InitLangs() {
                IReadOnlyList<string> languages = this.ycManager.i18nManager.AvailableLanguages;
                int langCount = languages.Count;
                this.bLang.gameObject.SetActive(langCount > 1);
                if (langCount > 1) {
                    this.iLang.sprite = this.ycManager.i18nManager.GetCurrentSprite();
                    this.bLang.onClick.AddListener(() => {
                        this.buttonsParent.SetActive(false);
                        this.langsContent.gameObject.SetActive(true);
                    });
                    for (int i = 0; i < languages.Count; i++) {
                        string lang = languages[i];
                        LangSelector selector = i == 0 ? this.langSelectors[0] : Instantiate(this.langSelectors[0], langsContent);
                        selector.Init(lang, () => {
                            this.iLang.sprite = this.ycManager.i18nManager.GetCurrentSprite();
                            this.buttonsParent.SetActive(true);
                            this.langsContent.gameObject.SetActive(false);
                        });
                    }
                }
            }

            private void UpdateCanvasScaler() {
                this.GetComponentInParent<CanvasScaler>().matchWidthOrHeight = (Screen.width > Screen.height) ? 0.69f : 0f;
            }

            public void Show() {
                this.gameObject.SetActive(true);
                this.buttonsParent.SetActive(true);
                this.langsContent.gameObject.SetActive(false);
            }
        }

    }
}
