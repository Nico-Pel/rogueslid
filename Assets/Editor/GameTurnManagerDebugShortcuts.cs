#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

public static class GameTurnManagerDebugShortcuts
{
    [Shortcut("Rogue Slide/Debug Give Money", KeyCode.M)]
    private static void DebugGiveMoneyShortcut()
    {
        if (!Application.isPlaying)
        {
            Debug.Log("[DebugGiveMoneyShortcut] Ignored because the game is not playing.");
            return;
        }

        GameTurnManager turnManager = Object.FindFirstObjectByType<GameTurnManager>();
        if (turnManager == null)
        {
            Debug.LogWarning("[DebugGiveMoneyShortcut] No GameTurnManager found in the active scene.");
            return;
        }

        Debug.Log("[DebugGiveMoneyShortcut] Editor shortcut triggered.");
        turnManager.DebugGiveMoney();
    }
}
#endif
