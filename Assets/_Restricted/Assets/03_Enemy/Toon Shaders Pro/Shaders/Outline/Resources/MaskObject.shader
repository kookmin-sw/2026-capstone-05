Shader "Hidden/ToonShadersPro/URP/MaskObject"
{
	SubShader
	{
		Tags
		{
			"RenderType" = "Opaque"
			"Queue" = "Geometry"
			"RenderPipeline" = "UniversalPipeline"
		}

		HLSLINCLUDE
			#pragma target 3.0
			#pragma fragment frag
			#pragma multi_compile_instancing

			#pragma shader_feature_local_fragment _IGNORE_DEPTH

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
			#include "OutlineUtils.hlsl"

            struct appdata
            {
                float4 positionOS : POSITION;
				float4 color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
				nointerpolation float3 gameObjectPositionWS : TEXCOORD0;
				nointerpolation float4 color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v, out float4 positionCS : SV_Position)
            {
				v2f o;

				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                
                positionCS = TransformObjectToHClip(v.positionOS.xyz);
				o.gameObjectPositionWS = GetObjectToWorldMatrix()._m03_m13_m23; 
				o.color = v.color;
                return o;
            }

			// Hashing algorithms modified from: https://tips.orels.sh/optimized-hash-for-shaders
			float hash1to1(float p)
			{
				p = frac(p * .1031);
				p *= p + 33.33;
				p *= p + p;
				return frac(p) * 0.995f + 0.005f;
			}

			float hash3to1(float3 p3)
			{
				p3  = frac(p3 * .1031);
				p3 += dot(p3, p3.zyx + 31.32);
				return frac((p3.x + p3.y) * p3.z);
			}
		ENDHLSL

		Pass
		{
			Name "MaskObjects"
			
			ZTest Always
			Cull Off
			ZWrite Off

			HLSLPROGRAM
			#pragma vertex vert

			// Thanks to this for the note on using VPOS: https://gamedev.stackexchange.com/questions/157922/depth-intersection-shader
            float frag (v2f i, float4 positionSS : VPOS) : SV_Target
            {
				UNITY_SETUP_INSTANCE_ID(i);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i); 

#ifndef _IGNORE_DEPTH
				// Get the depth of the pixel being rendered and the pixel already rendered.
				float2 screenUV = positionSS.xy / _ScreenSize.xy;
				float screenDepth = Linear01Depth(GetSceneDepth(screenUV), _ZBufferParams);
				float objectDepth = Linear01Depth(positionSS.z, _ZBufferParams);

				// Compare the two pixels - if depthMask is 1, the object is visible in the final image.
            	float depthMask = 1.0f - step(0.0000001f, abs(objectDepth - screenDepth));

				if(depthMask < 0.5f)
				{
					discard;
				}
#endif

				// Render random value based on object world position.
				float maskColor = hash3to1(i.gameObjectPositionWS + float3(17.34f, 271.4f, 94.247f));
				return maskColor * 0.999f + 0.001f;
            }
            ENDHLSL
        }

		Pass
		{
			Name "MaskTriangles"
			
			ZTest Always
			Cull Off
			ZWrite Off

			HLSLPROGRAM
			#pragma vertex vert

			// Thanks to this for the note on using VPOS: https://gamedev.stackexchange.com/questions/157922/depth-intersection-shader
            float frag (v2f i, float4 positionSS : VPOS, uint primitiveID : SV_PrimitiveID) : SV_Target
            {
				UNITY_SETUP_INSTANCE_ID(i);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i); 

#ifndef _IGNORE_DEPTH
				// Get the depth of the pixel being rendered and the pixel already rendered.
				float2 screenUV = positionSS.xy / _ScreenSize.xy;
				float screenDepth = Linear01Depth(GetSceneDepth(screenUV), _ZBufferParams);
				float objectDepth = Linear01Depth(positionSS.z, _ZBufferParams);

				// Compare the two pixels - if depthMask is 1, the object is visible in the final image.
				float depthMask = 1.0f - step(0.0000001f, abs(objectDepth - screenDepth));

				if(depthMask < 0.5f)
				{
					discard;
				}
#endif

				// Render random value based on object world position and primitiveID.
				float maskColor = hash3to1(primitiveID + i.gameObjectPositionWS + float3(17.34f, 271.4f, 94.247f));
				return maskColor * 0.999f + 0.001f;
            }
            ENDHLSL
        }

		Pass
		{
			Name "MaskOnce"
			
			ZTest Always
			Cull Off
			ZWrite Off

			HLSLPROGRAM
			#pragma vertex vert

			// Thanks to this for the note on using VPOS: https://gamedev.stackexchange.com/questions/157922/depth-intersection-shader
            float frag (v2f i, float4 positionSS : VPOS) : SV_Target
            {
				UNITY_SETUP_INSTANCE_ID(i);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i); 

#ifndef _IGNORE_DEPTH
				// Get the depth of the pixel being rendered and the pixel already rendered.
				float2 screenUV = positionSS.xy / _ScreenSize.xy;
				float screenDepth = Linear01Depth(GetSceneDepth(screenUV), _ZBufferParams);
				float objectDepth = Linear01Depth(positionSS.z, _ZBufferParams);

				// Compare the two pixels - if depthMask is 1, the object is visible in the final image.
				float depthMask = 1.0f - step(0.0000001f, abs(objectDepth - screenDepth));

				if(depthMask < 0.5f)
				{
					discard;
				}
#endif

				// Render just one mask value for anything that fits.
				return 0.5f;
            }
			ENDHLSL
		}

		Pass
		{
			Name "MaskVertexColors"
			
			ZTest Always
			Cull Off
			ZWrite Off

			HLSLPROGRAM
			#pragma vertex vert

			// Thanks to this for the note on using VPOS: https://gamedev.stackexchange.com/questions/157922/depth-intersection-shader
            float frag (v2f i, float4 positionSS : VPOS) : SV_Target
            {
				UNITY_SETUP_INSTANCE_ID(i);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i); 

#ifndef _IGNORE_DEPTH
				// Get the depth of the pixel being rendered and the pixel already rendered.
				float2 screenUV = positionSS.xy / _ScreenSize.xy;
				float screenDepth = Linear01Depth(GetSceneDepth(screenUV), _ZBufferParams);
				float objectDepth = Linear01Depth(positionSS.z, _ZBufferParams);

				// Compare the two pixels - if depthMask is 1, the object is visible in the final image.
				float depthMask = 1.0f - step(0.0000001f, abs(objectDepth - screenDepth));

				if(depthMask < 0.5f)
				{
					discard;
				}
#endif

				// Render random value based on object world position.
				float maskColor = hash3to1(i.color.rgb + float3(17.34f, 271.4f, 94.247f));
				return maskColor * 0.999f + 0.001f;
            }
            ENDHLSL
		}

		Pass
		{
			Name "MaskRSUV"
			
			ZTest Always
			Cull Off
			ZWrite Off

			HLSLPROGRAM
			#pragma vertex vert

			// Thanks to this for the note on using VPOS: https://gamedev.stackexchange.com/questions/157922/depth-intersection-shader
            float frag (v2f i, float4 positionSS : VPOS) : SV_Target
            {
				UNITY_SETUP_INSTANCE_ID(i);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
            	
#if UNITY_VERSION >= 60030000

#ifndef _IGNORE_DEPTH
				// Get the depth of the pixel being rendered and the pixel already rendered.
				float2 screenUV = positionSS.xy / _ScreenSize.xy;
				float screenDepth = Linear01Depth(GetSceneDepth(screenUV), _ZBufferParams);
				float objectDepth = Linear01Depth(positionSS.z, _ZBufferParams);

				// Compare the two pixels - if depthMask is 1, the object is visible in the final image.
				float depthMask = 1.0f - step(0.0000001f, abs(objectDepth - screenDepth));

				if(depthMask < 0.5f)
				{
					discard;
				}
#endif

				float rsuv = unity_RendererUserValue;
                            	
                // Render random value based on RSUV.
                float maskColor = hash3to1(rsuv.xxx + float3(17.34f, 271.4f, 94.247f));
                return maskColor * 0.999f + 0.001f;
            	
#else
            	// Unity versions before RSUV will do nothing.
				return 0.0f;
#endif
            }
            ENDHLSL
        }

		/*
		Pass
		{
			Name "MaskObjectsTransparent"

			Blend One One

			HLSLPROGRAM
			#pragma vertex vert

			// Thanks to this for the note on using VPOS: https://gamedev.stackexchange.com/questions/157922/depth-intersection-shader
            float frag (v2f i, float4 positionSS : VPOS) : SV_Target
            {
				UNITY_SETUP_INSTANCE_ID(i);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i); 

				// Get the depth of the pixel being rendered and the pixel already rendered.
				float2 screenUV = positionSS.xy / _ScreenSize.xy;
				float screenDepth = Linear01Depth(GetSceneDepth(screenUV), _ZBufferParams);
				float objectDepth = Linear01Depth(positionSS.z, _ZBufferParams);

				// Compare the two pixels - if depthMask is 1, the object is visible in the final image.
				float depthMask = step(objectDepth, screenDepth);

				if(depthMask < 0.5f)
				{
					discard;
				}

				return 0.001f;
            }
            ENDHLSL
        }
		*/
    }
}
