using System;
using UnityEngine;
using YsoCorp;
using YsoCorp.GameUtils;

/// <summary>One GameUtils game corresponds to one complete run, not one arena.</summary>
public static class GameServices
{
    // Same persistent counter as YCDataManager; do not increment it twice.
    private const string GameCountKey = "YC_PLAYER_GAME_COUNT";
    private static bool runActive;
    private static bool playPending;

    public static int GamesPlayed => ADataManager.GetInt(GameCountKey);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        runActive = false;
        playPending = false;
    }

    public static void PlayAfterInterstitial(Action startRun)
    {
        if (playPending || startRun == null) return;
        if (YCManager.instance != null && YCManager.instance.adsManager != null
            && YCManager.instance.adsManager.IsFullscreenAdPending) return;
        playPending = true;
        bool completed = false;
        void Continue()
        {
            if (completed) return;
            completed = true;
            try { startRun(); }
            finally { playPending = false; }
        }

        var manager = YCManager.instance;
        if (manager != null && manager.adsManager != null)
            manager.adsManager.ShowInterstitial(Continue);
        else
            Continue();
    }

    public static void StartRun(int tourment)
    {
        // Starting a new run while one is active means the previous run was abandoned.
        FinishRun(false);
        var manager = YCManager.instance;
        if (manager != null)
            manager.OnGameStarted(Mathf.Max(1, tourment));
        else
            ADataManager.SetInt(GameCountKey, GamesPlayed + 1);
        runActive = true;
        PlayerPrefs.Save();
    }

    public static void FinishRun(bool won)
    {
        if (!runActive) return;
        runActive = false;
        YCManager.instance?.OnGameFinished(won);
        PlayerPrefs.Save();
    }
}
