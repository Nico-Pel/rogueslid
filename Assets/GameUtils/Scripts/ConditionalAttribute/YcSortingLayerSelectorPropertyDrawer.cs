using UnityEngine;
using UnityEditor;

#if UNITY_EDITOR
using UnityEditorInternal;

[CustomPropertyDrawer(typeof(YcSortingLayerSelectorAttribute))]
public class YcSortingLayerSelectorPropertyDrawer : PropertyDrawer {

    private string[] _sortingLayers;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
        if (property.propertyType == SerializedPropertyType.String) {
            EditorGUI.BeginProperty(position, label, property);

            string[] sortingLayers = this.GetSortingLayerNames();
            int index = 0;
            for (int i = 1; i < sortingLayers.Length; i++) {
                if (sortingLayers[i] == property.stringValue) {
                    index = i;
                    break;
                }
            }

            index = EditorGUI.Popup(position, label.text, index, sortingLayers);

            property.stringValue = sortingLayers[index];
            EditorGUI.EndProperty();
        } else {
            Debug.LogError("Property " + property.name + " is not a string. SortingLayerSelector attribute cannot be applied");
            EditorGUI.PropertyField(position, property, label);
        }
    }

    private string[] GetSortingLayerNames() {
        if (this._sortingLayers == null) {
            this._sortingLayers = typeof(InternalEditorUtility).GetProperty("sortingLayerNames", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                .GetValue(null, null) as string[];
        }
        return this._sortingLayers;
    }
}

#endif