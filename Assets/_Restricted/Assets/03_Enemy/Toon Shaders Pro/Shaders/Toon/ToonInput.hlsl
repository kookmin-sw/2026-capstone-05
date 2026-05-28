#ifndef TOON_INPUT_SURFACE_INCLUDED
#define TOON_INPUT_SURFACE_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceData.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

TEXTURE2D(_SmoothnessMap);
SAMPLER(sampler_SmoothnessMap);

TEXTURE2D(_MetallicGlossMap);
SAMPLER(sampler_MetallicGlossMap);

TEXTURE2D(_SpecularMap);
SAMPLER(sampler_SpecularMap);

TEXTURE2D(_SpecularOffsetNoiseMap);
SAMPLER(sampler_SpecularOffsetNoiseMap);

TEXTURE2D(_BumpMap);
SAMPLER(sampler_BumpMap);

CBUFFER_START(UnityPerMaterial)
	float _Surface;
	float4 _BaseMap_ST;
	float4 _BaseColor;
	float3 _LightTint;
	float3 _MiddleTint;
	float3 _ShadowTint;
	float  _AmbientStrength;
	float2 _ShadowThresholds;
	float4 _DiffuseThresholds;
	float _Smoothness;
	float _ConvertFromRoughness;
	float _Metallic;
	float _SpecularBoost;
	float _GIStrength;
	float4 _SpecularOffsetNoiseMap_ST;
	float  _SpecularOffsetNoiseStrength;
	float  _SpecularStrength;
	float3 _SpecularColor;
	float  _SpecularPower;
	float2 _SpecularThresholds;
	float3 _RimColor;
	float2 _RimThresholds;
	float _RimExtension;
	float  _BumpScale;
	float  _Cutoff;
CBUFFER_END

#ifdef UNITY_DOTS_INSTANCING_ENABLED

UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
	UNITY_DOTS_INSTANCED_PROP(float4, _BaseColor)
	UNITY_DOTS_INSTANCED_PROP(float3, _LightTint)
	UNITY_DOTS_INSTANCED_PROP(float3, _MiddleTint)
	UNITY_DOTS_INSTANCED_PROP(float3, _ShadowTint)
	UNITY_DOTS_INSTANCED_PROP(float , _AmbientStrength)
	UNITY_DOTS_INSTANCED_PROP(float2, _ShadowThresholds)
	UNITY_DOTS_INSTANCED_PROP(float4, _DiffuseThresholds)
	UNITY_DOTS_INSTANCED_PROP(float , _Smoothness)
	UNITY_DOTS_INSTANCED_PROP(float , _ConvertFromRoughness)
	UNITY_DOTS_INSTANCED_PROP(float , _Metallic)
	UNITY_DOTS_INSTANCED_PROP(float , _SpecularBoost)
	UNITY_DOTS_INSTANCED_PROP(float , _GIStrength)
	UNITY_DOTS_INSTANCED_PROP(float , _SpecularOffsetNoiseStrength)
	UNITY_DOTS_INSTANCED_PROP(float , _SpecularStrength)
	UNITY_DOTS_INSTANCED_PROP(float3, _SpecularColor)
	UNITY_DOTS_INSTANCED_PROP(float , _SpecularPower)
	UNITY_DOTS_INSTANCED_PROP(float2, _SpecularThresholds)
	UNITY_DOTS_INSTANCED_PROP(float3, _RimColor)
	UNITY_DOTS_INSTANCED_PROP(float2, _RimThresholds)
	UNITY_DOTS_INSTANCED_PROP(float , _RimExtension)
	UNITY_DOTS_INSTANCED_PROP(float , _BumpScale)
	UNITY_DOTS_INSTANCED_PROP(float , _Cutoff)
UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)

// Here, we want to avoid overriding a property like e.g. _BaseColor with something like this:
// #define _BaseColor UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _BaseColor0)
//
// It would be simpler, but it can cause the compiler to regenerate the property loading code for each use of _BaseColor.
//
// To avoid this, the property loads are cached in some static values at the beginning of the shader.
// The properties such as _BaseColor are then overridden so that it expand directly to the static value like this:
// #define _BaseColor unity_DOTS_Sampled_BaseColor
//
// This simple fix happened to improve GPU performances by ~10% on Meta Quest 2 with URP on some scenes.

static float4 unity_DOTS_Sampled_BaseColor;
static float3 unity_DOTS_Sampled_LightTint;
static float3 unity_DOTS_Sampled_MiddleTint;
static float3 unity_DOTS_Sampled_ShadowTint;
static float  unity_DOTS_Sampled_AmbientStrength;
static float2 unity_DOTS_Sampled_ShadowThresholds;
static float4 unity_DOTS_Sampled_DiffuseThresholds;
static float  unity_DOTS_Sampled_Smoothness;
static float  unity_DOTS_Sampled_ConvertFromRoughness;
static float  unity_DOTS_Sampled_Metallic;
static float  unity_DOTS_Sampled_SpecularBoost;
static float  unity_DOTS_Sampled_GIStrength;
static float  unity_DOTS_Sampled_SpecularOffsetNoiseStrength;
static float  unity_DOTS_Sampled_SpecularStrength;
static float3 unity_DOTS_Sampled_SpecularColor;
static float  unity_DOTS_Sampled_SpecularPower;
static float2 unity_DOTS_Sampled_SpecularThresholds;
static float3 unity_DOTS_Sampled_RimColor;
static float2 unity_DOTS_Sampled_RimThresholds;
static float  unity_DOTS_Sampled_RimExtension;
static float  unity_DOTS_Sampled_BumpScale;
static float  unity_DOTS_Sampled_Cutoff;

void SetupDOTSLitMaterialPropertyCaches()
{
    unity_DOTS_Sampled_BaseColor			= UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _BaseColor);
    unity_DOTS_Sampled_LightTint            = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float3, _LightTint);
    unity_DOTS_Sampled_MiddleTint			= UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float3, _MiddleTint);
    unity_DOTS_Sampled_ShadowTint			= UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float3, _ShadowTint);
    unity_DOTS_Sampled_AmbientStrength		= UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _AmbientStrength);
    unity_DOTS_Sampled_ShadowThresholds		= UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float2, _ShadowThresholds);
    unity_DOTS_Sampled_DiffuseThresholds	= UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _DiffuseThresholds);
    unity_DOTS_Sampled_Smoothness			= UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _Smoothness);
    unity_DOTS_Sampled_ConvertFromRoughness	= UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _ConvertFromRoughness);
    unity_DOTS_Sampled_Metallic				= UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _Metallic);
    unity_DOTS_Sampled_SpecularBoost		= UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _SpecularBoost);
    unity_DOTS_Sampled_GIStrength		    = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _GIStrength);
	unity_DOTS_Sampled_SpecularOffsetNoiseStrength	= UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _SpecularOffsetNoiseStrength);
    unity_DOTS_Sampled_SpecularStrength     = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _SpecularStrength);
    unity_DOTS_Sampled_SpecularColor        = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float3, _SpecularColor);
    unity_DOTS_Sampled_SpecularPower        = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _SpecularPower);
    unity_DOTS_Sampled_SpecularThresholds   = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float2, _SpecularThresholds);
    unity_DOTS_Sampled_RimColor				= UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float3, _RimColor);
    unity_DOTS_Sampled_RimThresholds        = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float2, _RimThresholds);
    unity_DOTS_Sampled_RimExtension			= UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _RimExtension);
    unity_DOTS_Sampled_BumpScale            = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _BumpScale);
    unity_DOTS_Sampled_Cutoff				= UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _Cutoff);
}

#undef UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES
#define UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES() SetupDOTSLitMaterialPropertyCaches()

#define _BaseColor				UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _BaseColor)
#define _LightTint				UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float3, _LightTint)
#define _MiddleTint				UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float3, _MiddleTint)
#define _ShadowTint				UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float3, _ShadowTint)
#define _AmbientStrength		UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _AmbientStrength)
#define _ShadowThresholds		UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float2, _ShadowThresholds)
#define _DiffuseThresholds		UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _DiffuseThresholds)
#define _Smoothness				UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _Smoothness)
#define _ConvertFromRoughness	UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _ConvertFromRoughness)
#define _Metallic				UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _Metallic)
#define _SpecularBoost			UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _SpecularBoost)
#define _GIStrength			    UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _GIStrength)
#define _SpecularOffsetNoiseStrength	UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _SpecularOffsetNoiseStrength)
#define _SpecularStrength		UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _SpecularStrength)
#define _SpecularColor			UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float3, _SpecularColor)
#define _SpecularPower			UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _SpecularPower)
#define _SpecularThresholds		UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float2, _SpecularThresholds)
#define _RimColor				UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float3, _RimColor)
#define _RimThresholds			UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float2, _RimThresholds)
#define _RimExtension			UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _RimExtension)
#define _BumpScale				UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _BumpScale)
#define _Cutoff					UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _Cutoff)

#endif

///////////////////////////////////////////////////////////////////////////////
//                      Material Property Helpers                            //
///////////////////////////////////////////////////////////////////////////////
half Alpha(half albedoAlpha, half4 color, half cutoff)
{
#if !defined(_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A) && !defined(_GLOSSINESS_FROM_BASE_ALPHA)
	half alpha = albedoAlpha * color.a;
#else
	half alpha = color.a;
#endif

	alpha = AlphaDiscard(alpha, cutoff);

	return alpha;
}

half4 SampleAlbedoAlpha(float2 uv, TEXTURE2D_PARAM(albedoAlphaMap, sampler_albedoAlphaMap))
{
	return half4(SAMPLE_TEXTURE2D(albedoAlphaMap, sampler_albedoAlphaMap, uv));
}

half3 SampleNormal(float2 uv, TEXTURE2D_PARAM(bumpMap, sampler_bumpMap), half scale = half(1.0))
{
#ifdef _NORMALMAP
	half4 n = SAMPLE_TEXTURE2D(bumpMap, sampler_bumpMap, uv);
#if BUMP_SCALE_NOT_SUPPORTED
	return UnpackNormal(n);
#else
	return UnpackNormalScale(n, scale);
#endif
#else
	return half3(0.0h, 0.0h, 1.0h);
#endif
}

half3 SampleEmission(float2 uv, half3 emissionColor, TEXTURE2D_PARAM(emissionMap, sampler_emissionMap))
{
#ifndef _EMISSION
	return 0;
#else
	return SAMPLE_TEXTURE2D(emissionMap, sampler_emissionMap, uv).rgb * emissionColor;
#endif
}

#endif // TOON_INPUT_SURFACE_INCLUDED
