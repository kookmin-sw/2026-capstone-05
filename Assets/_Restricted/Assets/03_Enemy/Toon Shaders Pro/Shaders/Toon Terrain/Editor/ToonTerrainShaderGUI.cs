using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEditor;
using UnityEditor.Rendering;

namespace ToonShadersPro.URP
{
    internal class ToonTerrainShaderGUI : UnityEditor.ShaderGUI, ITerrainLayerCustomUI
    {
        private class StylesLayer
        {
            public readonly GUIContent warningHeightBasedBlending = new GUIContent("Height-based blending is disabled if you have more than four TerrainLayer materials!");

            public readonly GUIContent enableHeightBlend = new GUIContent("Enable Height-based Blend", "Blend terrain layers based on height values.");
            public readonly GUIContent heightTransition = new GUIContent("Height Transition", "Size in world units of the smooth transition between layers.");
            public readonly GUIContent enableInstancedPerPixelNormal = new GUIContent("Enable Per-pixel Normal", "Enable per-pixel normal when the terrain uses instanced rendering.");

            public readonly GUIContent diffuseTexture = new GUIContent("Diffuse");
            public readonly GUIContent colorTint = new GUIContent("Color Tint");
            public readonly GUIContent opacityAsDensity = new GUIContent("Opacity as Density", "Enable Density Blend (if unchecked, opacity is used as Smoothness)");
            public readonly GUIContent normalMapTexture = new GUIContent("Normal Map");
            public readonly GUIContent normalScale = new GUIContent("Normal Scale");
            public readonly GUIContent maskMapTexture = new GUIContent("Mask", "R: Metallic\nG: AO\nB: Height\nA: Smoothness");
            public readonly GUIContent maskMapTextureWithoutHeight = new GUIContent("Mask Map", "R: Metallic\nG: AO\nA: Smoothness");
            public readonly GUIContent channelRemapping = new GUIContent("Channel Remapping");
            public readonly GUIContent defaultValues = new GUIContent("Channel Default Values");
            public readonly GUIContent metallic = new GUIContent("R: Metallic");
            public readonly GUIContent ao = new GUIContent("G: AO");
            public readonly GUIContent height = new GUIContent("B: Height");
            public readonly GUIContent heightParametrization = new GUIContent("Parametrization");
            public readonly GUIContent heightAmplitude = new GUIContent("Amplitude (cm)");
            public readonly GUIContent heightBase = new GUIContent("Base (cm)");
            public readonly GUIContent heightMin = new GUIContent("Min (cm)");
            public readonly GUIContent heightMax = new GUIContent("Max (cm)");
            public readonly GUIContent heightCm = new GUIContent("B: Height (cm)");
            public readonly GUIContent smoothness = new GUIContent("A: Smoothness");
        }

        MaterialProperty useSecondThresholdProp = null;
        const string useSecondThresholdName = "_UseSecondThreshold";
        const string useSecondThresholdLabel = "Use Second Threshold?";
        const string useSecondThresholdTooltip = "Use a second set of thresholds for diffuse lights?";

        MaterialProperty useStochasticTexturingProp = null;
        const string useStochasticTexturingName = "_UseStochasticTexturing";
        const string useStochasticTexturingLabel = "Use Stochastic Texturing?";
        const string useStochasticTexturingTooltip = "Use texture offsets and multiple samples to disrupt visible tiling?\n" +
            "\nWarning: This setting is expensive, as it uses three texture samples for each albedo and normal texture assigned to the terrain.";

        /*
        MaterialProperty specular0Prop = null;
        const string specular0Name = "_SpecularMap0";
        const string specular0Label = "Specular Map 0";
        const string specular0Tooltip = "Specular map for terrain texture slot 0.";

        MaterialProperty specular1Prop = null;
        const string specular1Name = "_SpecularMap1";
        const string specular1Label = "Specular Map 1";
        const string specular1Tooltip = "Specular map for terrain texture slot 1.";

        MaterialProperty specular2Prop = null;
        const string specular2Name = "_SpecularMap2";
        const string specular2Label = "Specular Map 2";
        const string specular2Tooltip = "Specular map for terrain texture slot 2.";

        MaterialProperty specular3Prop = null;
        const string specular3Name = "_SpecularMap3";
        const string specular3Label = "Specular Map 3";
        const string specular3Tooltip = "Specular map for terrain texture slot 3.";
        */

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

        MaterialProperty ambientLightStrengthProp = null;
        const string ambientLightStrengthName = "_AmbientStrength";
        const string ambientLightStrengthLabel = "Ambient Light Strength";
        const string ambientLightStrengthTooltip = "Multiplier applied to ambient lighting amount.";

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

        MaterialProperty specularBoostProp = null;
        const string specularBoostName = "_SpecularBoost";
        const string specularBoostLabel = "Specular Boost";
        const string specularBoostTooltip = "Extra multiplier applied to specular highlights.";

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

        MaterialProperty receiveShadowsProp = null;
        const string receiveShadowsName = "_ReceiveShadows";
        const string receiveShadowsLabel = "Receive Shadows";
        const string receiveShadowsTooltip = "Toggle whether to render realtime shadows from other objects influenced by the main light.";

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

        protected readonly MaterialHeaderScopeList materialScopeList = new MaterialHeaderScopeList(uint.MaxValue);
        protected MaterialEditor materialEditor;
        private bool firstTimeOpen = true;

        static StylesLayer s_Styles = null;
        private static StylesLayer styles { get { if (s_Styles == null) s_Styles = new StylesLayer(); return s_Styles; } }

        public ToonTerrainShaderGUI()
        {
        }

        // Height blend params
        MaterialProperty enableHeightBlend = null;
        const string kEnableHeightBlend = "_EnableHeightBlend";

        MaterialProperty heightTransition = null;
        const string kHeightTransition = "_HeightTransition";

        // Per-pixel Normal (while instancing)
        MaterialProperty enableInstancedPerPixelNormal = null;
        const string kEnableInstancedPerPixelNormal = "_EnableInstancedPerPixelNormal";

        private bool m_ShowChannelRemapping = false;
        enum HeightParametrization
        {
            Amplitude,
            MinMax
        };
        private HeightParametrization m_HeightParametrization = HeightParametrization.Amplitude;

        private static bool DoesTerrainUseMaskMaps(TerrainLayer[] terrainLayers)
        {
            for (int i = 0; i < terrainLayers.Length; ++i)
            {
                if (terrainLayers[i].maskMapTexture != null)
                    return true;
            }
            return false;
        }

        protected void FindMaterialProperties(MaterialProperty[] props)
        {
            enableHeightBlend = FindProperty(kEnableHeightBlend, props, false);
            heightTransition = FindProperty(kHeightTransition, props, false);
            enableInstancedPerPixelNormal = FindProperty(kEnableInstancedPerPixelNormal, props, false);

            receiveShadowsProp = FindProperty(receiveShadowsName, props, true);
            useSecondThresholdProp = FindProperty(useSecondThresholdName, props, true);
            useStochasticTexturingProp = FindProperty(useStochasticTexturingName, props, true);

            /*
            specular0Prop = FindProperty(specular0Name, props, true);
            specular1Prop = FindProperty(specular1Name, props, true);
            specular2Prop = FindProperty(specular2Name, props, true);
            specular3Prop = FindProperty(specular3Name, props, true);
            */

            lightTintProp = FindProperty(lightTintName, props, true);
            middleTintProp = FindProperty(middleTintName, props, true);
            shadowTintProp = FindProperty(shadowTintName, props, true);
            ambientLightStrengthProp = FindProperty(ambientLightStrengthName, props, true);
            shadowThresholdsProp = FindProperty(shadowThresholdsName, props, true);
            diffuseThresholdsProp = FindProperty(diffuseThresholdsName, props, true);
            specularBoostProp = FindProperty(specularBoostName, props, false);
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
        }

        static public void SetupMaterialKeywords(Material material)
        {
            bool enableHeightBlend = (material.HasProperty(kEnableHeightBlend) && material.GetFloat(kEnableHeightBlend) > 0);
            CoreUtils.SetKeyword(material, "_TERRAIN_BLEND_HEIGHT", enableHeightBlend);

            bool enableInstancedPerPixelNormal = material.GetFloat(kEnableInstancedPerPixelNormal) > 0.0f;
            CoreUtils.SetKeyword(material, "_TERRAIN_INSTANCED_PERPIXEL_NORMAL", enableInstancedPerPixelNormal);
        }

        static public bool TextureHasAlpha(Texture2D inTex)
        {
            if (inTex != null)
            {
                return GraphicsFormatUtility.HasAlphaChannel(GraphicsFormatUtility.GetGraphicsFormat(inTex.format, true));
            }
            return false;
        }

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            if (materialEditor == null)
            {
                throw new ArgumentNullException("No MaterialEditor found (ToonTerrainShaderGUI).");
            }

            Material material = materialEditor.target as Material;
            this.materialEditor = materialEditor;

            FindMaterialProperties(properties);

            if (firstTimeOpen)
            {
                materialScopeList.RegisterHeaderScope(new GUIContent("Terrain Options"), 1u << 0, DrawTerrainOptions);
                materialScopeList.RegisterHeaderScope(new GUIContent("Surface Options"), 1u << 1, DrawSurfaceOptions);
                materialScopeList.RegisterHeaderScope(new GUIContent("Toon Diffuse"), 1u << 2, DrawToonDiffuseProperties);
                materialScopeList.RegisterHeaderScope(new GUIContent("Toon Specular"), 1u << 3, DrawToonMetallicProperties);
                materialScopeList.RegisterHeaderScope(new GUIContent("Toon Rim"), 1u << 4, DrawToonRimProperties);
                firstTimeOpen = false;
            }

            materialScopeList.DrawHeaders(materialEditor, material);
            materialEditor.serializedObject.ApplyModifiedProperties();
        }

        private void DrawTerrainOptions(Material material)
        {
            EditorGUI.indentLevel--;

            bool optionsChanged = false;
            EditorGUI.BeginChangeCheck();
            {
                if (enableHeightBlend != null)
                {
                    EditorGUI.indentLevel++;
                    materialEditor.ShaderProperty(enableHeightBlend, styles.enableHeightBlend);
                    if (enableHeightBlend.floatValue > 0)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.HelpBox(styles.warningHeightBasedBlending.text, MessageType.Info);
                        materialEditor.ShaderProperty(heightTransition, styles.heightTransition);
                        EditorGUI.indentLevel--;
                    }
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.Space();
            }
            if (EditorGUI.EndChangeCheck())
            {
                optionsChanged = true;
            }

            bool enablePerPixelNormalChanged = false;

            // Since Instanced Per-pixel normal is actually dependent on instancing enabled or not, it is not
            // important to check it in the GUI.  The shader will make sure it is enabled/disabled properly.s
            if (enableInstancedPerPixelNormal != null)
            {
                EditorGUI.indentLevel++;
                EditorGUI.BeginChangeCheck();
                materialEditor.ShaderProperty(enableInstancedPerPixelNormal, styles.enableInstancedPerPixelNormal);
                enablePerPixelNormalChanged = EditorGUI.EndChangeCheck();
                EditorGUI.indentLevel--;
            }

            if (optionsChanged || enablePerPixelNormalChanged)
            {
                foreach (var obj in materialEditor.targets)
                {
                    SetupMaterialKeywords((Material)obj);
                }
            }

            EditorGUI.indentLevel++;
        }

        private void DrawSurfaceOptions(Material material)
        {
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
                    material.SetVector(shadowThresholdsName, shadowThresholds);
                }
            }
        }

        private void DrawToonDiffuseProperties(Material material)
        {
            EditorGUI.BeginChangeCheck();
            {
                materialEditor.ShaderProperty(useStochasticTexturingProp, new GUIContent(useStochasticTexturingLabel, useStochasticTexturingTooltip));
            }
            if (EditorGUI.EndChangeCheck())
            {
                bool useStochastic = material.GetFloat(useStochasticTexturingName) >= 0.5f;

                if (useStochastic)
                {
                    material.EnableKeyword("_USE_STOCHASTIC_TEXTURING");
                }
                else
                {
                    material.DisableKeyword("_USE_STOCHASTIC_TEXTURING");
                }
            }

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

            materialEditor.ShaderProperty(lightTintProp, new GUIContent(lightTintLabel, lightTintTooltip));

            if (material.GetFloat(useSecondThresholdName) > 0.5f)
            {
                materialEditor.ShaderProperty(middleTintProp, new GUIContent(middleTintLabel, middleTintTooltip));
            }

            materialEditor.ShaderProperty(shadowTintProp, new GUIContent(shadowTintLabel, shadowTintTooltip));
            materialEditor.ShaderProperty(ambientLightStrengthProp, new GUIContent(ambientLightStrengthLabel, ambientLightStrengthTooltip));

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
            materialEditor.ShaderProperty(specularColorProp, new GUIContent(specularColorLabel, specularColorTooltip));

            //materialEditor.ShaderProperty(specularStrengthProp, new GUIContent(specularStrengthLabel, specularStrengthTooltip));
            materialEditor.ShaderProperty(specularOffsetNoiseMapProp, new GUIContent(specularOffsetNoiseMapLabel, specularOffsetNoiseMapTooltip));
            materialEditor.ShaderProperty(specularOffsetNoiseStrengthProp, new GUIContent(specularOffsetNoiseStrengthLabel, specularOffsetNoiseStrengthTooltip));
            materialEditor.ShaderProperty(specularBoostProp, new GUIContent(specularBoostLabel, specularBoostTooltip));
            //materialEditor.ShaderProperty(specularPowerProp, new GUIContent(specularPowerLabel, specularPowerTooltip));

            Vector2 specularThresholds = material.GetVector(specularThresholdsName);

            EditorGUI.BeginChangeCheck();
            {
                EditorGUILayout.LabelField(new GUIContent(specularThresholdsLabel, specularThresholdsTooltip));
                EditorGUI.indentLevel++;
                specularThresholds.x = EditorGUILayout.Slider("Specular Start:", specularThresholds.x, -1.0f, 1.0f);
                specularThresholds.y = EditorGUILayout.Slider("Specular Full:", specularThresholds.y, -1.0f, 1.0f);
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

        bool ITerrainLayerCustomUI.OnTerrainLayerGUI(TerrainLayer terrainLayer, Terrain terrain)
        {
            var terrainLayers = terrain.terrainData.terrainLayers;

            // Don't use the member field enableHeightBlend as ShaderGUI.OnGUI might not be called if the material UI is folded.
            // heightblend shouldn't be available if we are in multi-pass mode, because it is guaranteed to be broken.
            bool heightBlendAvailable = (terrainLayers.Length <= 4);
            bool heightBlend = heightBlendAvailable && terrain.materialTemplate.HasProperty(kEnableHeightBlend) && (terrain.materialTemplate.GetFloat(kEnableHeightBlend) > 0);

            terrainLayer.diffuseTexture = EditorGUILayout.ObjectField(styles.diffuseTexture, terrainLayer.diffuseTexture, typeof(Texture2D), false) as Texture2D;
            TerrainLayerUtility.ValidateDiffuseTextureUI(terrainLayer.diffuseTexture);

            var diffuseRemapMin = terrainLayer.diffuseRemapMin;
            var diffuseRemapMax = terrainLayer.diffuseRemapMax;
            EditorGUI.BeginChangeCheck();

            bool enableDensity = false;
            if (terrainLayer.diffuseTexture != null)
            {
                var rect = GUILayoutUtility.GetLastRect();
                rect.y += 16 + 4;
                rect.width = EditorGUIUtility.labelWidth + 64;
                rect.height = 16;

                ++EditorGUI.indentLevel;

                var diffuseTint = new Color(diffuseRemapMax.x, diffuseRemapMax.y, diffuseRemapMax.z);
                diffuseTint = EditorGUI.ColorField(rect, styles.colorTint, diffuseTint, true, false, false);
                diffuseRemapMax.x = diffuseTint.r;
                diffuseRemapMax.y = diffuseTint.g;
                diffuseRemapMax.z = diffuseTint.b;
                diffuseRemapMin.x = diffuseRemapMin.y = diffuseRemapMin.z = 0;

                if (!heightBlend)
                {
                    rect.y = rect.yMax + 2;
                    enableDensity = EditorGUI.Toggle(rect, styles.opacityAsDensity, diffuseRemapMin.w > 0);
                }

                --EditorGUI.indentLevel;
            }
            diffuseRemapMax.w = 1;
            diffuseRemapMin.w = enableDensity ? 1 : 0;

            if (EditorGUI.EndChangeCheck())
            {
                terrainLayer.diffuseRemapMin = diffuseRemapMin;
                terrainLayer.diffuseRemapMax = diffuseRemapMax;
            }

            // Display normal map UI
            terrainLayer.normalMapTexture = EditorGUILayout.ObjectField(styles.normalMapTexture, terrainLayer.normalMapTexture, typeof(Texture2D), false) as Texture2D;
            TerrainLayerUtility.ValidateNormalMapTextureUI(terrainLayer.normalMapTexture, TerrainLayerUtility.CheckNormalMapTextureType(terrainLayer.normalMapTexture));

            if (terrainLayer.normalMapTexture != null)
            {
                var rect = GUILayoutUtility.GetLastRect();
                rect.y += 16 + 4;
                rect.width = EditorGUIUtility.labelWidth + 64;
                rect.height = 16;

                ++EditorGUI.indentLevel;
                terrainLayer.normalScale = EditorGUI.FloatField(rect, styles.normalScale, terrainLayer.normalScale);
                --EditorGUI.indentLevel;
            }

            // Display the mask map UI and the remap controls
            terrainLayer.maskMapTexture = EditorGUILayout.ObjectField(heightBlend ? styles.maskMapTexture : styles.maskMapTextureWithoutHeight, terrainLayer.maskMapTexture, typeof(Texture2D), false) as Texture2D;
            TerrainLayerUtility.ValidateMaskMapTextureUI(terrainLayer.maskMapTexture);

            var maskMapRemapMin = terrainLayer.maskMapRemapMin;
            var maskMapRemapMax = terrainLayer.maskMapRemapMax;
            var smoothness = terrainLayer.smoothness;
            var metallic = terrainLayer.metallic;

            ++EditorGUI.indentLevel;
            EditorGUI.BeginChangeCheck();

            m_ShowChannelRemapping = EditorGUILayout.Foldout(m_ShowChannelRemapping, terrainLayer.maskMapTexture != null ? s_Styles.channelRemapping : s_Styles.defaultValues);

            if (m_ShowChannelRemapping)
            {
                if (terrainLayer.maskMapTexture != null)
                {
                    float min, max;
                    min = maskMapRemapMin.x; max = maskMapRemapMax.x;
                    EditorGUILayout.MinMaxSlider(s_Styles.metallic, ref min, ref max, 0, 1);
                    maskMapRemapMin.x = min; maskMapRemapMax.x = max;

                    min = maskMapRemapMin.y; max = maskMapRemapMax.y;
                    EditorGUILayout.MinMaxSlider(s_Styles.ao, ref min, ref max, 0, 1);
                    maskMapRemapMin.y = min; maskMapRemapMax.y = max;

                    if (heightBlend)
                    {
                        EditorGUILayout.LabelField(styles.height);
                        ++EditorGUI.indentLevel;
                        m_HeightParametrization = (HeightParametrization)EditorGUILayout.EnumPopup(styles.heightParametrization, m_HeightParametrization);
                        if (m_HeightParametrization == HeightParametrization.Amplitude)
                        {
                            // (height - heightBase) * amplitude
                            float amplitude = Mathf.Max(maskMapRemapMax.z - maskMapRemapMin.z, Mathf.Epsilon); // to avoid divide by zero
                            float heightBase = maskMapRemapMin.z / amplitude;
                            amplitude = EditorGUILayout.FloatField(styles.heightAmplitude, amplitude * 100) / 100;
                            heightBase = EditorGUILayout.FloatField(styles.heightBase, heightBase * 100) / 100;
                            maskMapRemapMin.z = heightBase * amplitude;
                            maskMapRemapMax.z = (1.0f - heightBase) * amplitude;
                        }
                        else
                        {
                            maskMapRemapMin.z = EditorGUILayout.FloatField(styles.heightMin, maskMapRemapMin.z * 100) / 100;
                            maskMapRemapMax.z = EditorGUILayout.FloatField(styles.heightMax, maskMapRemapMax.z * 100) / 100;
                        }
                        --EditorGUI.indentLevel;
                    }

                    min = maskMapRemapMin.w; max = maskMapRemapMax.w;
                    EditorGUILayout.MinMaxSlider(s_Styles.smoothness, ref min, ref max, 0, 1);
                    maskMapRemapMin.w = min; maskMapRemapMax.w = max;
                }
                else
                {
                    metallic = EditorGUILayout.Slider(s_Styles.metallic, metallic, 0, 1);
                    // AO and Height are still exclusively controlled via the maskRemap controls
                    // metallic and smoothness have their own values as fields within the LayerData.
                    maskMapRemapMax.y = EditorGUILayout.Slider(s_Styles.ao, maskMapRemapMax.y, 0, 1);
                    if (heightBlend)
                    {
                        maskMapRemapMax.z = EditorGUILayout.FloatField(s_Styles.heightCm, maskMapRemapMax.z * 100) / 100;
                    }

                    // There's a possibility that someone could slide max below the existing min value
                    // so we'll just protect against that by locking the min value down a little bit.
                    // In the case of height (Z), we are trying to set min to no lower than zero value unless
                    // max goes negative.  Zero is a good sensible value for the minimum.  For AO (Y), we
                    // don't need this extra protection step because the UI blocks us from going negative
                    // anyway.  In both cases, pushing the slider below the min value will lock them together,
                    // but min will be "left behind" if you go back up.
                    maskMapRemapMin.y = Mathf.Min(maskMapRemapMin.y, maskMapRemapMax.y);
                    maskMapRemapMin.z = Mathf.Min(Mathf.Max(0, maskMapRemapMin.z), maskMapRemapMax.z);

                    if (TextureHasAlpha(terrainLayer.diffuseTexture))
                    {
                        GUIStyle warnStyle = new GUIStyle(GUI.skin.label);
                        warnStyle.wordWrap = true;
                        GUILayout.Label("Smoothness is controlled by diffuse alpha channel", warnStyle);
                    }
                    else
                        smoothness = EditorGUILayout.Slider(s_Styles.smoothness, smoothness, 0, 1);
                }
            }

            if (EditorGUI.EndChangeCheck())
            {
                terrainLayer.maskMapRemapMin = maskMapRemapMin;
                terrainLayer.maskMapRemapMax = maskMapRemapMax;
                terrainLayer.smoothness = smoothness;
                terrainLayer.metallic = metallic;
            }
            --EditorGUI.indentLevel;

            EditorGUILayout.Space();
            TerrainLayerUtility.TilingSettingsUI(terrainLayer);

            return true;
        }
    }
}
