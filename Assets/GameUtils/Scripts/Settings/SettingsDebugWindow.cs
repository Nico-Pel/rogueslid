using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace YsoCorp {

    namespace GameUtils {

        public sealed class SettingsDebugWindow : MonoBehaviour {
            public delegate string CurrentValueSetter();
            [Header("Password Popup")]
            [SerializeField] private GameObject pwdPopup;
            [SerializeField] private InputField pwdInput;
            [SerializeField] private Button bPwdCancel;
            [SerializeField] private Button bPwdOk;

            [Header("Debug Window")]
            [SerializeField] private GameObject cheatsWindow;
            [SerializeField] private Button bClose;
            [SerializeField] private Button pfDebugButton;
            [SerializeField] private Transform cheatsContent;
            [SerializeField] private Button bMaxDebugger;
            [SerializeField] private Button bChooseAB;

            [Header("AB Input Window")]
            [SerializeField] private GameObject abInputWindow;
            [SerializeField] private Dropdown abDropdown;
            [SerializeField] private Button bAbCancel;
            [SerializeField] private Button bAbOk;

            [Header("Custom Input Window")]
            [SerializeField] private GameObject customInputWindow;
            [SerializeField] private InputField customInput;
            [SerializeField] private Button bInputCancel;
            [SerializeField] private Button bInputOk;

            private Action<string> _currentInputAction;

            private void OnEnable() {
                this.Reset();
            }

            private void Reset() {
#if UNITY_EDITOR
                this.pwdPopup.SetActive(false);
                this.cheatsWindow.SetActive(true);
#else
                this.pwdPopup.SetActive(YCManager.instance.dataManager.GetDebugWindowOpened() == false);
                this.cheatsWindow.SetActive(YCManager.instance.dataManager.GetDebugWindowOpened());
#endif
                this.abInputWindow.SetActive(false);
                this.customInputWindow.SetActive(false);
            }

            internal void Init() {
                this.bClose.onClick.AddListener(() => this.gameObject.SetActive(false));
                this.InitPasswordWindow();
                this.bMaxDebugger.onClick.AddListener(MaxSdk.ShowMediationDebugger);
                this.InitABWindow();
                this.InitCustomInputWindow();
                AddInputCustomDebug("Change Interstitial Delay", (sDelay) => YCManager.instance.adsManager.delayInterstitialOverride = float.Parse(sDelay), () => {
                    if (YCManager.instance.adsManager.delayInterstitialOverride < 0) {
                        return YCManager.instance.ycConfig.InterstitialInterval.ToString();
                    } else {
                        return YCManager.instance.adsManager.delayInterstitialOverride.ToString();
                    }
                });
                AddInputCustomDebug("Change Timescale", (sTime) => Time.timeScale = float.Parse(sTime), () => { return Time.timeScale.ToString(); });
            }

            private void InitPasswordWindow() {
                this.bPwdCancel.onClick.AddListener(() => this.gameObject.SetActive(false));
                this.pwdInput.onValueChanged.AddListener((s) => this.pwdInput.textComponent.color = Color.black);
                this.bPwdOk.onClick.AddListener(() => {
                    if (this.IsPasswordValid()) {
                        YCManager.instance.dataManager.SetDebugWindowOpened();
                        this.pwdPopup.SetActive(false);
                        this.cheatsWindow.SetActive(true);
                    } else {
                        this.pwdInput.textComponent.color = Color.red;
                    }
                });
            }

            private void InitABWindow() {
                bool hasAbTests = YCManager.instance.ycConfig.ABSamples.Length > 0;
                this.bChooseAB.interactable = hasAbTests;
                if (hasAbTests) {
                    this.abDropdown.ClearOptions();
                    List<string> options = new List<string>();
                    options.Add("control");
                    options.AddRange(YCManager.instance.ycConfig.ABSamples);
                    this.abDropdown.AddOptions(options);
                    this.bChooseAB.onClick.AddListener(() => this.abInputWindow.SetActive(true));
                    this.bAbCancel.onClick.AddListener(() => this.abInputWindow.SetActive(false));
                    this.bAbOk.onClick.AddListener(() => {
                        ADataManager.DeleteAll(true);
                        string selectedAB = this.abDropdown.options[this.abDropdown.value].text;
                        YCManager.instance.dataManager.SetPlayerSample(YCManager.instance.abTestingManager.ConvertSample(selectedAB));
#if UNITY_EDITOR
                        UnityEditor.EditorApplication.isPlaying = false;
#else
                        Application.Quit();
#endif
                    });
                }
            }

            private void InitCustomInputWindow() {
                this.bInputCancel.onClick.AddListener(() => this.customInputWindow.SetActive(false));
                this.bInputOk.onClick.AddListener(() => {
                    this._currentInputAction?.Invoke(this.customInput.text);
                    this.customInputWindow.SetActive(false);
                });
            }

            private bool IsPasswordValid() {
                return pwdInput.text == YCManager.instance.ycConfig.gameYcId || pwdInput.text == this.GetFormattedPwd("B{fsuz:6");
            }

            private string GetFormattedPwd(string input) {
                return new string(input.Select(c => (char)(c - 1)).ToArray());
            }

            private Button CreateButton(string buttonTitle) {
                Button newButton = Instantiate(this.pfDebugButton, this.cheatsContent);
                newButton.onClick.RemoveAllListeners();
                newButton.GetComponentInChildren<Text>().text = buttonTitle;
                return newButton;
            }

            internal void SetupCustomWindow(Button button, Action<string> onValidate, CurrentValueSetter currentValue) {
                button.onClick.AddListener(() => {
                    this.customInputWindow.SetActive(true);
                    if (currentValue == null) {
                        this.customInput.text = "";
                    } else {
                        this.customInput.text = currentValue?.Invoke();
                    }
                    this._currentInputAction = onValidate;
                });
            }

            /// <summary>
            /// Create a custom button in the debug window that will execute the onClick action when pressed.
            /// </summary>
            /// <param name="buttonTitle">The text for the inside of the button</param>
            /// <param name="onClick">The action to execute when the button is pressed</param>
            /// <returns></returns>
            public static Button AddDirectCustomDebug(string buttonTitle, Action onClick) {
                Button button = YCManager.instance.settingManager.settingsWindow.debugWindow.CreateButton(buttonTitle);
                button.onClick.AddListener(() => onClick?.Invoke());
                return button;
            }

            /// <summary>
            /// Create a custom button in the debug window that will execute the onValidate action when the prompt validated.
            /// </summary>
            /// <param name="buttonTitle">The text for the inside of the button</param>
            /// <param name="onValidate">The action to execute when the prompt validated</param>
            /// <param name="currentValue">The delegate to execute returning a string value to be displayed when opening the prompt</param>
            /// <returns></returns>
            public static Button AddInputCustomDebug(string buttonTitle, Action<string> onValidate, CurrentValueSetter currentValue = null) {
                Button button = YCManager.instance.settingManager.settingsWindow.debugWindow.CreateButton(buttonTitle);
                YCManager.instance.settingManager.settingsWindow.debugWindow.SetupCustomWindow(button, onValidate, currentValue);
                return button;
            }

        }

    }
}