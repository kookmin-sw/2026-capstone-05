#ifndef CUSTOM_TOON_LIGHTING_INCLUDED
#define CUSTOM_TOON_LIGHTING_INCLUDED

// Required to avoid errors when using max 1 shadow cascade.
#ifndef SHADERGRAPH_PREVIEW
	#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
	#if (SHADERPASS != SHADERPASS_FORWARD)
		#undef REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR
	#endif
#endif

#ifndef SHADERGRAPH_PREVIEW
inline float2 CalculateDiffuse(Light light, float3 normalWS, float2 shadowThresholds, float4 diffuseThresholds)
{
    float rawDiffuseAmount = dot(normalWS, light.direction);
    float diffuseAmount = smoothstep(diffuseThresholds.x, diffuseThresholds.y, rawDiffuseAmount);
    
#ifdef _USE_SECOND_THRESHOLD
    diffuseAmount += smoothstep(diffuseThresholds.z, diffuseThresholds.w, rawDiffuseAmount);
#endif

    diffuseAmount *= saturate(light.distanceAttenuation);
    rawDiffuseAmount *= saturate(light.distanceAttenuation);

#ifdef _RECEIVE_SHADOWS_ON
    float shadow = smoothstep(shadowThresholds.x, shadowThresholds.y, light.shadowAttenuation);
    diffuseAmount *= shadow;
    rawDiffuseAmount *= shadow;
#endif
    
#ifdef _LIGHT_LAYERS
    uint meshRenderingLayers = GetMeshRenderingLayer();
    if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
    {
        return float2(diffuseAmount, rawDiffuseAmount);
    }
    else
    {
        return 0;
    }
#endif
    
    return float2(diffuseAmount, rawDiffuseAmount);
}

inline float CalculateSpecular(Light light, float3 normalWS, float3 viewWS, float2 specularThresholds, float specularPower, float diffuse, float specularStrength, float specularOffset)
{
    float specularAmount = (pow(saturate(dot(reflect(light.direction, normalWS), -viewWS) + specularOffset), specularPower)) * diffuse;
    specularAmount = smoothstep(specularThresholds.x, specularThresholds.y, specularAmount);
    specularAmount *= specularStrength;

    return specularAmount;
}

inline float CalculateRim(Light light, float3 normalWS, float3 viewWS, float2 rimThresholds, float diffuse)
{
    float rimAmount = (1.0f - saturate(dot(normalWS, viewWS))) * diffuse;
    rimAmount = smoothstep(rimThresholds.x, rimThresholds.y, rimAmount);

    return rimAmount;
}

inline float3 diffuseTint(float diffuse, float3 shadowTint, float3 middleTint, float3 lightTint, float3 lightColor)
{
#ifdef _USE_SECOND_THRESHOLD
    return lerp(lerp(shadowTint, middleTint, saturate(diffuse)), lightTint, saturate(diffuse - 1.0f));
#else
    return lerp(shadowTint, lightTint, saturate(diffuse));
#endif
}

#endif

void CalculateToonLighting_float(float3 PositionWS, float3 NormalWS, float3 ViewWS, float4 DynamicLightmapUV, float AmbientStrength, float2 ShadowThresholds, float4 DiffuseThresholds, float3 ShadowTint, float3 MiddleTint, float3 LightTint, float2 SpecularThresholds, float3 SpecularColor, float SpecularPower, float SpecularStrength, float SpecularOffset, float2 RimThresholds, float RimExtension, float3 RimColor,
    out float3 AmbientLight, out float3 DiffuseLight, out float3 SpecularLight, out float3 RimLight)
{
#ifdef SHADERGRAPH_PREVIEW
    AmbientLight = 0.15f;
    DiffuseLight = saturate(dot(NormalWS, normalize(float3(1.0f, 1.0f, 0.0f))));
    DiffuseLight = smoothstep(DiffuseThresholds.x, DiffuseThresholds.y, DiffuseLight);
    
    SpecularLight = (pow(dot(reflect(normalize(float3(1.0f, 1.0f, 0.0f)), NormalWS), -ViewWS), 300)) * DiffuseLight;
    SpecularLight = smoothstep(SpecularThresholds.x, SpecularThresholds.y, SpecularLight);
    
    RimLight = (1.0f - saturate(dot(NormalWS, ViewWS))) * DiffuseLight;
    RimLight = smoothstep(RimThresholds.x, RimThresholds.y, RimLight);
#else
    #if defined(_MAIN_LIGHT_SHADOWS_SCREEN) && !defined(_SURFACE_TYPE_TRANSPARENT)
        half4 shadowCoord = ComputeScreenPos(TransformWorldToHClip(PositionWS));
    #else
        half4 shadowCoord = TransformWorldToShadowCoord(PositionWS);
    #endif
    
    //OUTPUT_LIGHTMAP_UV(LightmapUV, unity_LightmapST, LightmapUV);
    float2 lightmapUV = DynamicLightmapUV.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
    float4 shadowMask = SAMPLE_SHADOWMASK(lightmapUV);
    
    float3 ambientLight = SampleSH(NormalWS) * AmbientStrength;
    
    Light mainLight = GetMainLight(shadowCoord, PositionWS, shadowMask);
    float2 diffuseAmount = CalculateDiffuse(mainLight, NormalWS, ShadowThresholds, DiffuseThresholds);
    float specularAmount = CalculateSpecular(mainLight, NormalWS, ViewWS, SpecularThresholds, SpecularPower, diffuseAmount.x, SpecularStrength, SpecularOffset);
    float rimAmount = CalculateRim(mainLight, NormalWS, ViewWS, RimThresholds, saturate(diffuseAmount.y + RimExtension));
    
    AmbientLight = ambientLight;
    DiffuseLight = diffuseTint(diffuseAmount.x, ShadowTint, MiddleTint, LightTint, mainLight.color);
    SpecularLight = specularAmount * SpecularColor * mainLight.color;
    RimLight = rimAmount * RimColor * mainLight.color;
    
    #ifdef _ADDITIONAL_LIGHTS
    
        uint lightCount = GetAdditionalLightsCount();
    
        #ifdef _FORWARD_PLUS
            InputData inputData = (InputData)0;
            inputData.positionWS = PositionWS;
            inputData.normalWS = NormalWS;
            inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(PositionWS);
            inputData.shadowCoord = shadowCoord;

            // Apply secondary lights (Forward+ rendering).
            LIGHT_LOOP_BEGIN(lightsCount)

                Light light = GetAdditionalLight(lightIndex, PositionWS, shadowMask);
                diffuseAmount = CalculateDiffuse(light, NormalWS, ShadowThresholds, DiffuseThresholds);
                specularAmount = CalculateSpecular(light, NormalWS, ViewWS, SpecularThresholds, SpecularPower, diffuseAmount.x, SpecularStrength, SpecularOffset);
                rimAmount = CalculateRim(light, NormalWS, ViewWS, RimThresholds, saturate(diffuseAmount.y + RimExtension));

                // Combine diffuse and specular and apply final tint color.
                DiffuseLight += diffuseTint(diffuseAmount.x, ShadowTint, MiddleTint, LightTint, light.color);
                SpecularLight += specularAmount * SpecularColor * light.color;
                RimLight += rimAmount * RimColor * light.color;
            LIGHT_LOOP_END
#else
            // Apply secondary lights (Forward rendering).
            for (uint lightIndex = 0; lightIndex < lightCount; ++lightIndex) 
            {
                Light light = GetAdditionalLight(lightIndex, PositionWS, shadowMask);
                diffuseAmount = CalculateDiffuse(light, NormalWS, ShadowThresholds, DiffuseThresholds);
                specularAmount = CalculateSpecular(light, NormalWS, ViewWS, SpecularThresholds, SpecularPower, diffuseAmount.x, SpecularStrength, SpecularOffset);
                rimAmount = CalculateRim(light, NormalWS, ViewWS, RimThresholds, saturate(diffuseAmount.y + RimExtension));

                // Combine diffuse and specular and apply final tint color.
                DiffuseLight += diffuseTint(diffuseAmount.x, ShadowTint, MiddleTint, LightTint, light.color);
                SpecularLight += specularAmount * SpecularColor * light.color;
                RimLight += rimAmount * RimColor * light.color;
            }
#endif
    
    #endif
    
#endif
}

// Get parameters from the main light - usually the scene's primary directional light.
void MainLight_float(float3 PositionWS, 
    out float3 Direction, out float3 Color, out float DistanceAttenuation, out float ShadowAttenuation)
{
    #ifdef SHADERGRAPH_PREVIEW
        Direction = normalize(float3(1.0f, 1.0f, 0.0f));
        Color = 1.0f;
        DistanceAttenuation = 1.0f;
        ShadowAttenuation = 1.0f;
    #else
        Light mainLight = GetMainLight();
        Direction = normalize(mainLight.direction);
        Color = mainLight.color;
        DistanceAttenuation = mainLight.distanceAttenuation;

        #if defined(_MAIN_LIGHT_SHADOWS_SCREEN) && !defined(_SURFACE_TYPE_TRANSPARENT)
		    float4 shadowCoord = ComputeScreenPos(TransformWorldToHClip(PositionWS));
		#else
		    float4 shadowCoord = TransformWorldToShadowCoord(PositionWS);
		#endif
		ShadowAttenuation = MainLightShadow(shadowCoord, PositionWS, float4(1, 1, 1, 1), _MainLightOcclusionProbes);
    #endif
}

void MainLight_half(half3 PositionWS, 
    out half3 Direction, out half3 Color, out half DistanceAttenuation, out half ShadowAttenuation)
{
    #ifdef SHADERGRAPH_PREVIEW
        Direction = normalize(half3(1.0f, 1.0f, 0.0f));
        Color = 1.0f;
        DistanceAttenuation = 1.0f;
        ShadowAttenuation = 1.0f;
    #else
        Light mainLight = GetMainLight();
        Direction = normalize(mainLight.direction);
        Color = mainLight.color;
        DistanceAttenuation = mainLight.distanceAttenuation;

        #if defined(_MAIN_LIGHT_SHADOWS_SCREEN) && !defined(_SURFACE_TYPE_TRANSPARENT)
		    half4 shadowCoord = ComputeScreenPos(TransformWorldToHClip(PositionWS));
		#else
		    half4 shadowCoord = TransformWorldToShadowCoord(PositionWS);
		#endif
		ShadowAttenuation = MainLightShadow(shadowCoord, PositionWS, half4(1, 1, 1, 1), _MainLightOcclusionProbes);
    #endif
}

// Modify the world normals according to a normal map.
void ApplyNormalMap_float(float3 NormalSample, float3 WorldNormal, float4 WorldTangent, out float3 OutNormal)
{
    float3 binormal = cross(WorldNormal, WorldTangent.xyz);

    OutNormal = normalize(
        NormalSample.x * WorldTangent.xyz +
        NormalSample.y * binormal +
        NormalSample.z * WorldNormal
    );
}

void ApplyNormalMap_half(half3 NormalSample, half3 WorldNormal, half4 WorldTangent, out half3 OutNormal)
{
    half3 binormal = cross(WorldNormal, WorldTangent.xyz) * (WorldTangent.w * unity_WorldTransformParams.w);

    OutNormal = normalize(
        NormalSample.x * WorldTangent.xyz +
        NormalSample.y * binormal +
        NormalSample.z * WorldNormal
    );
}

#endif // CUSTOM_TOON_LIGHTING_INCLUDED
