using UnityEditor;
using UnityEngine;

public static class ClearSaveMenuItem
{
    [MenuItem("Tools/Rogue Slide/Clear Save")]
    private static void ClearSave()
    {
        bool confirmed = EditorUtility.DisplayDialog(
            "Clear Save",
            "Do you want to clear the local progression save for character orbs and upgrades?",
            "Clear Save",
            "Cancel");

        if (!confirmed)
        {
            return;
        }

        CharacterProgressionSaveManager.ClearSave();
        Debug.Log("[Rogue Slide] Local progression save cleared.");
    }
}
