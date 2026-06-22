using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class EquipmentBuildUpgradeEntry
{
    public string UpgradeKey;
    public int Stacks;
}

[Serializable]
public class EquipmentBuildData
{
    public string BuildName;
    public string CharacterId;
    public string BasicAttackAbilityId;
    public string MobilityAbilityId;
    public string SpecialAbilityId;
    public List<EquipmentBuildUpgradeEntry> Upgrades = new List<EquipmentBuildUpgradeEntry>();
    public List<string> OwnedItems = new List<string>();
}

[Serializable]
public class EquipmentBuildLibraryData
{
    public List<EquipmentBuildData> Builds = new List<EquipmentBuildData>();
}

public static class EquipmentBuildLibrary
{
    private const string PlayerPrefsKey = "rogueslid.equipment-builds";

    public static List<EquipmentBuildData> GetBuilds(string characterId)
    {
        List<EquipmentBuildData> filteredBuilds = new List<EquipmentBuildData>();
        EquipmentBuildLibraryData libraryData = LoadLibraryData();
        string normalizedCharacterId = NormalizeCharacterId(characterId);

        for (int index = 0; index < libraryData.Builds.Count; index++)
        {
            EquipmentBuildData build = libraryData.Builds[index];
            if (build == null || NormalizeCharacterId(build.CharacterId) != normalizedCharacterId)
            {
                continue;
            }

            filteredBuilds.Add(CloneBuild(build));
        }

        filteredBuilds.Sort((left, right) => string.Compare(left?.BuildName, right?.BuildName, StringComparison.OrdinalIgnoreCase));
        return filteredBuilds;
    }

    public static void SaveBuild(EquipmentBuildData buildData)
    {
        if (buildData == null || string.IsNullOrWhiteSpace(buildData.CharacterId))
        {
            return;
        }

        EquipmentBuildLibraryData libraryData = LoadLibraryData();
        string normalizedCharacterId = NormalizeCharacterId(buildData.CharacterId);
        string normalizedBuildName = NormalizeBuildName(buildData.BuildName);

        for (int index = libraryData.Builds.Count - 1; index >= 0; index--)
        {
            EquipmentBuildData existingBuild = libraryData.Builds[index];
            if (existingBuild == null)
            {
                libraryData.Builds.RemoveAt(index);
                continue;
            }

            if (NormalizeCharacterId(existingBuild.CharacterId) == normalizedCharacterId
                && NormalizeBuildName(existingBuild.BuildName) == normalizedBuildName)
            {
                libraryData.Builds[index] = CloneBuild(buildData);
                SaveLibraryData(libraryData);
                return;
            }
        }

        libraryData.Builds.Add(CloneBuild(buildData));
        SaveLibraryData(libraryData);
    }

    public static void DeleteBuild(string characterId, string buildName)
    {
        EquipmentBuildLibraryData libraryData = LoadLibraryData();
        string normalizedCharacterId = NormalizeCharacterId(characterId);
        string normalizedBuildName = NormalizeBuildName(buildName);

        for (int index = libraryData.Builds.Count - 1; index >= 0; index--)
        {
            EquipmentBuildData existingBuild = libraryData.Builds[index];
            if (existingBuild == null)
            {
                libraryData.Builds.RemoveAt(index);
                continue;
            }

            if (NormalizeCharacterId(existingBuild.CharacterId) == normalizedCharacterId
                && NormalizeBuildName(existingBuild.BuildName) == normalizedBuildName)
            {
                libraryData.Builds.RemoveAt(index);
            }
        }

        SaveLibraryData(libraryData);
    }

    private static EquipmentBuildLibraryData LoadLibraryData()
    {
        string rawJson = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return new EquipmentBuildLibraryData();
        }

        EquipmentBuildLibraryData libraryData = JsonUtility.FromJson<EquipmentBuildLibraryData>(rawJson);
        return libraryData ?? new EquipmentBuildLibraryData();
    }

    private static void SaveLibraryData(EquipmentBuildLibraryData libraryData)
    {
        string json = JsonUtility.ToJson(libraryData ?? new EquipmentBuildLibraryData());
        PlayerPrefs.SetString(PlayerPrefsKey, json);
        PlayerPrefs.Save();
    }

    private static EquipmentBuildData CloneBuild(EquipmentBuildData source)
    {
        EquipmentBuildData clone = new EquipmentBuildData
        {
            BuildName = source != null ? source.BuildName : string.Empty,
            CharacterId = source != null ? source.CharacterId : string.Empty,
            BasicAttackAbilityId = source != null ? source.BasicAttackAbilityId : string.Empty,
            MobilityAbilityId = source != null ? source.MobilityAbilityId : string.Empty,
            SpecialAbilityId = source != null ? source.SpecialAbilityId : string.Empty
        };

        if (source?.Upgrades != null)
        {
            for (int index = 0; index < source.Upgrades.Count; index++)
            {
                EquipmentBuildUpgradeEntry entry = source.Upgrades[index];
                if (entry == null || string.IsNullOrWhiteSpace(entry.UpgradeKey) || entry.Stacks <= 0)
                {
                    continue;
                }

                clone.Upgrades.Add(new EquipmentBuildUpgradeEntry
                {
                    UpgradeKey = entry.UpgradeKey,
                    Stacks = entry.Stacks
                });
            }
        }

        if (source?.OwnedItems != null)
        {
            for (int index = 0; index < source.OwnedItems.Count; index++)
            {
                string itemKey = source.OwnedItems[index];
                if (!string.IsNullOrWhiteSpace(itemKey))
                {
                    clone.OwnedItems.Add(itemKey);
                }
            }
        }

        return clone;
    }

    private static string NormalizeCharacterId(string characterId)
    {
        return string.IsNullOrWhiteSpace(characterId) ? string.Empty : characterId.Trim();
    }

    private static string NormalizeBuildName(string buildName)
    {
        return string.IsNullOrWhiteSpace(buildName) ? string.Empty : buildName.Trim();
    }
}
