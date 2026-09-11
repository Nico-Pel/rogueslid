using System;
using UnityEditor;
using UnityEngine;

namespace YsoCorp {

    public class YCEditorGUILayout {
        public static void AddEmptyLine(int amount = 1) {
            string spaces = "";
            for (int i = 0; i < amount - 1; i++) {
                spaces += "\n";
            }
            GUILayout.Label(spaces);
        }

        public static void AddLabel(GUIContent content, TextAnchor alignment = TextAnchor.MiddleLeft, GUIStyle style = null) {
            GUIStyle newstyle = new GUIStyle(style == null ? GUI.skin.label : style);
            newstyle.alignment = alignment;
            GUILayout.Label(content, newstyle);
        }
        public static void AddLabel(string text, TextAnchor alignment = TextAnchor.MiddleLeft, GUIStyle style = null) {
            AddLabel(new GUIContent(text), alignment, style);
        }

        public static void AddSelectableLabel(string text, TextAnchor alignment = TextAnchor.MiddleLeft, GUIStyle style = null) {
            GUIStyle newstyle = new GUIStyle(style == null ? GUI.skin.label : style);
            newstyle.alignment = alignment;
            GUILayout.BeginVertical("box");
            EditorGUILayout.SelectableLabel(text, newstyle);
            GUILayout.EndVertical();
        }

        public static string AddTextInputField(GUIContent title, string defaultValue, GUIStyle style = null) {
            GUIStyle newstyle = new GUIStyle(style == null ? GUI.skin.textField : style);
            return EditorGUILayout.TextField(title, defaultValue, newstyle);
        }
        public static string AddTextInputField(string title, string defaultValue, GUIStyle style = null) {
            return AddTextInputField(new GUIContent(title), defaultValue, style);
        }

        public static string AddTextAreaField(GUIContent title, string defaultValue, GUIStyle style = null) {
            GUILayout.BeginHorizontal();
            GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fixedWidth = 147;
            AddLabel(title, TextAnchor.MiddleLeft, labelStyle);
            GUIStyle areaStyle = new GUIStyle(style == null ? new GUIStyle(EditorStyles.textArea) : style);
            defaultValue = EditorGUILayout.TextArea(defaultValue, areaStyle);
            GUILayout.EndHorizontal();
            return defaultValue;
        }
        public static string AddTextAreaField(string title, string defaultValue, GUIStyle style = null) {
            return AddTextAreaField(new GUIContent(title), defaultValue, style);
        }

        public static bool AddToggle(GUIContent title = null, bool defaultValue = false, TextAnchor alignment = TextAnchor.MiddleLeft) {
            GUIStyle style = new GUIStyle(GUI.skin.toggle);
            style.alignment = alignment;
            return GUILayout.Toggle(defaultValue, title, style);
        }
        public static bool AddToggle(string title, bool defaultValue = false, TextAnchor alignment = TextAnchor.MiddleLeft) {
            return AddToggle(new GUIContent(title), defaultValue, alignment);
        }

        public static bool AddBoolField(GUIContent title = null, bool defaultValue = false, TextAnchor alignment = TextAnchor.MiddleLeft) {
            GUIStyle style = new GUIStyle(GUI.skin.toggle);
            style.alignment = alignment;
            return EditorGUILayout.Toggle(title, defaultValue, style);
        }
        public static bool AddBoolField(string title, bool defaultValue = false, TextAnchor alignment = TextAnchor.MiddleLeft) {
            return AddBoolField(new GUIContent(title), defaultValue, alignment);
        }

        public static void AddButton(GUIContent text, Action action, GUIStyle style = null) {
            GUIStyle newstyle = new GUIStyle(style == null ? GUI.skin.button : style);
            if (GUILayout.Button(text, newstyle)) {
                action();
            }
        }
        public static void AddButton(string text, Action action, GUIStyle style = null) {
            AddButton(new GUIContent(text), action, style);
        }

        public static void AddFoldout(GUIContent title, ref bool status, Action inside, GUIStyle style = null) {
            GUILayout.BeginVertical("box");
            GUIStyle foldoutStyle = style != null ? style : EditorStyles.foldout;
            foldoutStyle.wordWrap = false;

            float calculatedHeight = foldoutStyle.CalcHeight(title, EditorGUIUtility.currentViewWidth);
            float defaultHeight = EditorGUIUtility.singleLineHeight;
            float difference = calculatedHeight - defaultHeight;

            if (difference > 0) {
                GUILayout.Space(difference / 2);
            }
            status = EditorGUILayout.Foldout(status, title, true, foldoutStyle);
            if (difference > 0) {
                GUILayout.Space(difference / 2);
            }

            if (status) {
                GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
                boxStyle.margin.left = 25;
                GUILayout.BeginVertical(boxStyle);
                inside.Invoke();
                GUILayout.EndVertical();
            }
            GUILayout.EndVertical();
        }
        public static void AddFoldout(string text, ref bool status, Action inside, GUIStyle style = null) {
            AddFoldout(new GUIContent(text), ref status, inside, style);
        }

        public static void AddScrollView(ref Vector2 scrollPosition, Action inside, bool alwaysShowHorizontal = false, bool alwaysShowVertical = false) {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, alwaysShowHorizontal, alwaysShowVertical);
            inside.Invoke();
            EditorGUILayout.EndScrollView();
        }

        public static Enum AddEnumPopup(GUIContent title, Enum enumerator) {
            return EditorGUILayout.EnumPopup(title, enumerator);
        }
        public static Enum AddEnumPopup(string title, Enum enumerator) {
            return AddEnumPopup(new GUIContent(title), enumerator);
        }

        public static Enum AddEnumFlagPopup(GUIContent title, Enum enumerator) {
            return EditorGUILayout.EnumFlagsField(title, enumerator);
        }
        public static Enum AddEnumFlagPopup(string title, Enum enumerator) {
            return AddEnumFlagPopup(new GUIContent(title), enumerator);
        }

        public static void AddTable(int column, int row, Action[] actions) {
            if (actions.Length <= column * row) {
                GUILayout.BeginVertical("box");
                for (int i = 0; i < row; i++) {
                    GUILayout.BeginHorizontal();
                    for (int j = 0; j < column; j++) {
                        GUILayout.BeginHorizontal("box");
                        actions[i * column + j]();
                        GUILayout.EndHorizontal();
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndVertical();
            } else {
                Debug.LogError("Actions Array too large for the set size");
            }
        }

        public static T AddObjectField<T>(GUIContent text, T obj, bool allowSceneObjects) where T : UnityEngine.Object {
            return EditorGUILayout.ObjectField(text, obj, typeof(T), allowSceneObjects) as T;
        }
        public static T AddObjectField<T>(string text, T obj, bool allowSceneObjects) where T : UnityEngine.Object {
            return AddObjectField(new GUIContent(text), obj, allowSceneObjects);
        }

        public static void AddHelpBox(string content, MessageType messageType) {
            EditorGUILayout.HelpBox(content, messageType);
        }
    }
    
}
