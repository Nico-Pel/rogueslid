using UnityEngine;
using Lofelt.NiceVibrations;
using System.Collections.Generic;

namespace YsoCorp {

    namespace GameUtils {

        [DefaultExecutionOrder(-15)]
        public sealed class VibrationManager : BaseManager {

            private Dictionary<string, float> _vibrations = new Dictionary<string, float>();

            private void Awake() {
                this.ycManager.vibrationManager = this;
            }

            private void _Vibration(HapticPatterns.PresetType type, float delta, string key, float amplitude, float frequency, float duration) {
                this._vibrations[key] = Time.time + delta;
                if (type == HapticPatterns.PresetType.None) {
                    HapticPatterns.PlayConstant(amplitude, frequency, duration);
                } else {
                    HapticPatterns.PlayPreset(type);

                }
#if UNITY_EDITOR
                if (this.ycManager.ycConfig.VibrationDebugLog) {
                    Debug.Log("[VIBRATION] " + type + " " + key);
                }
#endif
            }

            private void _VibrationCheck(HapticPatterns.PresetType type, float delta, string key, float amplitude = 0, float frequency = 0, float duration = 0) {
                if (this.ycManager.ycConfig.Vibration && this.ycManager.dataManager.GetVibration() == true) {
                    if (this._vibrations.ContainsKey(key)) {
                        if (Time.time >= this._vibrations[key]) {
                            this._Vibration(type, delta, key, amplitude, frequency, duration);
                        }
                    } else {
                        this._Vibration(type, delta, key, amplitude, frequency, duration);
                    }
                }
            }

            /// <summary>
            /// Play a single custom vibration.
            /// </summary>
            /// <param name="amplitude">Amplitude, from 0.0 to 1.0</param>
            /// <param name="frequency">Frequency, from 0.0 to 1.0</param>
            /// <param name="duration">Play duration in seconds</param>
            /// <param name="delta">The delay between each similar vibration call</param>
            /// <param name="key">A unique key to differentiate 2 vibrations with the same strength</param>
            public void CustomVibration(float amplitude, float frequency, float duration, float delta = 0.03f, string key = "") {
                this._VibrationCheck(HapticPatterns.PresetType.None, delta, key, amplitude, frequency, duration);
            }

            /// <summary>
            /// Triggers a vibration: (16%, short duration)
            /// </summary>
            /// <param name="delta">The delay between each similar vibration call</param>
            /// <param name="key">A unique key to differentiate 2 vibrations with the same strength</param>
            public void SoftImpact(float delta = 0.03f, string key = "") {
                this._VibrationCheck(HapticPatterns.PresetType.SoftImpact, delta, "SoftImpact-" + key);
            }

            /// <summary>
            /// Triggers a vibration. (16% / long duration)
            /// </summary>
            /// <param name="delta">The delay between each similar vibration call</param>
            /// <param name="key">A unique key to differentiate 2 vibrations with the same strength</param>
            public void LightImpact(float delta = 0.03f, string key = "") {
                this._VibrationCheck(HapticPatterns.PresetType.LightImpact, delta, "LightImpact-" + key);
            }

            /// <summary>
            /// Triggers a vibration. (47% / medium duration)
            /// </summary>
            /// <param name="delta">The delay between each similar vibration call</param>
            /// <param name="key">A unique key to differentiate 2 vibrations with the same strength</param>
            public void MediumImpact(float delta = 0.03f, string key = "") {
                this._VibrationCheck(HapticPatterns.PresetType.MediumImpact, delta, "MediumImpact-" + key);
            }

            /// <summary>
            /// Triggers a vibration. (100% / long duration)
            /// </summary>
            /// <param name="delta">The delay between each similar vibration call</param>
            /// <param name="key">A unique key to differentiate 2 vibrations with the same strength</param>
            public void HeavyImpact(float delta = 0.03f, string key = "") {
                this._VibrationCheck(HapticPatterns.PresetType.HeavyImpact, delta, "HeavyImpact-" + key);
            }

            /// <summary>
            /// Triggers a vibration. (100% / short duration)
            /// </summary>
            /// <param name="delta">The delay between each similar vibration call</param>
            /// <param name="key">A unique key to differentiate 2 vibrations with the same strength</param>
            public void RigidImpact(float delta = 0.03f, string key = "") {
                this._VibrationCheck(HapticPatterns.PresetType.RigidImpact, delta, "RigidImpact-" + key);
            }

            /// <summary>
            /// Triggers a chain vibration. (47% / medium duration) -> short pause -> (47% / short duration) -> short pause -> (100% / long duration) -> short pause -> (16% / short duration)
            /// </summary>
            /// <param name="delta">The delay between each similar vibration call</param>
            /// <param name="key">A unique key to differentiate 2 vibrations with the same strength</param>
            public void Failure(float delta = 0.03f, string key = "") {
                this._VibrationCheck(HapticPatterns.PresetType.Failure, delta, "Failure-" + key);
            }

            /// <summary>
            /// Triggers a vibration. (47% / short duration)
            /// </summary>
            /// <param name="delta">The delay between each similar vibration call</param>
            /// <param name="key">A unique key to differentiate 2 vibrations with the same strength</param>
            public void Selection(float delta = 0.03f, string key = "") {
                this._VibrationCheck(HapticPatterns.PresetType.Selection, delta, "Selection-" + key);
            }

            /// <summary>
            /// Triggers a chain vibration. (16% / short duration) -> short pause -> (100% / long duration)
            /// </summary>
            /// <param name="delta">The delay between each similar vibration call</param>
            /// <param name="key">A unique key to differentiate 2 vibrations with the same strength</param>
            public void Success(float delta = 0.03f, string key = "") {
                this._VibrationCheck(HapticPatterns.PresetType.Success, delta, "Success-" + key);
            }

            /// <summary>
            /// Triggers a chain vibration. (100% / medium duration) -> long pause -> (47% / short duration)
            /// </summary>
            /// <param name="delta">The delay between each similar vibration call</param>
            /// <param name="key">A unique key to differentiate 2 vibrations with the same strength</param>
            public void Warning(float delta = 0.03f, string key = "") {
                this._VibrationCheck(HapticPatterns.PresetType.Warning, delta, "Warning-" + key);
            }

            /// <summary>
            /// Triggers a custom vibration.
            /// </summary>
            /// <param name="impact">Strength percentage</param>
            /// <param name="delta">The delay between each similar vibration call</param>
            /// <param name="key">A unique key to differentiate 2 vibrations with the same strength</param>
            public void SmartImpact(float impact, float delta = 0.03f, string key = "") {
                if (impact > 0.66f) {
                    this.HeavyImpact(delta, key);
                } else if (impact > 0.33f) {
                    this.MediumImpact(delta, key);
                } else {
                    this.LightImpact(delta, key);
                }
            }
        }
    }
}