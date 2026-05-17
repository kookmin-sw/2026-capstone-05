using UnityEditor;
using UnityEngine;

namespace Systems.Loot.Editor
{
    [CustomPropertyDrawer(typeof(LootTableEntry))]
    public class LootTableEntryDrawer : PropertyDrawer
    {
        private const float FrequencyMin = 0f;
        private const float FrequencyMax = 10f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            SerializedProperty itemData = property.FindPropertyRelative("itemData");
            SerializedProperty frequency = property.FindPropertyRelative("frequency");
            SerializedProperty minQuantity = property.FindPropertyRelative("minQuantity");
            SerializedProperty maxQuantity = property.FindPropertyRelative("maxQuantity");

            Rect line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, GetEntryLabel(itemData, label), true);

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;

                line.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                EditorGUI.PropertyField(line, itemData);

                line.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                frequency.floatValue = EditorGUI.Slider(
                    line,
                    new GUIContent("Frequency", "Relative appearance score. 0 disables this item, 10 is the most common."),
                    frequency.floatValue,
                    FrequencyMin,
                    FrequencyMax);

                line.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                EditorGUI.PropertyField(line, minQuantity);

                line.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                EditorGUI.PropertyField(line, maxQuantity);

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            int lineCount = property.isExpanded ? 5 : 1;
            return (EditorGUIUtility.singleLineHeight * lineCount)
                + (EditorGUIUtility.standardVerticalSpacing * (lineCount - 1));
        }

        private static GUIContent GetEntryLabel(SerializedProperty itemData, GUIContent fallback)
        {
            if (itemData.objectReferenceValue == null)
            {
                return fallback;
            }

            return new GUIContent(itemData.objectReferenceValue.name);
        }
    }
}
