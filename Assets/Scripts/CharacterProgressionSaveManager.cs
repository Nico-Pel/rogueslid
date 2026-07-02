using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class CharacterUpgradeProgressEntry
{
    public string UpgradeId;
    public int UnlockCount;
}

[Serializable]
public class CharacterProgressEntry
{
    public string CharacterId;
    public int OrbCount;
    public int MaxUnlockedTourment = 1;
    public int SelectedTourment = 1;
    public List<string> UnlockedRewardIds = new List<string>();
    public List<CharacterUpgradeProgressEntry> Upgrades = new List<CharacterUpgradeProgressEntry>();
}

[Serializable]
public class CharacterProgressLibraryData
{
    public List<CharacterProgressEntry> Characters = new List<CharacterProgressEntry>();
    public List<string> GloballyUnlockedRewardIds = new List<string>();
    public List<string> UnlockedCharacterIds = new List<string>();
}

public static class CharacterProgressionSaveManager
{
    private const string PlayerPrefsKey = "rogueslid.character-progression";

    public static void ClearSave()
    {
        PlayerPrefs.DeleteKey(PlayerPrefsKey);
        PlayerPrefs.Save();
    }

    public static int GetOrbCount(string characterId)
    {
        CharacterProgressEntry characterProgress = GetOrCreateCharacterProgress(LoadLibraryData(), characterId, false);
        return characterProgress != null ? Mathf.Max(0, characterProgress.OrbCount) : 0;
    }

    public static int GetUpgradeUnlockCount(string characterId, string upgradeId)
    {
        CharacterUpgradeProgressEntry upgradeProgress = GetUpgradeProgress(characterId, upgradeId, false);
        return upgradeProgress != null ? Mathf.Max(0, upgradeProgress.UnlockCount) : 0;
    }

    public static int GetMaxUnlockedTourment(string characterId)
    {
        CharacterProgressEntry characterProgress = GetOrCreateCharacterProgress(LoadLibraryData(), characterId, false);
        return characterProgress != null ? Mathf.Clamp(characterProgress.MaxUnlockedTourment, 1, 5) : 1;
    }

    public static int GetSelectedTourment(string characterId)
    {
        CharacterProgressEntry characterProgress = GetOrCreateCharacterProgress(LoadLibraryData(), characterId, false);
        if (characterProgress == null)
        {
            return 1;
        }

        int maxUnlockedTourment = Mathf.Clamp(characterProgress.MaxUnlockedTourment, 1, 5);
        return Mathf.Clamp(characterProgress.SelectedTourment, 1, maxUnlockedTourment);
    }

    public static void SetSelectedTourment(string characterId, int selectedTourment)
    {
        if (string.IsNullOrWhiteSpace(characterId))
        {
            return;
        }

        CharacterProgressLibraryData libraryData = LoadLibraryData();
        CharacterProgressEntry characterProgress = GetOrCreateCharacterProgress(libraryData, characterId, true);
        int maxUnlockedTourment = Mathf.Clamp(characterProgress.MaxUnlockedTourment, 1, 5);
        characterProgress.SelectedTourment = Mathf.Clamp(selectedTourment, 1, maxUnlockedTourment);
        SaveLibraryData(libraryData);
    }

    public static bool TryUnlockNextTourment(string characterId, int clearedTourmentLevel, out int unlockedTourmentLevel)
    {
        unlockedTourmentLevel = 0;
        if (string.IsNullOrWhiteSpace(characterId))
        {
            return false;
        }

        int currentLevel = Mathf.Clamp(clearedTourmentLevel, 1, 5);
        if (currentLevel >= 5)
        {
            return false;
        }

        CharacterProgressLibraryData libraryData = LoadLibraryData();
        CharacterProgressEntry characterProgress = GetOrCreateCharacterProgress(libraryData, characterId, true);
        int currentMaxUnlocked = Mathf.Clamp(characterProgress.MaxUnlockedTourment, 1, 5);
        int nextTourment = Mathf.Clamp(currentLevel + 1, 1, 5);
        if (currentMaxUnlocked >= nextTourment)
        {
            return false;
        }

        characterProgress.MaxUnlockedTourment = nextTourment;
        if (characterProgress.SelectedTourment <= 0 || characterProgress.SelectedTourment > nextTourment)
        {
            characterProgress.SelectedTourment = currentLevel;
        }

        SaveLibraryData(libraryData);
        unlockedTourmentLevel = nextTourment;
        return true;
    }

    public static bool IsCharacterRewardUnlocked(string characterId, string rewardId)
    {
        if (string.IsNullOrWhiteSpace(characterId) || string.IsNullOrWhiteSpace(rewardId))
        {
            return false;
        }

        CharacterProgressEntry characterProgress = GetOrCreateCharacterProgress(LoadLibraryData(), characterId, false);
        return ContainsNormalized(characterProgress != null ? characterProgress.UnlockedRewardIds : null, rewardId);
    }

    public static bool UnlockCharacterReward(string characterId, string rewardId)
    {
        if (string.IsNullOrWhiteSpace(characterId) || string.IsNullOrWhiteSpace(rewardId))
        {
            return false;
        }

        CharacterProgressLibraryData libraryData = LoadLibraryData();
        CharacterProgressEntry characterProgress = GetOrCreateCharacterProgress(libraryData, characterId, true);
        if (ContainsNormalized(characterProgress.UnlockedRewardIds, rewardId))
        {
            return false;
        }

        characterProgress.UnlockedRewardIds.Add(rewardId.Trim());
        SaveLibraryData(libraryData);
        return true;
    }

    public static bool IsGlobalRewardUnlocked(string rewardId)
    {
        if (string.IsNullOrWhiteSpace(rewardId))
        {
            return false;
        }

        CharacterProgressLibraryData libraryData = LoadLibraryData();
        return ContainsNormalized(libraryData.GloballyUnlockedRewardIds, rewardId);
    }

    public static bool UnlockGlobalReward(string rewardId)
    {
        if (string.IsNullOrWhiteSpace(rewardId))
        {
            return false;
        }

        CharacterProgressLibraryData libraryData = LoadLibraryData();
        if (ContainsNormalized(libraryData.GloballyUnlockedRewardIds, rewardId))
        {
            return false;
        }

        libraryData.GloballyUnlockedRewardIds.Add(rewardId.Trim());
        SaveLibraryData(libraryData);
        return true;
    }

    public static bool IsCharacterUnlocked(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId))
        {
            return false;
        }

        string normalizedCharacterId = NormalizeKey(characterId);
        if (normalizedCharacterId == "pandora")
        {
            return true;
        }

        CharacterProgressLibraryData libraryData = LoadLibraryData();
        return ContainsNormalized(libraryData.UnlockedCharacterIds, characterId);
    }

    public static bool UnlockCharacter(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId))
        {
            return false;
        }

        string normalizedCharacterId = NormalizeKey(characterId);
        if (normalizedCharacterId == "pandora")
        {
            return false;
        }

        CharacterProgressLibraryData libraryData = LoadLibraryData();
        if (ContainsNormalized(libraryData.UnlockedCharacterIds, characterId))
        {
            return false;
        }

        libraryData.UnlockedCharacterIds.Add(characterId.Trim());
        SaveLibraryData(libraryData);
        return true;
    }

    public static void AddOrbs(string characterId, int amount)
    {
        if (string.IsNullOrWhiteSpace(characterId) || amount <= 0)
        {
            return;
        }

        CharacterProgressLibraryData libraryData = LoadLibraryData();
        CharacterProgressEntry characterProgress = GetOrCreateCharacterProgress(libraryData, characterId, true);
        characterProgress.OrbCount = Mathf.Max(0, characterProgress.OrbCount + amount);
        SaveLibraryData(libraryData);
    }

    public static bool TryPurchaseUpgrade(string characterId, CharacterUpgradeData upgradeData)
    {
        if (string.IsNullOrWhiteSpace(characterId) || upgradeData == null)
        {
            return false;
        }

        CharacterProgressLibraryData libraryData = LoadLibraryData();
        CharacterProgressEntry characterProgress = GetOrCreateCharacterProgress(libraryData, characterId, true);
        CharacterUpgradeProgressEntry upgradeProgress = GetOrCreateUpgradeProgress(characterProgress, upgradeData.UpgradeId, true);
        if (upgradeProgress.UnlockCount >= upgradeData.MaxUnlockCount || characterProgress.OrbCount < upgradeData.OrbPrice)
        {
            return false;
        }

        characterProgress.OrbCount -= upgradeData.OrbPrice;
        upgradeProgress.UnlockCount = Mathf.Min(upgradeData.MaxUnlockCount, upgradeProgress.UnlockCount + 1);
        SaveLibraryData(libraryData);
        return true;
    }

    private static CharacterUpgradeProgressEntry GetUpgradeProgress(string characterId, string upgradeId, bool createIfMissing)
    {
        CharacterProgressLibraryData libraryData = LoadLibraryData();
        CharacterProgressEntry characterProgress = GetOrCreateCharacterProgress(libraryData, characterId, createIfMissing);
        CharacterUpgradeProgressEntry upgradeProgress = GetOrCreateUpgradeProgress(characterProgress, upgradeId, createIfMissing);

        if (createIfMissing)
        {
            SaveLibraryData(libraryData);
        }

        return upgradeProgress;
    }

    private static CharacterProgressEntry GetOrCreateCharacterProgress(CharacterProgressLibraryData libraryData, string characterId, bool createIfMissing)
    {
        if (libraryData == null || string.IsNullOrWhiteSpace(characterId))
        {
            return null;
        }

        string normalizedCharacterId = NormalizeKey(characterId);
        for (int index = libraryData.Characters.Count - 1; index >= 0; index--)
        {
            CharacterProgressEntry entry = libraryData.Characters[index];
            if (entry == null || string.IsNullOrWhiteSpace(entry.CharacterId))
            {
                libraryData.Characters.RemoveAt(index);
                continue;
            }

            if (NormalizeKey(entry.CharacterId) == normalizedCharacterId)
            {
                return entry;
            }
        }

        if (!createIfMissing)
        {
            return null;
        }

        CharacterProgressEntry createdEntry = new CharacterProgressEntry
        {
            CharacterId = characterId.Trim(),
            OrbCount = 0,
            MaxUnlockedTourment = 1,
            SelectedTourment = 1
        };
        libraryData.Characters.Add(createdEntry);
        return createdEntry;
    }

    private static CharacterUpgradeProgressEntry GetOrCreateUpgradeProgress(CharacterProgressEntry characterProgress, string upgradeId, bool createIfMissing)
    {
        if (characterProgress == null || string.IsNullOrWhiteSpace(upgradeId))
        {
            return null;
        }

        string normalizedUpgradeId = NormalizeKey(upgradeId);
        for (int index = characterProgress.Upgrades.Count - 1; index >= 0; index--)
        {
            CharacterUpgradeProgressEntry entry = characterProgress.Upgrades[index];
            if (entry == null || string.IsNullOrWhiteSpace(entry.UpgradeId))
            {
                characterProgress.Upgrades.RemoveAt(index);
                continue;
            }

            if (NormalizeKey(entry.UpgradeId) == normalizedUpgradeId)
            {
                return entry;
            }
        }

        if (!createIfMissing)
        {
            return null;
        }

        CharacterUpgradeProgressEntry createdEntry = new CharacterUpgradeProgressEntry
        {
            UpgradeId = upgradeId.Trim(),
            UnlockCount = 0
        };
        characterProgress.Upgrades.Add(createdEntry);
        return createdEntry;
    }

    private static CharacterProgressLibraryData LoadLibraryData()
    {
        string rawJson = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return new CharacterProgressLibraryData();
        }

        CharacterProgressLibraryData libraryData = JsonUtility.FromJson<CharacterProgressLibraryData>(rawJson);
        return libraryData ?? new CharacterProgressLibraryData();
    }

    private static void SaveLibraryData(CharacterProgressLibraryData libraryData)
    {
        string json = JsonUtility.ToJson(libraryData ?? new CharacterProgressLibraryData());
        PlayerPrefs.SetString(PlayerPrefsKey, json);
        PlayerPrefs.Save();
    }

    private static string NormalizeKey(string rawValue)
    {
        return string.IsNullOrWhiteSpace(rawValue) ? string.Empty : rawValue.Trim();
    }

    private static bool ContainsNormalized(List<string> values, string value)
    {
        if (values == null || values.Count == 0 || string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string normalizedValue = NormalizeKey(value);
        for (int index = values.Count - 1; index >= 0; index--)
        {
            string entry = values[index];
            if (string.IsNullOrWhiteSpace(entry))
            {
                values.RemoveAt(index);
                continue;
            }

            if (NormalizeKey(entry) == normalizedValue)
            {
                return true;
            }
        }

        return false;
    }
}
