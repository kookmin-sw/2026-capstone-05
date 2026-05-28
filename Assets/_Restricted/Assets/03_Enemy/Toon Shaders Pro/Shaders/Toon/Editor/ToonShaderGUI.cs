using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEditor.Rendering.Universal.ShaderGraph;

namespace ToonShadersPro.URP
{
    public class ToonShaderGUI : ShaderGUI
    {
        MaterialProperty baseColorProp = null;
        const string baseColorName = "_BaseColor";
        const string baseColorLabel = "Base Color";
        const string baseColorTooltip = "Albedo color applied to entire mesh.";

        MaterialProperty baseTexProp = null;
        const string baseTexName = "_BaseMap";
        const string baseTexLabel = "Base Texture";
        const string baseTexTooltip = "Albedo texture applied to entire mesh.";

        MaterialProperty useVertexColorsProp = null;
        const string useVertexColorsName = "_UseVertexColors";
        const string useVertexColorsLabel = "Use Vertex Colors?";
        const string useVertexColorsTooltip = "Should vertex colors be used to tint the object?";

        MaterialProperty workflowModeProp = null;
        const string workflowModeName = "_WorkflowMode";
        const string workflowModeLabel = "Workflow Mode";
        const string workflowModeTooltip = "Should the material use metallic or specular mode?";

        MaterialProperty useSecondThresholdProp = null;
        const string useSecondThresholdName = "_UseSecondThreshold";
        const string useSecondThresholdLabel = "Use Second Threshold?";
        const string useSecondThresholdTooltip = "Use a second set of thresholds for diffuse lights?";

        MaterialProperty lightTintProp = null;
        const string lightTintName = "_LightTint";
        const string lightTintLabel = "Light Tint";
        const string lightTintTooltip = "Tint applied to areas of mesh in light.";

        MaterialProperty middleTintProp = null;
        const string middleTintName = "_MiddleTint";
        const string middleTintLabel = "Middle Tint";
        const string middleTintTooltip = "Tint applied to areas of mesh in middle light.";

        MaterialProperty shadowTintProp = null;
        const string shadowTintName = "_ShadowTint";
        const string shadowTintLabel = "Shadow Tint";
        const string shadowTintTooltip = "Tint applied to areas of mesh in shade.";

        /*
        MaterialProperty ambientLightStrengthProp = null;
        const string ambientLightStrengthName = "_AmbientStrength";
        const string ambientLightStrengthLabel = "Ambient Light Strength";
        const string ambientLightStrengthTooltip = "Multiplier applied to ambient lighting amount.";
        */

        MaterialProperty shadowThresholdsProp = null;
        const string shadowThresholdsName = "_ShadowThresholds";
        const string shadowThresholdsLabel = "Shadow Thresholds";
        const string shadowThresholdsTooltip = "Thresholds which determine position of lighting cutoff for diffuse light.\n" +
            "\nThe internal lighting values run from 0 to 1, and then for the output brightness: " +
            "\nx = Raw shadow value where shadowed area ends." +
            "\ny = Raw shadow value where calculated lighting (no shadow) starts.";

        MaterialProperty diffuseThresholdsProp = null;
        const string diffuseThresholdsName = "_DiffuseThresholds";
        const string diffuseThresholdsLabel = "Diffuse Thresholds";
        const string diffuseThresholdsTooltip = "Thresholds which determine position of lighting cutoff for diffuse light.\n" + 
            "\nThe internal lighting values run from -1 to +1, and then for the output brightness: " + 
            "\nx = Raw light value where darkness ends." + 
            "\ny = Raw light value where full brightness starts." + 
            "\nValues between x and y use a smoothstep falloff to calculate final diffuse between fully shadow and fully lit.\n" + 
            "\nUsing the 'second threshold' option performs two thresholding steps and introduces a 'middle tint'.";

        MaterialProperty smoothnessProp = null;
        const string smoothnessName = "_Smoothness";
        MaterialProperty smoothnessMapProp = null;
        const string smoothnessMapName = "_SmoothnessMap";
        const string smoothnessLabel = "Smoothness";
        const string smoothnessTooltip = "How smooth the surface of the object should be." +
            "\n1 reprsents a highly polished surface. 0 represents a very rough or matter surface.";

        MaterialProperty convertFromRoughnessProp = null;
        const string convertFromRoughnessName = "_ConvertFromRoughness";
        const string convertFromRoughnessLabel = "Convert From Roughness";
        const string convertFromRoughnessTooltip = "Does this material use a roughness texture instead of smoothness?";

        MaterialProperty metallicProp = null;
        const string metallicName = "_Metallic";
        MaterialProperty metallicMapProp = null;
        const string metallicMapName = "_MetallicGlossMap";
        const string metallicLabel = "Metallic";
        const string metallicTooltip = "How metallic the surface should be." +
            "\n1 represents a metal, and 0 represents a non-metal." +
            "\nVery few objects in the real world use values around 0.5.";

        MaterialProperty specularBoostProp = null;
        const string specularBoostName = "_SpecularBoost";
        const string specularBoostLabel = "Specular Boost";
        const string specularBoostTooltip = "Extra multiplier applied to specular highlights.";

        MaterialProperty specularMapProp = null;
        const string specularMapName = "_SpecularMap";
        const string specularMapLabel = "Specular Map";
        const string specularMapTooltip = "Texture which controls specular strength on the surface. Black = no specular, white = full specular.";

        MaterialProperty giStrengthProp = null;
        const string giStrengthName = "_GIStrength";
        const string giStrengthLabel = "Global Illumination Strength";
        const string giStrengthTooltip = "Global Illuminantion strength modifier. 0 = no GI, 1 = full GI.";

        /*
        MaterialProperty specularStrengthProp = null;
        const string specularStrengthName = "_SpecularStrength";
        const string specularStrengthLabel = "Specular Strength";
        const string specularStrengthTooltip = "Multiplier for the specular map - also controls specular strength on the surface.";
        */

        MaterialProperty specularOffsetNoiseMapProp = null;
        const string specularOffsetNoiseMapName = "_SpecularOffsetNoiseMap";
        const string specularOffsetNoiseMapLabel = "Specular Offset Noise Map";
        const string specularOffsetNoiseMapTooltip = "Texture which controls the amount of noise offset that is applied to UVs for specular highlights.";

        MaterialProperty specularOffsetNoiseStrengthProp = null;
        const string specularOffsetNoiseStrengthName = "_SpecularOffsetNoiseStrength";
        const string specularOffsetNoiseStrengthLabel = "Specular Offset Noise Strength";
        const string specularOffsetNoiseStrengthTooltip = "Strength of the offset values output by the Specular Offset Noise Map.";

        MaterialProperty specularColorProp = null;
        const string specularColorName = "_SpecularColor";
        const string specularColorLabel = "Specular Color";
        const string specularColorTooltip = "Tint applied to the specular highlights.";

        /*
        MaterialProperty specularPowerProp = null;
        const string specularPowerName = "_SpecularPower";
        const string specularPowerLabel = "Glossiness";
        const string specularPowerTooltip = "Controls the size of the specular highlights.\nLarger value = smaller highlights.";
        */

        MaterialProperty specularThresholdsProp = null;
        const string specularThresholdsName = "_SpecularThresholds";
        const string specularThresholdsLabel = "Specular Thresholds";
        const string specularThresholdsTooltip = "Thresholds which determine position of lighting cutoff for specular light. The internal lighting values run from -1 to +1, and then for the output brightness: \nx = End of no highlight.\ny = Start of full highlight.\nValues between x and y use a smoothstep falloff to calculate final specular between no highlight and full highlight.";

        MaterialProperty rimColorProp = null;
        const string rimColorName = "_RimColor";
        const string rimColorLabel = "Rim Color";
        const string rimColorTooltip = "Tint applied to the rim lighting.";

        MaterialProperty rimThresholdsProp = null;
        const string rimThresholdsName = "_RimThresholds";
        const string rimThresholdsLabel = "Rim Thresholds";
        const string rimThresholdsTooltip = "Cutoff thresholds for rim lighting. Internal Fresnel lighting values run from 0 to 1, then: \nx = Value where rim lighting starts.\ny = Value where rim lighting is at full strength.\nValues between x and y use a smoothstep falloff to calculate final rim value between unlit and fully lit.";

        MaterialProperty rimExtensionProp = null;
        const string rimExtensionName = "_RimExtension";
        const string rimExtensionLabel = "Rim Extension";
        const string rimExtensionTooltip = "Extends the region of diffuse light where rim lighting is allowed to appear.";

        MaterialProperty normalMapProp = null;
        const string normalMapName = "_BumpMap";
        const string normalMapLabel = "Normal Map";
        const string normalMapTooltip = "Normal map modifies the surface normals for finer lighting detail.";

        MaterialProperty normalStrengthProp = null;
        const string normalStrengthName = "_BumpScale";
        const string normalStrengthLabel = "Normal Strength";
        const string normalStrengthTooltip = "How strongly the normal map influences lighting. 1 is \"standard\" strength.";

        MaterialProperty receiveShadowsProp = null;
        const string receiveShadowsName = "_ReceiveShadows";
        const string receiveShadowsLabel = "Receive Shadows";
        const string receiveShadowsTooltip = "Toggle whether to render realtime shadows from other objects influenced by the main light.";

        MaterialProperty alphaClipProp = null;
        const string alphaClipName = "_AlphaClip";
        const string alphaClipLabel = "Alpha Clip";
        const string alphaClipTooltip = "Should this object use alpha clipping?";

        MaterialProperty alphaClipThresholdProp = null;
        const string alphaClipThresholdName = "_Cutoff";
        const string alphaClipThresholdLabel = "Threshold";
        const string alphaClipThresholdTooltip = "Pixels with an alpha value below this threshold are culled.";

        private MaterialProperty cullProp = null;
        private const string cullName = "_Cull";
        private const string cullLabel = "Render Face";
        private const string cullTooltip = "Choose which sides of the mesh faces to render.";

        private MaterialProperty blendModeProp = null;
        private const string blendModeName = "_Blend";
        private const string blendModeLabel = "Blend Mode";
        private const string blendModeTooltip = "How Unity should blend this mesh with previously drawn objects.";

        private const string surfaceTypeLabel = "Surface Type";
        private const string surfaceTypeTooltip = "Whether the mesh is rendered opaque or transparent.";

        private static readonly string[] surfaceTypeNames = Enum.GetNames(typeof(SurfaceType));
        private static readonly string[] renderFaceNames = Enum.GetNames(typeof(RenderFace));
        private static readonly string[] blendModeNames = Enum.GetNames(typeof(BlendMode));

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

        private static GUIStyle _tinyLabelStyle;
        private static GUIStyle tinyLabelStyle
        {
            get
            {
                if (_tinyLabelStyle == null)
                {
                    _tinyLabelStyle = new GUIStyle(GUI.skin.label)
                    {
                        wordWrap = true,
                        fontSize = 10,
                        fontStyle = FontStyle.Normal,
                        alignment = TextAnchor.MiddleLeft
                    };
                }

                return _tinyLabelStyle;
            }
        }

        private enum SurfaceType
        {
            Opaque = 0,
            Transparent = 1
        }

        private enum RenderFace
        {
            Front = 2,
            Back = 1,
            Both = 0
        }

        private enum BlendMode
        {
            Alpha = 0,
            Premultiply = 1,
            Additive = 2,
            Multiply = 3
        }

        private SurfaceType surfaceType = SurfaceType.Opaque;
        private RenderFace renderFace = RenderFace.Front;
        private BlendMode blendMode = BlendMode.Alpha;
        private WorkflowMode workflowMode = WorkflowMode.Specular;

        private bool shouldRenderMetallic = false;
        private bool shouldRenderSpecular = false;

        protected readonly MaterialHeaderScopeList materialScopeList = new MaterialHeaderScopeList(uint.MaxValue);
        protected MaterialEditor materialEditor;
        private bool firstTimeOpen = true;

        private void FindProperties(MaterialProperty[] props)
        {
            baseColorProp = FindProperty(baseColorName, props, true);
            baseTexProp = FindProperty(baseTexName, props, true);
            useVertexColorsProp = FindProperty(useVertexColorsName, props, true);
            workflowModeProp = FindProperty(workflowModeName, props, false);
            useSecondThresholdProp = FindProperty(useSecondThresholdName, props, true);
            lightTintProp = FindProperty(lightTintName, props, true);
            middleTintProp = FindProperty(middleTintName, props, true);
            shadowTintProp = FindProperty(shadowTintName, props, true);
            //ambientLightStrengthProp = FindProperty(ambientLightStrengthName, props, true);
            shadowThresholdsProp = FindProperty(shadowThresholdsName, props, true);
            diffuseThresholdsProp = FindProperty(diffuseThresholdsName, props, true);
            smoothnessProp = FindProperty(smoothnessName, props, false);
            smoothnessMapProp = FindProperty(smoothnessMapName, props, false);
            convertFromRoughnessProp = FindProperty(convertFromRoughnessName, props, false);
            metallicProp = FindProperty(metallicName, props, false);
            metallicMapProp = FindProperty(metallicMapName, props, false);
            specularBoostProp = FindProperty(specularBoostName, props, false);
            specularMapProp = FindProperty(specularMapName, props, true);
            giStrengthProp = FindProperty(giStrengthName, props, true);
            //specularStrengthProp = FindProperty(specularStrengthName, props, true);
            specularOffsetNoiseMapProp = FindProperty(specularOffsetNoiseMapName, props, true);
            specularOffsetNoiseStrengthProp = FindProperty(specularOffsetNoiseStrengthName, props, true);
            specularColorProp = FindProperty(specularColorName, props, true);
            //specularPowerProp = FindProperty(specularPowerName, props, true);
            specularThresholdsProp = FindProperty(specularThresholdsName, props, true);
            rimColorProp = FindProperty(rimColorName, props, true);
            rimThresholdsProp = FindProperty(rimThresholdsName, props, true);
            rimExtensionProp = FindProperty(rimExtensionName, props, true);
            normalMapProp = FindProperty(normalMapName, props, true);
            normalStrengthProp = FindProperty(normalStrengthName, props, true);

            cullProp = FindProperty(cullName, props, true);
            blendModeProp = FindProperty(blendModeName, props, true);
            alphaClipProp = FindProperty(alphaClipName, props, true);
            alphaClipThresholdProp = FindProperty(alphaClipThresholdName, props, true);
            receiveShadowsProp = FindProperty(receiveShadowsName, props, true);
        }

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            if (materialEditor == null)
            {
                throw new ArgumentNullException("No MaterialEditor found (ToonShaderGUI).");
            }

            Material material = materialEditor.target as Material;
            this.materialEditor = materialEditor;

            FindProperties(properties);

            if (firstTimeOpen)
            {
                materialScopeList.RegisterHeaderScope(new GUIContent("Surface Options"), 1u << 0, DrawSurfaceOptions);
                materialScopeList.RegisterHeaderScope(new GUIContent("Toon Diffuse"), 1u << 1, DrawToonDiffuseProperties);
                materialScopeList.RegisterHeaderScope(new GUIContent("Toon Metallic/Specular"), 1u << 2, DrawToonMetallicProperties);
                materialScopeList.RegisterHeaderScope(new GUIContent("Toon Rim"), 1u << 3, DrawToonRimProperties);
                materialScopeList.RegisterHeaderScope(new GUIContent("Toon Normals"), 1u << 4, DrawToonNormalProperties);
                firstTimeOpen = false;
            }

            materialScopeList.DrawHeaders(materialEditor, material);
            materialEditor.serializedObject.ApplyModifiedProperties();
        }

        private void DrawSurfaceOptions(Material material)
        {
            surfaceType = (SurfaceType)material.GetFloat("_Surface");
            renderFace = (RenderFace)material.GetFloat("_Cull");
            blendMode = (BlendMode)material.GetFloat("_Blend");
            workflowMode = (WorkflowMode)material.GetFloat(workflowModeName);

            shouldRenderMetallic = (metallicMapProp != null) && (workflowMode == WorkflowMode.Metallic);
            shouldRenderSpecular = (specularMapProp != null) && (workflowMode == WorkflowMode.Specular);

            // Show the workflow mode only if it exists and there is actually a choice between both.
            if (workflowModeProp != null && metallicMapProp != null && specularMapProp != null)
            {
                EditorGUI.BeginChangeCheck();
                {
                    workflowMode = (WorkflowMode)EditorGUILayout.EnumPopup(new GUIContent(workflowModeLabel, workflowModeTooltip), workflowMode);
                }
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(material, "Modify Workflow Mode");
                    material.SetFloat(workflowModeName, (float)workflowMode);

                    if (workflowMode == WorkflowMode.Specular)
                    {
                        material.EnableKeyword("_SPECULAR_SETUP");
                    }
                    else
                    {
                        material.DisableKeyword("_SPECULAR_SETUP");
                    }

                    EditorUtility.SetDirty(material);
                }

                shouldRenderMetallic = (workflowMode == WorkflowMode.Metallic);
                shouldRenderSpecular = (workflowMode == WorkflowMode.Specular);
            }

            // Display opaque/transparent options.
            bool surfaceTypeChanged = false;
            EditorGUI.BeginChangeCheck();
            {
                surfaceType = (SurfaceType)EditorGUILayout.EnumPopup(new GUIContent(surfaceTypeLabel, surfaceTypeTooltip), surfaceType);
            }
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(material, "Modify Surface Type");
                surfaceTypeChanged = true;
            }

            // Display culling options.
            EditorGUI.BeginChangeCheck();
            {
                renderFace = (RenderFace)EditorGUILayout.EnumPopup(new GUIContent(cullLabel, cullTooltip), renderFace);
            }
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(material, "Modify Render Face");
                material.SetFloat("_Cull", (float)renderFace);
            }

            // Display blend mode options.
            bool blendModeChanged = false;
            if (surfaceType == SurfaceType.Transparent)
            {
                EditorGUI.BeginChangeCheck();
                {
                    blendMode = (BlendMode)EditorGUILayout.EnumPopup(new GUIContent(blendModeLabel, blendModeTooltip), blendMode);
                }

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(material, "Modify Blend Mode");
                    blendModeChanged = true;
                    material.SetFloat("_Blend", (float)blendMode);
                }
            }

            // Display alpha clip options.
            EditorGUI.BeginChangeCheck();
            {
                materialEditor.ShaderProperty(alphaClipProp, new GUIContent(alphaClipLabel, alphaClipTooltip));
            }
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(material, "Toggle Alpha Clip");
                surfaceTypeChanged = true;
            }

            bool alphaClip;
            bool useAlphaToMask = false;

            if (surfaceTypeChanged || blendModeChanged)
            {
                switch (surfaceType)
                {
                    case SurfaceType.Opaque:
                        {
                            material.SetOverrideTag("RenderType", "Opaque");
                            SetBlendMode(blendMode, surfaceType, material);
                            material.SetFloat("_ZWrite", 1);
                            material.SetFloat("_Surface", 0);

                            alphaClip = material.GetFloat(alphaClipName) >= 0.5f;
                            if (alphaClip)
                            {
                                material.EnableKeyword("_ALPHATEST_ON");
                                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                                material.SetOverrideTag("RenderType", "TransparentCutout");
                                useAlphaToMask = true;
                            }
                            else
                            {
                                material.DisableKeyword("_ALPHATEST_ON");
                                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
                                material.SetOverrideTag("RenderType", "Opaque");
                            }

                            break;
                        }
                    case SurfaceType.Transparent:
                        {
                            alphaClip = material.GetFloat(alphaClipName) >= 0.5f;
                            if (alphaClip)
                            {
                                material.EnableKeyword("_ALPHATEST_ON");
                            }
                            else
                            {
                                material.DisableKeyword("_ALPHATEST_ON");
                            }
                            material.SetOverrideTag("RenderType", "Transparent");
                            SetBlendMode(blendMode, surfaceType, material);
                            material.SetFloat("_ZWrite", 0);
                            material.SetFloat("_Surface", 1);

                            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                            break;
                        }
                }
                
                material.SetFloat("_AlphaToMask", useAlphaToMask ? 1.0f : 0.0f);
            }

            alphaClip = material.GetFloat(alphaClipName) >= 0.5f;
            if (alphaClip)
            {
                EditorGUI.indentLevel++;
                materialEditor.ShaderProperty(alphaClipThresholdProp, new GUIContent(alphaClipThresholdLabel, alphaClipThresholdTooltip));
                EditorGUI.indentLevel--;
            }

            bool receiveShadows;

            EditorGUI.BeginChangeCheck();
            {
                materialEditor.ShaderProperty(receiveShadowsProp, new GUIContent(receiveShadowsLabel, receiveShadowsTooltip));
            }
            if (EditorGUI.EndChangeCheck())
            {
                receiveShadows = material.GetFloat(receiveShadowsName) >= 0.5f;

                if (receiveShadows)
                {
                    material.EnableKeyword("_RECEIVE_SHADOWS_ON");
                }
                else
                {
                    material.DisableKeyword("_RECEIVE_SHADOWS_ON");
                }
            }

            receiveShadows = material.GetFloat(receiveShadowsName) >= 0.5f;

            if (receiveShadows)
            {
                Vector4 shadowThresholds = material.GetVector(shadowThresholdsName);

                EditorGUI.BeginChangeCheck();
                {
                    EditorGUILayout.LabelField(new GUIContent(shadowThresholdsLabel, shadowThresholdsTooltip));

                    EditorGUI.indentLevel++;
                    shadowThresholds.x = EditorGUILayout.Slider("Shadow ends:", shadowThresholds.x, 0.0f, 1.0f);
                    shadowThresholds.y = EditorGUILayout.Slider("Lit starts:", shadowThresholds.y, 0.0f, 1.0f);
                    EditorGUI.indentLevel--;
                }
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(material, "Set Shadow Thresholds");
                    material.SetVector(shadowThresholdsName, shadowThresholds);
                    EditorUtility.SetDirty(material);
                }
            }
        }

        private void DrawToonDiffuseProperties(Material material)
        {
            EditorGUI.BeginChangeCheck();
            {
                materialEditor.ShaderProperty(useSecondThresholdProp, new GUIContent(useSecondThresholdLabel, useSecondThresholdTooltip));
            }
            if (EditorGUI.EndChangeCheck())
            {
                if (material.GetFloat(useSecondThresholdName) > 0.5f)
                {
                    material.EnableKeyword("_USE_SECOND_THRESHOLD");
                }
                else
                {
                    material.DisableKeyword("_USE_SECOND_THRESHOLD");
                }
            }

            materialEditor.ShaderProperty(baseColorProp, new GUIContent(baseColorLabel, baseColorTooltip));
            materialEditor.ShaderProperty(baseTexProp, new GUIContent(baseTexLabel, baseTexTooltip));

            EditorGUI.BeginChangeCheck();
            {
                materialEditor.ShaderProperty(useVertexColorsProp, new GUIContent(useVertexColorsLabel, useVertexColorsTooltip));
            }
            if (EditorGUI.EndChangeCheck())
            {
                if (material.GetFloat(useVertexColorsName) > 0.5f)
                {
                    material.EnableKeyword("_USE_VERTEX_COLORS");
                }
                else
                {
                    material.DisableKeyword("_USE_VERTEX_COLORS");
                }
            }

            materialEditor.ShaderProperty(lightTintProp, new GUIContent(lightTintLabel, lightTintTooltip));

            if (material.GetFloat(useSecondThresholdName) > 0.5f)
            {
                materialEditor.ShaderProperty(middleTintProp, new GUIContent(middleTintLabel, middleTintTooltip));
            }

            materialEditor.ShaderProperty(shadowTintProp, new GUIContent(shadowTintLabel, shadowTintTooltip));
            //materialEditor.ShaderProperty(ambientLightStrengthProp, new GUIContent(ambientLightStrengthLabel, ambientLightStrengthTooltip));

            if (material.GetFloat(useSecondThresholdName) > 0.5f)
            {
                Vector4 diffuseThresholds = material.GetVector(diffuseThresholdsName);

                EditorGUI.BeginChangeCheck();
                {
                    EditorGUILayout.LabelField(new GUIContent(diffuseThresholdsLabel, diffuseThresholdsTooltip));
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField("First Diffuse Threshold", tinyLabelStyle);
                    EditorGUI.indentLevel++;
                    diffuseThresholds.x = EditorGUILayout.Slider("Dark ends:", diffuseThresholds.x, -1.0f, 1.0f);
                    diffuseThresholds.y = EditorGUILayout.Slider("Midtone starts:", diffuseThresholds.y, -1.0f, 1.0f);
                    EditorGUI.indentLevel--;
                    EditorGUILayout.LabelField("Second Diffuse Threshold", tinyLabelStyle);
                    EditorGUI.indentLevel++;
                    diffuseThresholds.z = EditorGUILayout.Slider("Midtone ends:", diffuseThresholds.z, -1.0f, 1.0f);
                    diffuseThresholds.w = EditorGUILayout.Slider("Light starts:", diffuseThresholds.w, -1.0f, 1.0f);
                    EditorGUI.indentLevel--;
                    EditorGUI.indentLevel--;
                }
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(material, "Set Diffuse Thresholds");
                    material.SetVector(diffuseThresholdsName, diffuseThresholds);
                    EditorUtility.SetDirty(material);
                }
            }
            else
            {
                Vector2 diffuseThresholds = material.GetVector(diffuseThresholdsName);

                EditorGUI.BeginChangeCheck();
                {
                    EditorGUILayout.LabelField(new GUIContent(diffuseThresholdsLabel, diffuseThresholdsTooltip));
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField("First Diffuse Threshold", tinyLabelStyle);
                    EditorGUI.indentLevel++;
                    diffuseThresholds.x = EditorGUILayout.Slider("Dark ends:", diffuseThresholds.x, -1.0f, 1.0f);
                    diffuseThresholds.y = EditorGUILayout.Slider("Light starts:", diffuseThresholds.y, -1.0f, 1.0f);
                    EditorGUI.indentLevel--;
                    EditorGUI.indentLevel--;
                }
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(material, "Set Diffuse Thresholds");
                    material.SetVector(diffuseThresholdsName, diffuseThresholds);
                    EditorUtility.SetDirty(material);
                }
            }
        }

        private void DrawToonMetallicProperties(Material material)
        {
            if(shouldRenderMetallic)
            {
                materialEditor.TexturePropertySingleLine(new GUIContent(metallicLabel, metallicTooltip),
                    metallicMapProp, metallicProp);
            }
            
            if(shouldRenderSpecular)
            {
                materialEditor.TexturePropertySingleLine(new GUIContent(specularColorLabel, specularColorTooltip), specularMapProp, specularColorProp);
            }

            materialEditor.TexturePropertySingleLine(new GUIContent(smoothnessLabel, smoothnessTooltip),
                smoothnessMapProp, smoothnessProp);

            bool convertFromRough = material.GetFloat(convertFromRoughnessName) > 0.5f;

            EditorGUI.BeginChangeCheck();
            {
                convertFromRough = EditorGUILayout.Toggle(new GUIContent(convertFromRoughnessLabel, convertFromRoughnessTooltip), convertFromRough);
            }
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(material, "Modify Convert From Rough");
                material.SetFloat(convertFromRoughnessName, convertFromRough ? 1.0f : 0.0f);
                EditorUtility.SetDirty(material);
            }

            materialEditor.ShaderProperty(specularOffsetNoiseMapProp, new GUIContent(specularOffsetNoiseMapLabel, specularOffsetNoiseMapTooltip));
            materialEditor.ShaderProperty(specularOffsetNoiseStrengthProp, new GUIContent(specularOffsetNoiseStrengthLabel, specularOffsetNoiseStrengthTooltip));
            materialEditor.ShaderProperty(specularBoostProp, new GUIContent(specularBoostLabel, specularBoostTooltip));

            Vector2 specularThresholds = material.GetVector(specularThresholdsName);

            EditorGUI.BeginChangeCheck();
            {
                EditorGUILayout.LabelField(new GUIContent(specularThresholdsLabel, specularThresholdsTooltip));
                EditorGUI.indentLevel++;
                specularThresholds.x = EditorGUILayout.Slider("Specular Start:", specularThresholds.x, 0.0f, 10.0f);
                specularThresholds.y = EditorGUILayout.Slider("Specular Full:", specularThresholds.y, 0.0f, 10.0f);
                EditorGUI.indentLevel--;
            }
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(material, "Set Specular Thresholds");
                material.SetVector(specularThresholdsName, specularThresholds);
                EditorUtility.SetDirty(material);
            }

            materialEditor.ShaderProperty(giStrengthProp, new GUIContent(giStrengthLabel, giStrengthTooltip));
        }

        private void DrawToonRimProperties(Material material)
        {
            materialEditor.ShaderProperty(rimColorProp, new GUIContent(rimColorLabel, rimColorTooltip));

            Vector2 rimThresholds = material.GetVector(rimThresholdsName);

            EditorGUI.BeginChangeCheck();
            {
                EditorGUILayout.LabelField(new GUIContent(rimThresholdsLabel, rimThresholdsTooltip));
                EditorGUI.indentLevel++;
                rimThresholds.x = EditorGUILayout.Slider("Rim Start:", rimThresholds.x, -1.0f, 1.0f);
                rimThresholds.y = EditorGUILayout.Slider("Rim Full:", rimThresholds.y, -1.0f, 1.0f);
                EditorGUI.indentLevel--;
            }
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(material, "Set Rim Thresholds");
                material.SetVector(rimThresholdsName, rimThresholds);
                EditorUtility.SetDirty(material);
            }

            materialEditor.ShaderProperty(rimExtensionProp, new GUIContent(rimExtensionLabel, rimExtensionTooltip));
        }

        private void DrawToonNormalProperties(Material material)
        {
            materialEditor.ShaderProperty(normalMapProp, new GUIContent(normalMapLabel, normalMapTooltip));
            materialEditor.ShaderProperty(normalStrengthProp, new GUIContent(normalStrengthLabel, normalStrengthTooltip));
        }

        private void SetBlendMode(BlendMode blendMode, SurfaceType surfaceType, Material material)
        {
            var srcBlendRGB = UnityEngine.Rendering.BlendMode.One;
            var dstBlendRGB = UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha;
            var srcBlendA = UnityEngine.Rendering.BlendMode.One;
            var dstBlendA = UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha;

            if (surfaceType == SurfaceType.Transparent)
            {
                switch (blendMode)
                {
                    case BlendMode.Alpha:
                        {
                            srcBlendRGB = UnityEngine.Rendering.BlendMode.SrcAlpha;
                            dstBlendRGB = UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha;
                            srcBlendA = UnityEngine.Rendering.BlendMode.One;
                            dstBlendA = dstBlendRGB;
                            break;
                        }
                    case BlendMode.Premultiply:
                        {
                            srcBlendRGB = UnityEngine.Rendering.BlendMode.One;
                            dstBlendRGB = UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha;
                            srcBlendA = srcBlendRGB;
                            dstBlendA = dstBlendRGB;
                            break;
                        }
                    case BlendMode.Additive:
                        {
                            srcBlendRGB = UnityEngine.Rendering.BlendMode.SrcAlpha;
                            dstBlendRGB = UnityEngine.Rendering.BlendMode.One;
                            srcBlendA = UnityEngine.Rendering.BlendMode.One;
                            dstBlendA = dstBlendRGB;
                            break;
                        }
                    case BlendMode.Multiply:
                        {
                            srcBlendRGB = UnityEngine.Rendering.BlendMode.DstColor;
                            dstBlendRGB = UnityEngine.Rendering.BlendMode.Zero;
                            srcBlendA = UnityEngine.Rendering.BlendMode.Zero;
                            dstBlendA = UnityEngine.Rendering.BlendMode.One;
                            break;
                        }
                }
            }
            else
            {
                srcBlendRGB = UnityEngine.Rendering.BlendMode.One;
                dstBlendRGB = UnityEngine.Rendering.BlendMode.Zero;
                srcBlendA = UnityEngine.Rendering.BlendMode.One;
                dstBlendA = UnityEngine.Rendering.BlendMode.Zero;
            }

            material.SetFloat("_SrcBlend", (float)srcBlendRGB);
            material.SetFloat("_DstBlend", (float)dstBlendRGB);
            material.SetFloat("_SrcBlendAlpha", (float)srcBlendA);
            material.SetFloat("_DstBlendAlpha", (float)dstBlendA);
        }
    }
}
