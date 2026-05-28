using UnityEngine;
using UnityEditor;
using UnityEditor.Rendering;

namespace ToonShadersPro.URP
{
#if UNITY_2022_2_OR_NEWER
    [CustomEditor(typeof(OutlineSettings))]
#else
    [VolumeComponentEditor(typeof(OutlineSettings))]
#endif
    public class OutlineEditor : VolumeComponentEditor
    {
        SerializedDataParameter renderPassEvent;
        SerializedDataParameter outlineType;
        SerializedDataParameter outlineColor;
        SerializedDataParameter colorSensitivity;
        SerializedDataParameter colorStrength;
        SerializedDataParameter depthSensitivity;
        SerializedDataParameter depthStrength;
        SerializedDataParameter normalSensitivity;
        SerializedDataParameter normalStrength;
        SerializedDataParameter depthThreshold;
        SerializedDataParameter objectMask;
        SerializedDataParameter maskDrawingMode;
        SerializedDataParameter lightModes;
        SerializedDataParameter renderQueue;
        SerializedDataParameter maskIgnoreDepth;
        SerializedDataParameter useDepthNormals;
        SerializedDataParameter maskedOutlineThickness;
        SerializedDataParameter maskedOutlineSmoothing;
        SerializedDataParameter outlineDrawSides;
        SerializedDataParameter outlineFadeStart;
        SerializedDataParameter outlineFadeEnd;
        SerializedDataParameter outlineThickness;
        SerializedDataParameter outlineTransparency;
        SerializedDataParameter outlineLighting;
        SerializedDataParameter flipOutlineDirection;
        SerializedDataParameter outlineMinLighting;
        SerializedDataParameter useNoiseOffsets;
        SerializedDataParameter noiseScale;
        SerializedDataParameter noiseOffset;
        SerializedDataParameter noiseStrength;

        private static GUIStyle _headerStyle;
        private static GUIStyle headerStyle
        {
            get
            {
                if (_headerStyle == null)
                {
                    _headerStyle = new GUIStyle(GUI.skin.label)
                    {
                        wordWrap = true,
                        fontSize = 14,
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleLeft
                    };
                }

                return _headerStyle;
            }
        }

        public override void OnEnable()
        {
            var o = new PropertyFetcher<OutlineSettings>(serializedObject);
            renderPassEvent = Unpack(o.Find(x => x.renderPassEvent));
            outlineType = Unpack(o.Find(x => x.outlineType));
            outlineColor = Unpack(o.Find(x => x.outlineColor));
            colorSensitivity = Unpack(o.Find(x => x.colorSensitivity));
            colorStrength = Unpack(o.Find(x => x.colorStrength));
            depthSensitivity = Unpack(o.Find(x => x.depthSensitivity));
            depthStrength = Unpack(o.Find(x => x.depthStrength));
            normalSensitivity = Unpack(o.Find(x => x.normalSensitivity));
            normalStrength = Unpack(o.Find(x => x.normalStrength));
            depthThreshold = Unpack(o.Find(x => x.depthThreshold));
            objectMask = Unpack(o.Find(x => x.objectMask));
            maskDrawingMode = Unpack(o.Find(x => x.maskDrawingMode));
            lightModes = Unpack(o.Find(x => x.lightModes));
            renderQueue = Unpack(o.Find(x => x.renderQueue));
            maskIgnoreDepth = Unpack(o.Find(x => x.maskIgnoreDepth));
            useDepthNormals = Unpack(o.Find(x => x.useDepthNormals));
            maskedOutlineThickness = Unpack(o.Find(x => x.maskedOutlineThickness));
            maskedOutlineSmoothing = Unpack(o.Find(x => x.maskedOutlineSmoothing));
            outlineDrawSides = Unpack(o.Find(x => x.outlineDrawSides));
            outlineFadeStart = Unpack(o.Find(x => x.outlineFadeStart));
            outlineFadeEnd = Unpack(o.Find(x => x.outlineFadeEnd));
            outlineThickness = Unpack(o.Find(x => x.outlineThickness));
            outlineTransparency = Unpack(o.Find(x => x.outlineTransparency));
            outlineLighting = Unpack(o.Find(x => x.outlineLighting));
            flipOutlineDirection = Unpack(o.Find(x => x.flipOutlineDirection));
            outlineMinLighting = Unpack(o.Find(x => x.outlineMinLighting));
            useNoiseOffsets = Unpack(o.Find(x => x.useNoiseOffsets));
            noiseScale = Unpack(o.Find(x => x.noiseScale));
            noiseOffset = Unpack(o.Find(x => x.noiseOffset));
            noiseStrength = Unpack(o.Find(x => x.noiseStrength));
        }

        private void ShowMaskDrawingMode()
        {
            PropertyField(maskDrawingMode);
            
#if !UNITY_6000_3_OR_NEWER
            if (maskDrawingMode.value.GetEnumValue<MaskDrawingMode>() == MaskDrawingMode.RSUV)
            {
                EditorGUILayout.HelpBox("The Renderer Shader User Value was added in Unity 6.3. This mask will be empty.", MessageType.Error);
            }
#endif
        }

        public override void OnInspectorGUI()
        {
            if (!ToonShaderUtility.CheckEffectEnabled<OutlineFeature>())
            {
                EditorGUILayout.HelpBox("The Outlines effect must be added to your renderer's Renderer Features list.", MessageType.Error);
                if (GUILayout.Button("Add Outlines Renderer Feature"))
                {
                    ToonShaderUtility.AddEffectToPipelineAsset<OutlineFeature>();
                }
            }

            EditorGUILayout.LabelField("Rendering Options", headerStyle);

            PropertyField(renderPassEvent);
            PropertyField(outlineType);

            OutlineType activeOutlineType = outlineType.value.GetEnumValue<OutlineType>();

            if (activeOutlineType != OutlineType.NoOutlines)
            {
                GUILayout.Space(10);
                EditorGUILayout.LabelField("Outline Options", headerStyle);
                PropertyField(outlineColor);

                if (activeOutlineType != OutlineType.HullOutlines &&
                    activeOutlineType != OutlineType.DebugOutlineMask)
                {
                    PropertyField(useNoiseOffsets);

                    if (useNoiseOffsets.value.boolValue)
                    {
                        PropertyField(noiseScale);
                        PropertyField(noiseOffset);
                        PropertyField(noiseStrength);
                    }
                }
            }

            if(activeOutlineType == OutlineType.DepthNormalOutlines)
            {
                GUILayout.Space(10);
                EditorGUILayout.LabelField("Depth-Normals Options", headerStyle);
                PropertyField(colorSensitivity);
                PropertyField(colorStrength);
                PropertyField(depthSensitivity);
                PropertyField(depthStrength);
                PropertyField(normalSensitivity);
                PropertyField(normalStrength);
                PropertyField(depthThreshold);
            }
            else if(activeOutlineType == OutlineType.HighQualityObjectMaskOutlines)
            {
                GUILayout.Space(10);
                EditorGUILayout.LabelField("Object Mask Options", headerStyle);
                PropertyField(objectMask);
                PropertyField(maskIgnoreDepth);
                ShowMaskDrawingMode();
                PropertyField(lightModes);
                PropertyField(renderQueue);

                GUILayout.Space(10);
                EditorGUILayout.LabelField("Mask Outline Options", headerStyle);
                PropertyField(maskedOutlineThickness);
                PropertyField(maskedOutlineSmoothing);
                PropertyField(outlineDrawSides);
                PropertyField(outlineFadeStart);
                PropertyField(outlineFadeEnd);

                GUILayout.Space(10);
                EditorGUILayout.LabelField("Normals Options", headerStyle);
                PropertyField(useDepthNormals);

                if(useDepthNormals.value.boolValue)
                {
                    PropertyField(normalSensitivity);
                    PropertyField(normalStrength);
                }
            }
            else if(activeOutlineType == OutlineType.PixelWidthObjectMaskOutlines)
            {
                GUILayout.Space(10);
                EditorGUILayout.LabelField("Object Mask Options", headerStyle);
                PropertyField(objectMask);
                PropertyField(maskIgnoreDepth);
                ShowMaskDrawingMode();
                PropertyField(lightModes);
                PropertyField(renderQueue);
            }
            else if(activeOutlineType == OutlineType.HullOutlines)
            {
                GUILayout.Space(10);
                EditorGUILayout.LabelField("Object Mask Options", headerStyle);
                PropertyField(objectMask);
                PropertyField(maskIgnoreDepth);
                ShowMaskDrawingMode();
                PropertyField(lightModes);
                PropertyField(renderQueue);

                GUILayout.Space(10);
                EditorGUILayout.LabelField("Hull Outline Options", headerStyle);
                PropertyField(outlineThickness);
                PropertyField(outlineTransparency);
                PropertyField(outlineLighting);

                if(outlineLighting.value.boolValue)
                {
                    PropertyField(flipOutlineDirection);
                    PropertyField(outlineMinLighting);
                }
            }
            else if(activeOutlineType == OutlineType.DebugOutlineMask)
            {
                GUILayout.Space(10);
                EditorGUILayout.LabelField("Object Mask Options", headerStyle);
                PropertyField(objectMask);
                PropertyField(maskIgnoreDepth);
                ShowMaskDrawingMode();
                PropertyField(lightModes);
                PropertyField(renderQueue);
            }
        }

#if UNITY_2021_2_OR_NEWER
        public override GUIContent GetDisplayTitle()
        {
            return new GUIContent("Outlines");
        }
#else
    public override string GetDisplayTitle()
    {
        return "Outlines";
    }
#endif
    }
}
