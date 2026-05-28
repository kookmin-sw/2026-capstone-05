Shader "Hidden/ToonShadersPro/URP/HullOutlines"
{
	Properties
	{
		_OutlineColor("Outline Color", Color) = (0, 0, 0, 1)
		_OutlineThickness("Outline Thickness", Float) = 0.1

		[HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
	}
	SubShader
	{
		Tags
		{
			"RenderType" = "Opaque"
			"Queue" = "Geometry"
			"RenderPipeline" = "UniversalPipeline"
		}

		Pass
		{
			Cull Front
			Blend[_SrcBlend][_DstBlend]

			HLSLPROGRAM
			#pragma target 3.0
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing

			#pragma shader_feature_local _ _HULL_LIGHTING_ON
			#pragma multi_compile_fragment _ _LIGHT_COOKIES

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

#ifdef _HULL_LIGHTING_ON
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#endif

			CBUFFER_START(UnityPerMaterial)
				float _OutlineThickness;
				float4 _OutlineColor;
				float _OutlineDirection;
				float _OutlineMinLighting;
			CBUFFER_END

            struct appdata
            {
                float4 positionOS : POSITION;
				float3 normalOS : NORMAL;
				UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
#ifdef _HULL_LIGHTING_ON
				float3 normalWS : TEXCOORD0;
#endif
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v, out float4 positionCS : SV_Position)
            {
				v2f o;

				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                
				float4 positionOS = v.positionOS;
				positionOS.xyz += normalize(v.normalOS) * _OutlineThickness;
                positionCS = TransformObjectToHClip(positionOS.xyz);

#ifdef _HULL_LIGHTING_ON
				o.normalWS = TransformObjectToWorldNormal(-v.normalOS);
#endif
                return o;
            }

			float4 frag (v2f i, float4 positionSS : VPOS) : SV_Target
            {
				UNITY_SETUP_INSTANCE_ID(i);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

				// Get the depth of the pixel being rendered and the pixel already rendered.
				float2 screenUV = positionSS.xy / _ScreenSize.xy;
				float screenDepth = Linear01Depth(SampleSceneDepth(screenUV), _ZBufferParams);
				float objectDepth = Linear01Depth(positionSS.z, _ZBufferParams);

				// Compare the two pixels - if depthMask is 1, the object is visible in the final image.
				float depthMask = step(objectDepth, screenDepth);

				if(depthMask < 0.5f)
				{
					discard;
				}

				float4 outlineColor = _OutlineColor;

				// Apply simple diffuse lighting from main light to the outlines.
#ifdef _HULL_LIGHTING_ON
				float3 normalWS = normalize(i.normalWS * _OutlineDirection);

				Light mainLight = GetMainLight();
				float3 lighting = max(saturate(dot(normalWS, mainLight.direction)), _OutlineMinLighting) * mainLight.color;

				outlineColor.rgb *= lighting;
#endif

				// Render the outline color (with blending if transparency was enabled).
				return outlineColor;
            }
            ENDHLSL
        }
    }
}
