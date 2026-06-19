using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BoardManager))]
public class BoardManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        BoardManager boardManager = (BoardManager)target;
        if (boardManager == null)
        {
            return;
        }

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("Give Cheat Rewards"))
            {
                boardManager.RequestCheatRewardsFromInspector();
            }
        }
    }
}
