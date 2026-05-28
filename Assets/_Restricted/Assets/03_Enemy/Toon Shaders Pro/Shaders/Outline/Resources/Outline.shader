Shader "Hidden/ToonShadersPro/URP/Outlines"
{
    SubShader
    {
		Tags
		{
			"RenderType" = "Opaque"
			"RenderPipeline" = "UniversalPipeline"
		}

		HLSLINCLUDE
		// Credit to https://alexanderameye.github.io/outlineshader.html:
		float3 DecodeNormal(float4 enc)
		{
			float kScale = 1.7777;
			float3 nn = enc.xyz*float3(2 * kScale, 2 * kScale, 0) + float3(-kScale, -kScale, 1);
			float g = 2.0 / dot(nn.xyz, nn.xyz);
			float3 n;
			n.xy = g * nn.xy;
			n.z = g - 1;
			return n;
		}
		
		// Code 'liberated' from Shader Graph's Simple Noise node.
		inline float randomValue(float2 uv)
		{
			return frac(sin(dot(uv, float2(12.9898, 78.233)))*43758.5453);
		}

		inline float perlinLerp(float a, float b, float t)
		{
			return (1.0f - t) * a + (t * b);
		}

		inline float valueNoise(float2 uv)
		{
			float2 i = floor(uv);
			float2 f = frac(uv);
			f = f * f * (3.0 - 2.0 * f);

			uv = abs(frac(uv) - 0.5);
			float2 c0 = i + float2(0.0, 0.0);
			float2 c1 = i + float2(1.0, 0.0);
			float2 c2 = i + float2(0.0, 1.0);
			float2 c3 = i + float2(1.0, 1.0);
			float r0 = randomValue(c0);
			float r1 = randomValue(c1);
			float r2 = randomValue(c2);
			float r3 = randomValue(c3);

			float bottomOfGrid = perlinLerp(r0, r1, f.x);
			float topOfGrid = perlinLerp(r2, r3, f.x);
			float t = perlinLerp(bottomOfGrid, topOfGrid, f.y);
			return t;
		}

		float perlinNoise(float2 uv, float offset, float2 scale)
		{
			float t = 0.0;
			float2 scaledUV = uv * scale + offset;

			float freq = pow(2.0, float(0));
			float amp = pow(0.5, float(3 - 0));
			t += valueNoise(float2(scaledUV.x / freq, scaledUV.y / freq))*amp;

			freq = pow(2.0, float(1));
			amp = pow(0.5, float(3 - 1));
			t += valueNoise(float2(scaledUV.x / freq, scaledUV.y / freq))*amp;

			freq = pow(2.0, float(2));
			amp = pow(0.5, float(3 - 2));
			t += valueNoise(float2(scaledUV.x / freq, scaledUV.y / freq))*amp;

			return t;
		}
		
		float2 perlinNoiseRG(float2 uv, float2 screenSize, float offset, float scale)
		{
			float2 aspectScale = float2(screenSize.x / screenSize.y * scale, scale);
			return float2(perlinNoise(uv, offset, aspectScale), perlinNoise(uv + float2(321.24167f, -47.124f), offset, aspectScale)) * 2.0f - 1.0f;
		}
		ENDHLSL

		Pass
        {
			Name "DepthNormalOutlines"
			
			ZTest Always
			Cull Off
			ZWrite Off

			HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag
            
            #pragma shader_feature_local_fragment _USE_NOISE_OFFSETS

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
			#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
			#include "OutlineUtils.hlsl"

			float4 _OutlineColor;
			float _ColorSensitivity;
			float _ColorStrength;
			float _DepthSensitivity;
			float _DepthStrength;
			float _NormalsSensitivity;
			float _NormalsStrength;
			float _DepthThreshold;

#ifdef _USE_NOISE_OFFSETS
            float _NoiseScale;
            float _NoiseOffset;
            float _NoiseStrength;
#endif

            float4 frag (Varyings i) : SV_TARGET
            {
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
            	
            	float2 uv = i.texcoord;
            	
            	float4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
            	
#ifdef _USE_NOISE_OFFSETS
            	uv += perlinNoiseRG(uv, float2(_ScreenParams.x, _ScreenParams.y), _NoiseOffset, _NoiseScale) * _NoiseStrength;
#endif

				// Get UV coords of pixel to the left, right, below, and above the center pixel.
				float2 leftUV = uv + float2(1.0f / -_ScreenParams.x, 0.0f);
				float2 rightUV = uv + float2(1.0f / _ScreenParams.x, 0.0f);
				float2 bottomUV = uv + float2(0.0f, 1.0f / -_ScreenParams.y);
				float2 topUV = uv + float2(0.0f, 1.0f / _ScreenParams.y);

				// Find differences between nearby pixel colors.
				float3 col0 = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, leftUV).rgb;
				float3 col1 = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, rightUV).rgb;
				float3 col2 = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, bottomUV).rgb;
				float3 col3 = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, topUV).rgb;

				float3 c0 = col1 - col0;
				float3 c1 = col3 - col2;

				// Detect color-based edges using gradients.
				float edgeCol = sqrt(dot(c0, c0) + dot(c1, c1));
				edgeCol = edgeCol > _ColorSensitivity ? _ColorStrength : 0;

				// Find differences between nearby pixel depth values.
				float depth0 = Linear01Depth(GetSceneDepth(leftUV), _ZBufferParams);
				float depth1 = Linear01Depth(GetSceneDepth(rightUV), _ZBufferParams);
				float depth2 = Linear01Depth(GetSceneDepth(bottomUV), _ZBufferParams);
				float depth3 = Linear01Depth(GetSceneDepth(topUV), _ZBufferParams);

				float d0 = depth1 - depth0;
				float d1 = depth3 - depth2;

				// Detect depth-based edges using gradients.
				float edgeDepth = sqrt(d0 * d0 + d1 * d1);
				edgeDepth = edgeDepth > _DepthSensitivity ? _DepthStrength : 0;

				// Find differences between nearby pixel normals.
				float3 normal0 = DecodeNormal(SAMPLE_TEXTURE2D_X(_CameraNormalsTexture, sampler_LinearClamp, leftUV));
				float3 normal1 = DecodeNormal(SAMPLE_TEXTURE2D_X(_CameraNormalsTexture, sampler_LinearClamp, rightUV));
				float3 normal2 = DecodeNormal(SAMPLE_TEXTURE2D_X(_CameraNormalsTexture, sampler_LinearClamp, bottomUV));
				float3 normal3 = DecodeNormal(SAMPLE_TEXTURE2D_X(_CameraNormalsTexture, sampler_LinearClamp, topUV));

				float3 n0 = normal1 - normal0;
				float3 n1 = normal3 - normal2;

				// Detect normal-based edges using gradients.
				float edgeNormal = sqrt(dot(n0, n0) + dot(n1, n1));
				edgeNormal = edgeNormal > _NormalsSensitivity ? _NormalsStrength : 0;

				// Detect edges with the highest of the three metrics.
				float edge = max(max(edgeCol, edgeDepth), edgeNormal);
				float depth = GetSceneDepth(uv);

				// Discard edge-detected pixels which are super far away (usually at/near the skybox).
				depth = Linear01Depth(depth, _ZBufferParams);
				edge = depth > _DepthThreshold ? 0.0f : edge;

				return lerp(col, _OutlineColor, edge);
            }
            ENDHLSL
        }

		Pass
		{
			Name "MaskedObjectOutlines"
			
			ZTest Always
			Cull Off
			ZWrite Off

			HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

			#pragma shader_feature_local_fragment _USE_DEPTH_NORMALS
            #pragma shader_feature_local_fragment _USE_NOISE_OFFSETS

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#ifdef _USE_DEPTH_NORMALS
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
#endif
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
			#include "OutlineUtils.hlsl"

			TEXTURE2D(_MaskedObjects);

			float4 _OutlineColor;
			float _OutlineWidth;
			float _Spread;
			float _OutlineFadeStart;
			float _OutlineFadeEnd;
			float2 _DrawSides;
#ifdef _USE_DEPTH_NORMALS
			float _NormalsSensitivity;
			float _NormalsStrength;
#endif
            
#ifdef _USE_NOISE_OFFSETS
            float _NoiseScale;
            float _NoiseOffset;
            float _NoiseStrength;
#endif

			inline float distanceFade(float x, float y)
			{
				return (abs(x) + abs(y)) * _Spread;
			}

			float4 frag (Varyings i) : SV_TARGET
            {
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
            	
            	float2 uv = i.texcoord;

				float4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
            	
#ifdef _USE_NOISE_OFFSETS
            	uv += perlinNoiseRG(uv, float2(_ScreenParams.x, _ScreenParams.y), _NoiseOffset, _NoiseScale) * _NoiseStrength;
#endif

				// Get mask and depth values for center pixel.
				float maskMiddle = SAMPLE_TEXTURE2D(_MaskedObjects, sampler_LinearClamp, uv).r;
				float depthMiddle = GetSceneDepth(uv);

				// Will need eye depth later for outline fade-out.
				float eyeDepth = LinearEyeDepth(depthMiddle, _ZBufferParams);

				// Is the center pixel masked?
				float isMaskedPixel = step(0.0001f, maskMiddle.r);
				float totalDiff = 0.0f;

				// Iterate over a kernel of width (2 * _OutlineWidth + 1).
				for(int x = -_OutlineWidth; x <= _OutlineWidth; ++x)
				{
					for(int y = -_OutlineWidth; y <= _OutlineWidth; ++y)
					{
						// Get the mask and depth values of kernel pixel.
						float2 maskUV = uv + float2(x / _ScreenParams.x, y / _ScreenParams.y);
						float maskAdjacent = SAMPLE_TEXTURE2D(_MaskedObjects, sampler_LinearClamp, maskUV).r;
						float depthAdjacent = GetSceneDepth(maskUV);

						// Set eye depth to the closest pixel.
						eyeDepth = min(eyeDepth, LinearEyeDepth(depthAdjacent, _ZBufferParams));

						// Find out how the center and kernel pixel are different/same.
						float adjacentIsMask = step(0.0001f, maskAdjacent.r);
						float maskIsDifferent = step(0.000001f, distance(maskAdjacent.r, maskMiddle.r));
						float adjacentIsCloser = (1.0f - step(depthAdjacent, depthMiddle));

						// For masked center pixels, detect masked kernel pixels, or any unmasked pixel that is further away.
						float pixelDiff = isMaskedPixel * adjacentIsMask * maskIsDifferent * lerp(_DrawSides.x, _DrawSides.y, adjacentIsCloser);
						pixelDiff += isMaskedPixel * _DrawSides.x * maskIsDifferent * (1.0f - adjacentIsMask) * (1.0f - adjacentIsCloser);
						
						// For unmasked center pixels, detect masked pixels that are closer.
						pixelDiff += (1.0f - isMaskedPixel) * adjacentIsMask * adjacentIsCloser * _DrawSides.y;

						// Smooth out edge detect contributions using distance-based weighting.
						totalDiff += distanceFade(x, y) * saturate(pixelDiff);
					}
				}

				// Fade out outlines over a distance range.
				float outlineStrength = saturate(totalDiff) * _OutlineColor.a;
				outlineStrength *= smoothstep(_OutlineFadeEnd, _OutlineFadeStart, eyeDepth);

#ifdef _USE_DEPTH_NORMALS
				// Get UV coords of pixel to the left, right, below, and above the center pixel.
				float2 leftUV = uv + float2(1.0f / -_ScreenParams.x, 0.0f);
				float2 rightUV = uv + float2(1.0f / _ScreenParams.x, 0.0f);
				float2 bottomUV = uv + float2(0.0f, 1.0f / -_ScreenParams.y);
				float2 topUV = uv + float2(0.0f, 1.0f / _ScreenParams.y);

				// Find differences between nearby pixel normals.
				float3 normal0 = DecodeNormal(SAMPLE_TEXTURE2D_X(_CameraNormalsTexture, sampler_LinearClamp, leftUV));
				float3 normal1 = DecodeNormal(SAMPLE_TEXTURE2D_X(_CameraNormalsTexture, sampler_LinearClamp, rightUV));
				float3 normal2 = DecodeNormal(SAMPLE_TEXTURE2D_X(_CameraNormalsTexture, sampler_LinearClamp, bottomUV));
				float3 normal3 = DecodeNormal(SAMPLE_TEXTURE2D_X(_CameraNormalsTexture, sampler_LinearClamp, topUV));

				float3 n0 = normal1 - normal0;
				float3 n1 = normal3 - normal2;

				// Detect normal-based edges using gradients.
				float edgeNormal = sqrt(dot(n0, n0) + dot(n1, n1));
				edgeNormal = edgeNormal > _NormalsSensitivity ? _NormalsStrength : 0;

				outlineStrength = max(outlineStrength, edgeNormal);
#endif
				col.rgb = lerp(col.rgb, _OutlineColor.rgb, outlineStrength);

				return col;
            }

			ENDHLSL
		}

		Pass
		{
			Name "ThinMaskedObjectOutlines"
			
			ZTest Always
			Cull Off
			ZWrite Off

			HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag
            
            #pragma shader_feature_local_fragment _USE_NOISE_OFFSETS

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
			#include "OutlineUtils.hlsl"

			TEXTURE2D(_MaskedObjects);
			float4 _OutlineColor;
            
#ifdef _USE_NOISE_OFFSETS
            float _NoiseScale;
            float _NoiseOffset;
            float _NoiseStrength;
#endif

			float4 frag (Varyings i) : SV_TARGET
            {
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
            	
            	float2 uv = i.texcoord;

				float4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
            	
#ifdef _USE_NOISE_OFFSETS
            	uv += perlinNoiseRG(uv, float2(_ScreenParams.x, _ScreenParams.y), _NoiseOffset, _NoiseScale) * _NoiseStrength;
#endif

				// Get UVs for nearby four pixels.
				float2 leftUV = uv + float2(1.0f / -_ScreenParams.x, 0.0f);
				float2 rightUV = uv + float2(1.0f / _ScreenParams.x, 0.0f);
				float2 bottomUV = uv + float2(0.0f, 1.0f / -_ScreenParams.y);
				float2 topUV = uv + float2(0.0f, 1.0f / _ScreenParams.y);

				// Get mask pixels for center and four adjacent pixels.
				float middleMask = SAMPLE_TEXTURE2D(_MaskedObjects, sampler_LinearClamp, uv).r;
				float mask0 = SAMPLE_TEXTURE2D(_MaskedObjects, sampler_LinearClamp, leftUV).r;
				float mask1 = SAMPLE_TEXTURE2D(_MaskedObjects, sampler_LinearClamp, rightUV).r;
				float mask2 = SAMPLE_TEXTURE2D(_MaskedObjects, sampler_LinearClamp, bottomUV).r;
				float mask3 = SAMPLE_TEXTURE2D(_MaskedObjects, sampler_LinearClamp, topUV).r;

				// Get four adjacent depth pixels.
				float middleDepth = GetSceneDepth(uv);
				float depth0 = GetSceneDepth(leftUV);
				float depth1 = GetSceneDepth(rightUV);
				float depth2 = GetSceneDepth(bottomUV);
				float depth3 = GetSceneDepth(topUV);

				// Only use mask values for further away pixels.
				mask0 = step(middleDepth, depth0) < 0.5f ? mask0 : middleMask;
				mask1 = step(middleDepth, depth1) < 0.5f ? mask1 : middleMask;
				mask2 = step(middleDepth, depth2) < 0.5f ? mask2 : middleMask;
				mask3 = step(middleDepth, depth3) < 0.5f ? mask3 : middleMask;

				// Find differences across adjacent pixels to find edges.
				float c0 = mask1 - mask0;
				float c1 = mask3 - mask2;
				float edgeDiff = sqrt(c0 * c0 + c1 * c1);
				float edgeDetect = step(0.0001f, edgeDiff);

				// Do not detect edges on pixels that are not masked.
				edgeDetect *= step(0.001f, middleMask);

				col.rgb = lerp(col.rgb, _OutlineColor.rgb, edgeDetect * _OutlineColor.a);

				return col;
            }

			ENDHLSL
		}
    }
}
