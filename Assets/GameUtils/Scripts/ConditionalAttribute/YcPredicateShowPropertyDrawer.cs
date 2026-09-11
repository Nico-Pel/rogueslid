using UnityEngine;
using UnityEditor;
using NCalc;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Reflection;

#if UNITY_EDITOR

[CustomPropertyDrawer(typeof(YcPredicateShowAttribute))]
public class YcPredicateShowPropertyDrawer : PropertyDrawer {
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
        YcPredicateShowAttribute att = (YcPredicateShowAttribute)attribute;

        bool enabled = EvaluatePredicate(att, property);
        bool prevEnabled = GUI.enabled;
        GUI.enabled = enabled || att.IsReadOnly;

        if (enabled || att.IsReadOnly)
            EditorGUI.PropertyField(position, property, label, true);

        GUI.enabled = prevEnabled;
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
        YcPredicateShowAttribute att = (YcPredicateShowAttribute)attribute;
        if (EvaluatePredicate(att, property) || att.IsReadOnly)
            return EditorGUI.GetPropertyHeight(property, label, true);

        // effectively hide field
        return -EditorGUIUtility.standardVerticalSpacing;
    }

    // ---------------------------------------------------------------------

    private bool EvaluatePredicate(YcPredicateShowAttribute att, SerializedProperty property) {
        try {
            string expression = att.Predicate;

            // convert "@obj != null" to "@obj" for easier boolean handling
            expression = Regex.Replace(expression, @"@([A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)\s*!=\s*null", "@$1");
            // convert "@obj == null" to "!@obj"
            expression = Regex.Replace(expression, @"@([A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)\s*==\s*null", "!@$1");

            string replaced = ReplaceVariables(property, expression);

            var expr = new Expression(replaced);
            var result = expr.Evaluate();
            return System.Convert.ToBoolean(result);
        } catch (System.Exception e) {
            Debug.LogWarning($"[YcPredicateShow] Failed to evaluate '{att.Predicate}': {e.Message}");
            // fail-safe: show property rather than hide
            return true;
        }
    }

    // ---------------------------------------------------------------------

    private string ReplaceVariables(SerializedProperty property, string expression) {
        return Regex.Replace(expression, @"@([A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)", match => {
            string varName = match.Groups[1].Value;
            SerializedProperty sp = property.serializedObject.FindProperty(varName);
            object value = sp != null ? GetValueFromProperty(sp) : GetValueFromPath(property, varName);
            if (value == null)
                return "false";

            switch (value) {
                case string s:
                    s = s.Replace("'", "''");
                    return $"'{s}'";
                case bool b:
                    return b ? "true" : "false";
                case UnityEngine.Object obj:
                    return obj != null ? "true" : "false";
                case float f:
                    return f.ToString(CultureInfo.InvariantCulture);
                case double d:
                    return d.ToString(CultureInfo.InvariantCulture);
                case int i:
                    return i.ToString();
                default:
                    return value.ToString();
            }
        });
    }

    // ---------------------------------------------------------------------

    private object GetValueFromProperty(SerializedProperty sp) {
        if (sp == null)
            return null;

        switch (sp.propertyType) {
            case SerializedPropertyType.Boolean: return sp.boolValue;
            case SerializedPropertyType.Integer: return sp.intValue;
            case SerializedPropertyType.Float: return sp.floatValue;
            case SerializedPropertyType.String: return sp.stringValue;
            case SerializedPropertyType.Enum: return sp.enumValueIndex;
            case SerializedPropertyType.ObjectReference: return sp.objectReferenceValue;
            default: return null;
        }
    }

    private object GetValueFromPath(SerializedProperty property, string path) {
        object current = property.serializedObject.targetObject;
        string[] segments = path.Split('.');

        for (int i = 0; i < segments.Length; i++) {
            if (current == null)
                return null;

            string segment = segments[i];
            var type = current.GetType();

            FieldInfo field = type.GetField(segment, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null) {
                current = field.GetValue(current);
                continue;
            }

            PropertyInfo propertyInfo = type.GetProperty(segment, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (propertyInfo != null) {
                current = propertyInfo.GetValue(current, null);
                continue;
            }

            return null;
        }

        return current;
    }
}

#endif