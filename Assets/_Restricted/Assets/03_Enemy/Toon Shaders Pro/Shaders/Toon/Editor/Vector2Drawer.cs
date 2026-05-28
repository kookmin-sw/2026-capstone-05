using UnityEngine;
using UnityEditor;

namespace ToonShadersPro.URP
{
    // Displays a Vector2 shader property using only two input fields.
    public class Vector2Drawer : MaterialPropertyDrawer
    {
        protected float min;
        protected float max;
        protected bool usesArguments = false;

        public Vector2Drawer()
        {
            usesArguments = false;
        }

        public Vector2Drawer(float min, float max)
        {
            this.min = min;
            this.max = max;
            usesArguments = true;
        }

        public Vector2Drawer(float min, float max, float offset)
        {
            this.min = min - offset;
            this.max = max - offset;
            usesArguments = true;
        }

        public override void OnGUI(Rect position, MaterialProperty prop, GUIContent label, MaterialEditor editor)
        {
            if (prop.propertyType == UnityEngine.Rendering.ShaderPropertyType.Vector)
            {
                EditorGUI.BeginChangeCheck();
                EditorGUI.showMixedValue = prop.hasMixedValue;

                if (usesArguments)
                {
                    var labelText = label.text;
                    position.height = EditorGUIUtility.singleLineHeight;

                    var bottomSliderPosition = new Rect(position);
                    bottomSliderPosition.position += new Vector2(0.0f, EditorGUIUtility.singleLineHeight);

                    EditorGUI.PrefixLabel(position, label);

                    EditorGUI.indentLevel++;

                    label.text = "x";
                    position.position += new Vector2(0.0f, EditorGUIUtility.singleLineHeight);
                    float x = EditorGUI.Slider(position, label, prop.vectorValue.x, min, max);

                    label.text = "y";
                    position.position += new Vector2(0.0f, EditorGUIUtility.singleLineHeight);
                    float y = EditorGUI.Slider(position, label, prop.vectorValue.y, min, max);

                    EditorGUI.indentLevel--;

                    if (EditorGUI.EndChangeCheck())
                    {
                        prop.vectorValue = new Vector2(x, y);
                    }
                }
                else
                {
                    Vector4 vec = EditorGUI.Vector2Field(position, label, prop.vectorValue);

                    if (EditorGUI.EndChangeCheck())
                    {
                        prop.vectorValue = vec;
                    }
                }
            }
            else
            {
                editor.DefaultShaderProperty(prop, label.text);
            }
        }

        public override float GetPropertyHeight(MaterialProperty prop, string label, MaterialEditor editor)
        {
            if (usesArguments)
            {
                return base.GetPropertyHeight(prop, label, editor) * 3 + 10;
            }

            return base.GetPropertyHeight(prop, label, editor);
        }
    }
}
