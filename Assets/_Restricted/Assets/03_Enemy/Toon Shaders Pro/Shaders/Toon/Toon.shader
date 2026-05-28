Shader "Toon Shaders Pro/URP/Toon"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1,1,1,1)
        [ToggleUI] _UseVertexColors("Use Vertex Colors?", Float) = 0.0

        [HDR] _LightTint("Light Tint", Color) = (1, 1, 1, 1)
        [HDR] _MiddleTint("Middle Tint", Color) = (0.5, 0.5, 0.5, 1)
        _ShadowTint("Shadow Tint", Color) = (0, 0, 0, 0)
        //_AmbientStrength("Ambient Light Strength", Range(0.0, 1.0)) = 0.25
        [Vector2(0.0, 1.0)] _ShadowThresholds("Shadow Thresholds", Vector) = (0.1, 0.11, 0, 0)
        [Vector2(0.0, 2.0, 1.0)] _DiffuseThresholds("Diffuse Light Thresholds", Vector) = (-0.01, 0.01, 0, 0)

        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.0
        _SmoothnessMap("Smoothness Map", 2D) = "white" {}
        [ToggleUI] _ConvertFromRoughness("Convert From Roughness", Float) = 0

        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        _MetallicGlossMap("Metallic", 2D) = "white" {}
        _SpecularBoost("Specular Boost", Range(1.0, 10.0)) = 1.0

        _GIStrength("GI Strength", Range(0.0, 1.0)) = 1.0

        [NoScaleOffset] _SpecularMap("Specular Map", 2D) = "white" {}
        _SpecularOffsetNoiseMap("Specular Offset Noise Map", 2D) = "black" {}
        _SpecularOffsetNoiseStrength("Specular Offset Noise Strength", Range(0.0, 5.0)) = 0.02
        //("Specular Strength", Range(0, 1)) = 1
        [HDR] _SpecularColor("Specular Color", Color) = (0.2, 0.2, 0.2, 1)
        //_SpecularPower("Glossiness", Range(0.1, 1000)) = 300
        [Vector2(0.0, 0.05)] _SpecularThresholds("Specular Light Thresholds", Vector) = (0.01, 0.02, 0, 0)

        [HDR] _RimColor("Rim Lighting Color", Color) = (1, 1, 1, 1)
        [Vector2(0, 1)] _RimThresholds("Rim Thresholds", Vector) = (0.8, 0.82, 0, 0)
        _RimExtension("Rim Extension", Range(0.0, 1.0)) = 0.0

        [NoScaleOffset] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Strength", Range(0, 2)) = 1
        [ToggleUI] _ReceiveShadows("Receive Shadows", Float) = 1.0

        [ToggleUI] _UseSecondThreshold("Use Second Threshold?", Float) = 0.0

        _WorkflowMode("WorkflowMode", Float) = 1.0
        _Surface("__surface", Float) = 0.0
        _Blend("__blend", Float) = 0.0
        _Cull("__cull", Float) = 2.0
        [ToggleUI] _AlphaClip("__clip", Float) = 0.0
        [HideInInspector] _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _SrcBlendAlpha("__srcA", Float) = 1.0
        [HideInInspector] _DstBlendAlpha("__dstA", Float) = 0.0
        [HideInInspector] _ZWrite("__zw", Float) = 1.0
        [HideInInspector] _BlendModePreserveSpecular("_BlendModePreserveSpecular", Float) = 1.0
        [HideInInspector] _AlphaToMask("__alphaToMask", Float) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
        }
        LOD 300

        HLSLINCLUDE
        #include "ToonInput.hlsl"

        #pragma shader_feature_local_fragment _RECEIVE_SHADOWS_ON
        #pragma shader_feature_local_fragment _USE_SECOND_THRESHOLD
        #pragma shader_feature_local_fragment _USE_VERTEX_COLORS
        #pragma shader_feature_local_fragment _ALPHATEST_ON
        ENDHLSL

        // Draws the object "normally" - the main pass. Using ForwardOnly because the deferred lighting
        // sort of clashes with the idea of toon lighting.
        Pass
        {
            Name "ForwardLit"

            Tags
            {
                "LightMode" = "UniversalForwardOnly"
            }

            Blend[_SrcBlend][_DstBlend], [_SrcBlendAlpha][_DstBlendAlpha]
            ZWrite[_ZWrite]
            Cull[_Cull]
            AlphaToMask[_AlphaToMask]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            #pragma shader_feature_local_fragment _SPECULAR_SETUP
            
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
			#pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
			#pragma multi_compile_fragment _ _SHADOWS_SOFT
			#pragma multi_compile _ _FORWARD_PLUS
			#pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS

            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
			#pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
			#pragma multi_compile _ SHADOWS_SHADOWMASK
			#pragma multi_compile _ DIRLIGHTMAP_COMBINED
			#pragma multi_compile _ LIGHTMAP_ON
			#pragma multi_compile _ DYNAMICLIGHTMAP_ON

            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct appdata
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 staticLightmapUV : TEXCOORD1;
				float2 dynamicLightmapUV : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float fog : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 tangentWS : TEXCOORD3;
                float3 positionWS : TEXCOORD4;
                float3 viewWS : TEXCOORD5;
                DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 6);
				float2 dynamicLightmapUV : TEXCOORD7;
                UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
            };

            #include "ToonFunctions.hlsl"

            v2f vert(appdata v)
            {
                v2f o = (v2f)0;
                UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color;
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.fog = ComputeFogFactor(o.positionCS.z);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.tangentWS = float4(TransformObjectToWorldDir(v.tangentOS.xyz), v.tangentOS.w);
                o.positionWS = mul(UNITY_MATRIX_M, v.positionOS).xyz;
                o.viewWS = GetWorldSpaceNormalizeViewDir(o.positionWS);
                OUTPUT_SH(o.normalWS, o.vertexSH);
				OUTPUT_LIGHTMAP_UV(v.staticLightmapUV, unity_LightmapST, o.staticLightmapUV);
                o.dynamicLightmapUV = v.dynamicLightmapUV.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;

                return o;
            }

            void frag(
				v2f i, 
				out float4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
				, out float4 outRenderingLayers : SV_Target1
#endif
			)
            {
                UNITY_SETUP_INSTANCE_ID(i);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                // Apply base color and perform alpha clip if appropriate.
                float4 baseColor = _BaseColor * SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);

#ifdef _USE_VERTEX_COLORS
                baseColor *= i.color;
#endif

				AlphaDiscard(baseColor.a, _Cutoff);

                float3 normalWS = normalize(i.normalWS);
                float3 viewWS = normalize(i.viewWS);

                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                float4 shadowMask = SAMPLE_SHADOWMASK(i.dynamicLightmapUV);

                // Calculate the specular light offset values.
                float2 offsetUVs = TRANSFORM_TEX(i.positionWS.xz, _SpecularOffsetNoiseMap);
                float specularOffset = SAMPLE_TEXTURE2D(_SpecularOffsetNoiseMap, sampler_SpecularOffsetNoiseMap, offsetUVs).r;
                specularOffset = (specularOffset - 0.5f) * _SpecularOffsetNoiseStrength;

#if _SPECULAR_SETUP
                float metallic = 1.0f;
                float3 specular = SAMPLE_TEXTURE2D(_SpecularMap, sampler_SpecularMap, i.uv).r * _SpecularColor;
#else
                float metallic = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, i.uv).r * _Metallic;
                float3 specular = half3(0.0h, 0.0h, 0.0h);
#endif

                float smoothness = SAMPLE_TEXTURE2D(_SmoothnessMap, sampler_SmoothnessMap, i.uv).r * _Smoothness;
                smoothness = lerp(smoothness, 1.0f - smoothness, _ConvertFromRoughness);

#ifdef _DBUFFER
                float occlusion = 0;
                float3 norm = normalWS;
                ApplyDecal(i.positionCS, baseColor.rgb, specular, normalWS, metallic, occlusion, smoothness);
#endif

                // Sample normal map and convert to world normal.
                float3 normalMap = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv));
                normalMap = float3(normalMap.rg * _BumpScale, lerp(1.0f, normalMap.b, saturate(_BumpScale)));

                float3 binormal = cross(normalWS, i.tangentWS.xyz) * (i.tangentWS.w * unity_WorldTransformParams.w);
                normalWS = normalize(
                    normalMap.x * i.tangentWS.xyz +
                    normalMap.y * binormal +
                    normalMap.z * normalWS);

                // Set up InputData struct.
                InputData inputData = (InputData)0;
				inputData.positionWS = i.positionWS;
                inputData.positionCS = i.positionCS;
				inputData.normalWS = NormalizeNormalPerPixel(normalWS);
				inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
				inputData.shadowCoord = shadowCoord;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);

                ToonBakedGIData(i, inputData);

                // Get the main light.
                Light mainLight = GetMainLight(shadowCoord, i.positionWS, shadowMask);

                // Set up BRDF data for lighting calculations.
                BRDFData brdfData;
                ToonBRDFData(baseColor.rgb, metallic, specular, smoothness, baseColor.a, brdfData);

                // Calculate global illumination.
                MixRealtimeAndBakedGI(mainLight, inputData.normalWS, inputData.bakedGI, inputData.shadowMask);
                AmbientOcclusionFactor aoFactor = CreateAmbientOcclusionFactor(inputData.normalizedScreenSpaceUV, 1.0h);
                half3 giColor = ToonGlobalIllumination(brdfData, inputData.bakedGI, aoFactor.indirectAmbientOcclusion, i.normalWS, i.viewWS);
                giColor = lerp(0.0, giColor, _GIStrength);

                // Apply lighting from main directional light.
                half3 mainLightColor = CalculateToonLighting(brdfData, mainLight, normalWS, viewWS, specularOffset);

                half3 additionalLightColor = 0.0h;

#ifdef _ADDITIONAL_LIGHTS

                // Apply secondary lights.
				uint lightCount = GetAdditionalLightsCount();

#if USE_FORWARD_PLUS

                // Apply secondary lights (Forward rendering).
				for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); ++lightIndex) 
				{
                    FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK

					Light light = GetAdditionalLight(lightIndex, i.positionWS, shadowMask);
                    additionalLightColor += CalculateToonLighting(brdfData, light, normalWS, viewWS, specularOffset);
                }

#endif
                // Apply secondary lights (Forward+ rendering).
				LIGHT_LOOP_BEGIN(lightCount)

					Light light = GetAdditionalLight(lightIndex, i.positionWS, shadowMask);
                    additionalLightColor += CalculateToonLighting(brdfData, light, normalWS, viewWS, specularOffset);

                LIGHT_LOOP_END

#endif

                half3 lightColor = giColor + mainLightColor + additionalLightColor;
                baseColor.rgb *= lightColor;

                //baseColor.rgb = tint;
                baseColor.rgb = MixFog(baseColor.rgb, i.fog);
            	baseColor.a = OutputAlpha(baseColor.a, IsSurfaceTypeTransparent(_Surface));

                outColor = baseColor;
            	
#ifdef _WRITE_RENDERING_LAYERS
				outRenderingLayers = float4(EncodeMeshRenderingLayer(), 0, 0, 0);
#endif
            }

            ENDHLSL
        }

        // ShadowCaster draws to the shadow map.
        Pass
        {
            Name "ShadowCaster"

            Tags
            {
                "LightMode" = "ShadowCaster"
            }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull[_Cull]

            HLSLPROGRAM
            #pragma vertex shadowPassVert
            #pragma fragment shadowPassFrag

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            float3 _LightDirection;
            float3 _LightPosition;

            struct appdata
            {
	            float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
	            float2 uv : TEXCOORD0;
	            UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
	            float4 positionCS : SV_POSITION;
                float4 color : COLOR;
	            float2 uv : TEXCOORD0;
	            float3 normalWS : TEXCOORD1;
            };

            // Convert positions to be relative to the light facing direction.
            float4 GetShadowPositionHClip(appdata i)
            {
	            float3 positionWS = TransformObjectToWorld(i.positionOS.xyz);
	            float3 normalWS = TransformObjectToWorldNormal(i.normalOS);

#if _CASTING_PUNCTUAL_LIGHT_SHADOW
	            float3 lightDirectionWS = normalize(_LightPosition - positionWS);
#else
	            float3 lightDirectionWS = _LightDirection;
#endif

	            float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

#if UNITY_REVERSED_Z
	            positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
#else
	            positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
#endif

	            return positionCS;
            }

            v2f shadowPassVert(appdata v)
            {
                v2f o = (v2f)0;
	            UNITY_SETUP_INSTANCE_ID(v);

	            o.positionCS = GetShadowPositionHClip(v);
                o.color = v.color;
	            o.uv = TRANSFORM_TEX(v.uv, _BaseMap);

	            return o;
            }

            float4 shadowPassFrag(v2f i) : SV_TARGET
            {
                // Clip based on the base color alpha.
                float4 baseColor = _BaseColor * SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);

#ifdef _USE_VERTEX_COLORS
                baseColor *= i.color;
#endif

				AlphaDiscard(baseColor.a, _Cutoff);

                // If the pixel wasn't clipped, draw black into the shadow map.
                return 0;
            }

            ENDHLSL
        }

        // DepthOnly writes to the depth texture, useful for other shaders which use depth info.
        Pass
        {
            Name "DepthOnly"

            Tags
            {
                "LightMode" = "DepthOnly"
            }

            ZWrite On
            ColorMask R
            Cull[_Cull]

            HLSLPROGRAM
            #pragma vertex depthOnlyVert
            #pragma fragment depthOnlyFrag

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #pragma multi_compile_instancing

            struct appdata
            {
	            float4 positionOS : POSITION;
                float4 color : COLOR;
	            float2 uv : TEXCOORD0;
	            UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
	            float4 positionCS : SV_POSITION;
                float4 color : COLOR;
	            float2 uv : TEXCOORD0;
	            UNITY_VERTEX_INPUT_INSTANCE_ID
	            UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f depthOnlyVert(appdata v)
            {
                v2f o = (v2f)0;
	            UNITY_SETUP_INSTANCE_ID(v);
	            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

	            o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color;
	            o.uv = TRANSFORM_TEX(v.uv, _BaseMap);

	            return o;
            }

            float depthOnlyFrag(v2f i) : SV_TARGET
            {
	            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                // Clip based on the base color alpha.
                float baseColorAlpha = _BaseColor.a * SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a;

#ifdef _USE_VERTEX_COLORS
                baseColorAlpha *= i.color.a;
#endif

				AlphaDiscard(baseColorAlpha, _Cutoff);

                // Return depth value by using clip-space z.
	            return i.positionCS.z;
            }

            ENDHLSL
        }

        // The DepthNormals pass draws the normal vector of the mesh in world space.
        Pass
        {
            Name "DepthNormals"

            Tags
            {
                "LightMode" = "DepthNormalsOnly"
            }

            ZWrite On
            Cull[_Cull]

            HLSLPROGRAM
            #pragma vertex depthNormalsVert
            #pragma fragment depthNormalsFrag

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            
            #pragma multi_compile_instancing

            struct appdata
            {
	            float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float4 color : COLOR;
	            float2 uv : TEXCOORD0;
	            UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
	            float4 positionCS : SV_POSITION;
                float4 color : COLOR;
	            float2 uv : TEXCOORD0;
	            float3 normalWS : TEXCOORD1;
                float4 tangentWS : TEXCOORD2;
	            UNITY_VERTEX_INPUT_INSTANCE_ID
	            UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f depthNormalsVert(appdata v)
            {
	            v2f o = (v2f)0;
	            UNITY_SETUP_INSTANCE_ID(v);
	            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

	            o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color;
	            o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.tangentWS = float4(TransformObjectToWorldDir(v.tangentOS.xyz), v.tangentOS.w);
	            o.uv = TRANSFORM_TEX(v.uv, _BaseMap);

	            return o;
            }

            void depthNormalsFrag(
				v2f i,
				out float4 outNormalWS : SV_Target0
			#ifdef _WRITE_RENDERING_LAYERS
			    , out float4 outRenderingLayers : SV_Target1
			#endif
			)
            {
	            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                // Clip based on the base color alpha.
	            float baseColorAlpha = _BaseColor.a * SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a;

#ifdef _USE_VERTEX_COLORS
                baseColorAlpha *= i.color.a;
#endif

				AlphaDiscard(baseColorAlpha, _Cutoff);

                float3 normalWS = normalize(i.normalWS);

                // Sample normal map and convert to world normal.
                float3 normalMap = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv));
                normalMap = float3(normalMap.rg * _BumpScale, lerp(1.0f, normalMap.b, saturate(_BumpScale)));

                float3 binormal = cross(normalWS, i.tangentWS.xyz) * (i.tangentWS.w * unity_WorldTransformParams.w);
                normalWS = normalize(
                    normalMap.x * i.tangentWS.xyz +
                    normalMap.y * binormal +
                    normalMap.z * normalWS);

                // Draw the mesh's normals in world space to the output texture.
	            outNormalWS = float4(NormalizeNormalPerPixel(normalWS), 0.0f);
            	
#ifdef _WRITE_RENDERING_LAYERS
				outRenderingLayers = float4(EncodeMeshRenderingLayer(), 0, 0, 0);
#endif
            }
            ENDHLSL
        }

        // Meta pass for baking lightmaps.
        Pass
        {
            Name "Meta"

            Tags
            {
                "LightMode" = "Meta"
            }

            Cull Off

            HLSLPROGRAM
            #pragma vertex metaVert
            #pragma fragment metaFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"

            #pragma shader_feature EDITOR_VISUALIZATION

            struct appdata
            {
	            float4 positionOS : POSITION;
	            float3 normalOS : NORMAL;
                float4 color : COLOR;
	            float2 uv0 : TEXCOORD0;
	            float2 uv1 : TEXCOORD1;
	            float2 uv2 : TEXCOORD2;
	            UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
	            float4 positionCS : SV_POSITION;
                float4 color : COLOR;
	            float2 uv : TEXCOORD0;

#ifdef EDITOR_VISUALIZATION
	            float2 vizUV : TEXCOORD1;
	            float4 lightCoord : TEXCOORD2;
#endif
            };

            v2f metaVert(appdata v)
            {
	            v2f o = (v2f)0;

	            float3 vertex = v.positionOS.xyz;

#ifndef EDITOR_VISUALIZATION
	            if (unity_MetaVertexControl.x)
	            {
		            vertex.xy = v.uv1 * unity_LightmapST.xy + unity_LightmapST.zw;
		            vertex.z = vertex.z > 0 ? REAL_MIN : 0.0f;
	            }
	            if (unity_MetaVertexControl.y)
	            {
		            vertex.xy = v.uv2 * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
		            vertex.z = vertex.z > 0 ? REAL_MIN : 0.0f;
	            }
	            o.positionCS = TransformWorldToHClip(vertex);
#else
	            o.positionCS = TransformObjectToHClip(vertex);
#endif
                o.color = v.color;

	            o.uv = TRANSFORM_TEX(v.uv0, _BaseMap);
#ifdef EDITOR_VISUALIZATION
	            UnityEditorVizData(v.positionOS.xyz, v.uv0, v.uv1, v.uv2, o.vizUV, o.lightCoord);
#endif
	            return o;
            }

            float4 metaFrag(v2f i) : SV_TARGET
            {
                float4 baseColor = _BaseColor * SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);

#ifdef _USE_VERTEX_COLORS
                baseColor *= i.color;
#endif

	            MetaInput metaInput;
	            metaInput.Albedo = baseColor.rgb;
	            metaInput.Emission = 1;

#ifdef EDITOR_VISUALIZATION
	            metaInput.VizUV = i.vizUV;
	            metaInput.LightCoord = i.lightCoord;
#endif
	            return UnityMetaFragment(metaInput);
            }
            ENDHLSL
        }
    }

    CustomEditor "ToonShadersPro.URP.ToonShaderGUI"
}
