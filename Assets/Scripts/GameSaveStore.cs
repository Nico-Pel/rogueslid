using System;
using UnityEngine;

/// <summary>Local JSON saves. Keep the original keys so existing installs retain their progress.</summary>
public static class GameSaveStore
{
    public static T Load<T>(string key) where T : class, new()
    {
        string json = PlayerPrefs.GetString(key, string.Empty);
        if (TryRead(json, out T data)) return data;
        if (!string.IsNullOrWhiteSpace(json)) PreserveUnreadable(key, json);
        if (TryRead(PlayerPrefs.GetString(key + ".backup", string.Empty), out data))
        {
            Debug.LogWarning("[Rogue Slide] Recovered local save from backup: " + key);
            return data;
        }
        return new T();
    }

    public static void Save<T>(string key, T data) where T : class, new()
    {
        string json = JsonUtility.ToJson(data ?? new T());
        string previous = PlayerPrefs.GetString(key, string.Empty);
        if (TryRead<T>(previous, out _))
            PlayerPrefs.SetString(key + ".backup", previous);
        else if (!string.IsNullOrWhiteSpace(previous))
            PreserveUnreadable(key, previous);
        if (!PlayerPrefs.HasKey(key + ".backup")) PlayerPrefs.SetString(key + ".backup", json);
        PlayerPrefs.SetString(key, json);
        PlayerPrefs.Save();
    }

    public static void Delete(string key)
    {
        PlayerPrefs.DeleteKey(key);
        PlayerPrefs.DeleteKey(key + ".backup");
        PlayerPrefs.DeleteKey(key + ".unreadable");
        PlayerPrefs.Save();
    }

    private static bool TryRead<T>(string json, out T data) where T : class
    {
        data = null;
        if (string.IsNullOrWhiteSpace(json)) return false;
        try { data = JsonUtility.FromJson<T>(json); }
        catch (ArgumentException) { return false; }
        return data != null;
    }

    private static void PreserveUnreadable(string key, string json)
    {
        if (PlayerPrefs.GetString(key + ".unreadable", string.Empty) == json) return;
        PlayerPrefs.SetString(key + ".unreadable", json);
        PlayerPrefs.Save();
        Debug.LogWarning("[Rogue Slide] Unreadable local save preserved for recovery: " + key);
    }
}
