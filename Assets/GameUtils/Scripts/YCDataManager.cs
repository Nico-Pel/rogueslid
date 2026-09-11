using System;
using System.Collections.Generic;
using UnityEngine;

namespace YsoCorp {
    namespace GameUtils {

        [DefaultExecutionOrder(-15)]
        public sealed class YCDataManager : BaseManager {
            private static string PLAYER_LAUNCH_COUNT = "YC_PLAYER_LAUNCH_COUNT";
            private static string PLAYER_GAME_COUNT = "YC_PLAYER_GAME_COUNT";

            private static string ADVERTISING_ID = "ADVERTISING_ID";
            private static string VIBRATION = "VIBRATION";
            private static string SOUNDEFFECT = "SOUNDEFFECT";
            private static string SOUNDEFFECT_VOLUME = "SOUNDEFFECT_VOLUME";
            private static string SOUNDMUSIC = "SOUNDMUSIC";
            private static string SOUNDMUSIC_VOLUME = "SOUNDMUSIC_VOLUME";
            private static string ADSSHOW = "ADSSHOW";
            private static string AB_PLAYER_SAMPLE = "YC_PLAYER_SAMPLE";
            private static string REDROCK_REVENUE = "REDROCK_REVENUE";
            private static string LANGUAGE = "LANGUAGE";

            private static string INTERSTITIALS_NB = "INTERSTITIALS_NB";
            private static string REWARDEDS_NB = "REWARDEDS_NB";
            private static string TIMESTAMP = "TIMESTAMP";

            private static string DEBUG_OPENED = "DEBUG_OPENED";

            private static bool DEFAULT_VIBRATION = true;
            private static bool DEFAULT_SOUNDEFFECT = true;
            private static bool DEFAULT_SOUNDMUSIC = true;

            private static string MMP_EVENT = "MMP_EVENT_";

            private int[] _interstitialsWatchedMilestones = new int[] { 10, 15, 20 };
            private int[] _rewardedsWatchedMilestones = new int[] { 2, 5, 7 };
            private int[] _levelsReachedMilestones = new int[] { 3, 5, 7, 10, 15, 20, 25, 30, 40, 50, 100, 150, 200, 250, 300, 400, 500 };

            private void Awake() {
                this.ycManager.dataManager = this;
                if (ADataManager.HasKey(TIMESTAMP) == false) {
                    ADataManager.SetInt(TIMESTAMP, this.GetTimestamp());
                }
            }

            private int GetTimestamp() {
                return (int)(DateTimeOffset.Now.ToUnixTimeMilliseconds() / 1000);
            }

            public int GetDiffTimestamp() {
                return this.GetTimestamp() - ADataManager.GetInt(TIMESTAMP);
            }

            private void CheckMmpEvent(int value, int[] milestones, string endString) {
                for (int i = 0; i < milestones.Length; i++) {
                    string fullName = value.ToString() + endString;
                    if (value == milestones[i] && this.IsMmpEventSent(fullName) == false) {
                        this.ycManager.mmpManager.SendEvent(fullName);
                        this.SetMmpEventSent(fullName);
#if FIREBASE
                        Firebase.Analytics.FirebaseAnalytics.LogEvent("yc_" + fullName);
#endif
                        return;
                    }
                }
            }

            //PLAYER
            private void SetPlayerLaunchCount(int amount) {
                ADataManager.SetInt(PLAYER_LAUNCH_COUNT, amount);
            }

            internal void IncrementPlayerLaunchCount() {
                this.SetPlayerLaunchCount(this.GetPlayerLaunchCount() + 1);
            }

            internal int GetPlayerLaunchCount() {
                return ADataManager.GetInt(PLAYER_LAUNCH_COUNT);
            }

            private void SetPlayerGameCount(int amount) {
                ADataManager.SetInt(PLAYER_GAME_COUNT, amount);
            }

            internal void IncrementPlayerGameCount() {
                this.SetPlayerGameCount(this.GetPlayerGameCount() + 1);
            }

            internal int GetPlayerGameCount() {
                return ADataManager.GetInt(PLAYER_GAME_COUNT);
            }

            //ADVERTISING ID
            internal string GetAdvertisingId() {
                return ADataManager.GetString(ADVERTISING_ID, "");
            }
            internal void SetAdvertisingId(string id) {
                ADataManager.SetString(ADVERTISING_ID, id);
            }

            //VIBRATION
            internal bool GetVibration() {
                return ADataManager.GetBool(VIBRATION, DEFAULT_VIBRATION);
            }
            internal void SetVibration(bool enable) {
                ADataManager.SetBool(VIBRATION, enable);
            }

            //SOUND EFFECT
            internal bool GetSoundEffect() {
                return ADataManager.GetBool(SOUNDEFFECT, DEFAULT_SOUNDEFFECT) && this.GetSoundEffectVolume() != 0;
            }
            internal void SetSoundEffect(bool enable) {
                ADataManager.SetBool(SOUNDEFFECT, enable);
            }
            internal void ToggleSoundEffect() {
                this.SetSoundEffect(!GetSoundEffect());
            }
            internal void SetSoundEffectVolume(float volume) {
                ADataManager.SetFloat(SOUNDEFFECT_VOLUME, volume);
            }
            internal float GetSoundEffectVolume() {
                return ADataManager.GetFloat(SOUNDEFFECT_VOLUME, 1);
            }

            //MUSIC
            internal bool GetSoundMusic() {
                return ADataManager.GetBool(SOUNDMUSIC, DEFAULT_SOUNDMUSIC);
            }
            internal void SetSoundMusic(bool enable) {
                ADataManager.SetBool(SOUNDMUSIC, enable);
            }
            internal void ToggleSoundMusic() {
                this.SetSoundMusic(!GetSoundMusic());
            }
            internal void SetSoundMusicVolume(float volume) {
                ADataManager.SetFloat(SOUNDMUSIC_VOLUME, volume);
            }
            internal float GetSoundMusicVolume() {
                return ADataManager.GetFloat(SOUNDMUSIC_VOLUME, 1);
            }

            // ABTEST
            internal void SetPlayerSample(string sample) {
                ADataManager.SetString(AB_PLAYER_SAMPLE, sample);
            }
            internal string GetPlayerSample() {
                return ADataManager.GetString(AB_PLAYER_SAMPLE);
            }
            internal bool HasPlayerSample() {
                return ADataManager.HasKey(AB_PLAYER_SAMPLE);
            }
            internal void DeletePlayerSample() {
                ADataManager.DeleteKey(AB_PLAYER_SAMPLE);
            }

            // ADS
            internal bool GetAdsShow() {
                return ADataManager.GetInt(ADSSHOW, 1) == 1;
            }
            internal void BuyAdsShow() {
                ADataManager.SetInt(ADSSHOW, 0);
            }

            // AdsManager
            internal Dictionary<string, double> GetRedRockRevenue() {
                return ADataManager.GetObject<Dictionary<string, double>>(REDROCK_REVENUE);
            }

            internal void SetRedRockRevenue(Dictionary<string, double> dict) {
                ADataManager.SetObject(REDROCK_REVENUE, dict);
            }

            // LANGUAGE
            internal void SetLanguage(string lang) {
                ADataManager.SetString(LANGUAGE, lang);
            }
            internal string GetLanguage() {
                return ADataManager.GetString(LANGUAGE, "EN");
            }
            internal bool HasLanguage() {
                return ADataManager.HasKey(LANGUAGE);
            }

            // NB INTERSTITIALS
            public int GetInterstitialsNb() {
                return ADataManager.GetInt(INTERSTITIALS_NB, 0);
            }
            internal void IncrementInterstitialsNb() {
                int newValue = this.GetInterstitialsNb() + 1;
                ADataManager.SetInt(INTERSTITIALS_NB, newValue);
                this.CheckMmpEvent(newValue, this._interstitialsWatchedMilestones, "_interstitials_watched");
            }

            // NB REWARDEDS
            public int GetRewardedsNb() {
                return ADataManager.GetInt(REWARDEDS_NB, 0);
            }
            internal void IncrementRewardedsNb() {
                int newValue = this.GetRewardedsNb() + 1;
                ADataManager.SetInt(REWARDEDS_NB, newValue);
                this.CheckMmpEvent(newValue, this._rewardedsWatchedMilestones, "_rewardeds_watched");
            }

            // LEVEL PLAYED
            internal void IncrementLevelPlayed(int level, bool win) {
                if (win) {
                    this.CheckMmpEvent(level, this._levelsReachedMilestones, "_level_reached");
                }

#if FIREBASE
                Firebase.Analytics.FirebaseAnalytics.LogEvent("yc_level_finished");
#endif
            }

            //MMP EVENTS
            internal bool IsMmpEventSent(string name) {
                return ADataManager.GetBool(MMP_EVENT + name);
            }

            internal void SetMmpEventSent(string name, bool value = true) {
                ADataManager.SetBool(MMP_EVENT + name, value);
            }

            //MISC
            internal bool GetDebugWindowOpened() {
                return ADataManager.GetBool(DEBUG_OPENED);
            }
            internal void SetDebugWindowOpened(bool value = true) {
                ADataManager.SetBool(DEBUG_OPENED, value);
            }
        }
    }
}