using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class EquipmentBuildToolsWindow : EditorWindow
{
    private const string WindowTitle = "Equipment Builds";

    private string newBuildName = "Build 1";
    private Vector2 scrollPosition;
    private int selectedBuildIndex = -1;
    private string statusMessage = string.Empty;

    [MenuItem("Tools/Rogue Slide/Equipment Builds")]
    public static void ShowWindow()
    {
        EquipmentBuildToolsWindow window = GetWindow<EquipmentBuildToolsWindow>();
        window.titleContent = new GUIContent(WindowTitle);
        window.minSize = new Vector2(460f, 520f);
        window.Show();
    }

    private void OnEnable()
    {
        titleContent = new GUIContent(WindowTitle);
    }

    private void OnGUI()
    {
        BoardManager boardManager = FindBoardManager();
        Character character = boardManager != null && boardManager.Player != null ? boardManager.Player.ControlledCharacter : null;
        string characterId = boardManager != null ? boardManager.GetCurrentCharacterId() : string.Empty;

        DrawHeader(character);
        EditorGUILayout.Space(8f);

        using (new EditorGUI.DisabledScope(boardManager == null || character == null))
        {
            DrawSaveSection(boardManager);
        }

        EditorGUILayout.Space(12f);
        DrawSavedBuildsSection(boardManager, characterId);
        EditorGUILayout.Space(10f);
        DrawStatusBox();
    }

    private void DrawHeader(Character character)
    {
        EditorGUILayout.LabelField("Rogue Slide Build Tools", EditorStyles.boldLabel);
        string characterName = character != null ? character.CharacterName : "No active character";
        EditorGUILayout.HelpBox(
            character != null
                ? $"Active character: {characterName}\nSave, load and delete equipment builds for quick advanced testing."
                : "Enter Play Mode with an active character to save or load a build on the current run.",
            character != null ? MessageType.Info : MessageType.Warning);
    }

    private void DrawSaveSection(BoardManager boardManager)
    {
        EditorGUILayout.LabelField("Save Current Build", EditorStyles.boldLabel);
        newBuildName = EditorGUILayout.TextField("Build Name", newBuildName);

        using (new GUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Save Current Build", GUILayout.Height(30f), GUILayout.Width(180f)))
            {
                string finalBuildName = string.IsNullOrWhiteSpace(newBuildName) ? "Build" : newBuildName.Trim();
                EquipmentBuildData buildData = boardManager.CreateEquipmentBuildSnapshot(finalBuildName);
                if (buildData != null)
                {
                    EquipmentBuildLibrary.SaveBuild(buildData);
                    newBuildName = buildData.BuildName;
                    statusMessage = $"Saved build \"{buildData.BuildName}\".";
                }
                else
                {
                    statusMessage = "Unable to save the current build.";
                }
            }
        }
    }

    private void DrawSavedBuildsSection(BoardManager boardManager, string characterId)
    {
        List<EquipmentBuildData> builds = EquipmentBuildLibrary.GetBuilds(characterId);
        if (selectedBuildIndex >= builds.Count)
        {
            selectedBuildIndex = builds.Count - 1;
        }

        EditorGUILayout.LabelField($"Saved Builds ({builds.Count})", EditorStyles.boldLabel);
        if (builds.Count == 0)
        {
            EditorGUILayout.HelpBox("No saved build for the current character yet.", MessageType.None);
            return;
        }

        string[] buildNames = new string[builds.Count];
        for (int index = 0; index < builds.Count; index++)
        {
            EquipmentBuildData build = builds[index];
            buildNames[index] = $"Lv.{GetBuildLevel(build)} - {build.BuildName}";
        }

        selectedBuildIndex = Mathf.Clamp(selectedBuildIndex < 0 ? 0 : selectedBuildIndex, 0, builds.Count - 1);
        selectedBuildIndex = GUILayout.SelectionGrid(selectedBuildIndex, buildNames, 1, EditorStyles.miniButton);
        EquipmentBuildData selectedBuild = builds[selectedBuildIndex];

        EditorGUILayout.Space(8f);
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.MinHeight(220f));
        EditorGUILayout.LabelField("Selected Build", EditorStyles.boldLabel);
        EditorGUILayout.TextField("Name", selectedBuild.BuildName);
        EditorGUILayout.IntField("Level", GetBuildLevel(selectedBuild));
        EditorGUILayout.TextField("Weapon", selectedBuild.BasicAttackAbilityId);
        EditorGUILayout.TextField("Mobility", selectedBuild.MobilityAbilityId);
        EditorGUILayout.TextField("Power", selectedBuild.SpecialAbilityId);
        EditorGUILayout.LabelField($"Upgrades: {CountUpgradeStacks(selectedBuild.Upgrades)}");
        EditorGUILayout.LabelField($"Items: {(selectedBuild.OwnedItems != null ? selectedBuild.OwnedItems.Count : 0)}");

        if (selectedBuild.Upgrades != null && selectedBuild.Upgrades.Count > 0)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Upgrade Stacks", EditorStyles.boldLabel);
            for (int index = 0; index < selectedBuild.Upgrades.Count; index++)
            {
                EquipmentBuildUpgradeEntry entry = selectedBuild.Upgrades[index];
                if (entry == null)
                {
                    continue;
                }

                EditorGUILayout.LabelField($"- {entry.UpgradeKey} x{entry.Stacks}");
            }
        }

        if (selectedBuild.OwnedItems != null && selectedBuild.OwnedItems.Count > 0)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Items", EditorStyles.boldLabel);
            for (int index = 0; index < selectedBuild.OwnedItems.Count; index++)
            {
                EditorGUILayout.LabelField($"- {selectedBuild.OwnedItems[index]}");
            }
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.Space(10f);

        using (new GUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(boardManager == null || boardManager.Player == null || boardManager.Player.ControlledCharacter == null))
            {
                if (GUILayout.Button("Load Selected Build", GUILayout.Height(30f)))
                {
                    bool loaded = boardManager != null && boardManager.ApplyEquipmentBuild(selectedBuild);
                    statusMessage = loaded
                        ? $"Loaded build \"{selectedBuild.BuildName}\"."
                        : "Unable to load the selected build.";
                }
            }

            if (GUILayout.Button("Delete Selected Build", GUILayout.Height(30f)))
            {
                bool confirmed = EditorUtility.DisplayDialog(
                    "Delete Equipment Build",
                    $"Delete \"{selectedBuild.BuildName}\"?",
                    "Delete",
                    "Cancel");
                if (confirmed)
                {
                    EquipmentBuildLibrary.DeleteBuild(characterId, selectedBuild.BuildName);
                    selectedBuildIndex = -1;
                    statusMessage = $"Deleted build \"{selectedBuild.BuildName}\".";
                }
            }
        }
    }

    private void DrawStatusBox()
    {
        if (string.IsNullOrWhiteSpace(statusMessage))
        {
            return;
        }

        EditorGUILayout.HelpBox(statusMessage, MessageType.None);
    }

    private static int CountUpgradeStacks(List<EquipmentBuildUpgradeEntry> upgrades)
    {
        if (upgrades == null)
        {
            return 0;
        }

        int total = 0;
        for (int index = 0; index < upgrades.Count; index++)
        {
            EquipmentBuildUpgradeEntry entry = upgrades[index];
            if (entry != null && entry.Stacks > 0)
            {
                total += entry.Stacks;
            }
        }

        return total;
    }

    private static int GetBuildLevel(EquipmentBuildData buildData)
    {
        if (buildData == null)
        {
            return 0;
        }

        int equippedAbilityCount = 0;
        if (!string.IsNullOrWhiteSpace(buildData.BasicAttackAbilityId))
        {
            equippedAbilityCount++;
        }

        if (!string.IsNullOrWhiteSpace(buildData.MobilityAbilityId))
        {
            equippedAbilityCount++;
        }

        if (!string.IsNullOrWhiteSpace(buildData.SpecialAbilityId))
        {
            equippedAbilityCount++;
        }

        int itemCount = buildData.OwnedItems != null ? buildData.OwnedItems.Count : 0;
        return equippedAbilityCount + CountUpgradeStacks(buildData.Upgrades) + itemCount;
    }

    private static BoardManager FindBoardManager()
    {
        return Object.FindFirstObjectByType<BoardManager>();
    }
}
