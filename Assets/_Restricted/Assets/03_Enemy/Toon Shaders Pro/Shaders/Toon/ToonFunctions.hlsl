#ifndef TOON_FUNCTIONS_INCLUDED
#define TOON_FUNCTIONS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// BRDF functions derived from Unity's BRDF.hlsl.

half3 ToonGlossyEnvironmentReflection(half3 reflectVector, half perceptualRoughness, half occlusion)
{
#if !defined(_ENVIRONMENTREFLECTIONS_OFF)
    half3 irradiance;
    half mip = PerceptualRoughnessToMipmapLevel(perceptualRoughness);
    half4 encodedIrradiance = half4(SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, reflectVector, mip));

    return encodedIrradiance * occlusion;
#else

    return _GlossyEnvironmentColor.rgb * occlusion;
#endif // _ENVIRONMENTREFLECTIONS_OFF
}

half3 ToonGlobalIllumination(BRDFData brdfData,
    half3 bakedGI, half occlusion,
    half3 normalWS, half3 viewDirectionWS)
{
    half3 reflectVector = reflect(-viewDirectionWS, normalWS);
    half NoV = saturate(dot(normalWS, viewDirectionWS));
    half fresnelTerm = Pow4(1.0 - NoV);

    half3 indirectDiffuse = bakedGI;
    half3 indirectSpecular = ToonGlossyEnvironmentReflection(reflectVector, brdfData.perceptualRoughness, half(1.0));

    half3 color = EnvironmentBRDF(brdfData, indirectDiffuse, indirectSpecular, fresnelTerm);

    return color * occlusion;
}

void ToonBakedGIData(v2f input, inout InputData inputData)
{
#if defined(DYNAMICLIGHTMAP_ON)
    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.dynamicLightmapUV, input.vertexSH, inputData.normalWS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
#elif !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
    inputData.bakedGI = SAMPLE_GI(input.vertexSH,
        GetAbsolutePositionWS(inputData.positionWS),
        inputData.normalWS,
        inputData.viewDirectionWS,
        input.positionCS.xy,
        input.probeOcclusion,
        inputData.shadowMask);
#else
    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, inputData.normalWS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
#endif
}

inline void ToonBRDFDataDirect(half3 albedo, half3 diffuse, half3 specular, half reflectivity, half oneMinusReflectivity, half smoothness, inout half alpha, out BRDFData outBRDFData)
{
    outBRDFData = (BRDFData) 0;
    outBRDFData.albedo = albedo;
    outBRDFData.diffuse = diffuse;
    outBRDFData.specular = specular;
    outBRDFData.reflectivity = reflectivity;

    outBRDFData.perceptualRoughness = PerceptualSmoothnessToPerceptualRoughness(smoothness);
    outBRDFData.roughness = max(PerceptualRoughnessToRoughness(outBRDFData.perceptualRoughness), HALF_MIN_SQRT);
    outBRDFData.roughness2 = max(outBRDFData.roughness * outBRDFData.roughness, HALF_MIN);
    outBRDFData.grazingTerm = saturate(smoothness + reflectivity);
    outBRDFData.normalizationTerm = outBRDFData.roughness * half(4.0) + half(2.0);
    outBRDFData.roughness2MinusOne = outBRDFData.roughness2 - half(1.0);
}

// Initialize BRDFData for material, managing both specular and metallic setup using shader keyword _SPECULAR_SETUP.
inline void ToonBRDFData(half3 albedo, half metallic, half3 specular, half smoothness, inout half alpha, out BRDFData outBRDFData)
{
#ifdef _SPECULAR_SETUP
    half reflectivity = ReflectivitySpecular(specular);
    half oneMinusReflectivity = half(1.0) - reflectivity;
    half3 brdfDiffuse = albedo * oneMinusReflectivity;
    half3 brdfSpecular = specular;
#else
    half oneMinusReflectivity = OneMinusReflectivityMetallic(metallic);
    half reflectivity = half(1.0) - oneMinusReflectivity;
    half3 brdfDiffuse = albedo * oneMinusReflectivity;
    half3 brdfSpecular = lerp(kDielectricSpec.rgb, albedo, metallic);
#endif

    ToonBRDFDataDirect(albedo, brdfDiffuse, brdfSpecular, reflectivity, oneMinusReflectivity, smoothness, alpha, outBRDFData);
}

inline half3 CalculateToonLighting(BRDFData brdfData, Light light, half3 normalWS, half3 viewWS, float specularOffset)
{
#ifdef _LIGHT_LAYERS
    uint meshRenderingLayers = GetMeshRenderingLayer();
    if (!IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
    {
        return 0;
    }
#endif

    // Diffuse calculations (n dot l).
    half NdotL = dot(normalWS, light.direction);
    half rawDiffuseAmount = smoothstep(_DiffuseThresholds.x, _DiffuseThresholds.y, NdotL);

    // Light attenuation.
    half lightAtten = saturate(light.distanceAttenuation);

#ifdef _RECEIVE_SHADOWS_ON
    lightAtten *= smoothstep(_ShadowThresholds.x, _ShadowThresholds.y, light.shadowAttenuation);
#endif

    // Calculate radiance from diffuse values.
#ifdef _USE_SECOND_THRESHOLD
    half diffuseAmount = rawDiffuseAmount + smoothstep(_DiffuseThresholds.z, _DiffuseThresholds.w, NdotL);
    half3 radiance = lerp(lerp(_ShadowTint, _MiddleTint, saturate(diffuseAmount)), _LightTint, saturate(diffuseAmount - 1.0f));
#else
    half3 radiance = lerp(_ShadowTint, _LightTint, saturate(rawDiffuseAmount));
#endif

    radiance *= light.color * lightAtten;

    half3 brdf = brdfData.diffuse;

    // Specular calculations.
    float3 halfDir = SafeNormalize(light.direction + viewWS);
    float NoH = saturate(dot(normalWS, halfDir));
    half LoH = half(saturate(dot(light.direction, halfDir)));

    float d = NoH * NoH * brdfData.roughness2MinusOne + 1.00001f;

    half LoH2 = LoH * LoH;
    half specularTerm = brdfData.roughness2 / ((d * d) * max(0.1h, LoH2) * brdfData.normalizationTerm);

#if REAL_IS_HALF
    specularTerm = specularTerm - HALF_MIN;
    specularTerm = clamp(specularTerm, 0.0, 1000.0); // Prevent FP16 overflow on mobiles
#endif
    
    specularTerm = smoothstep(_SpecularThresholds.x, _SpecularThresholds.y, specularTerm + specularOffset);
    specularTerm *= _SpecularBoost;
    
    brdf += brdfData.specular * specularTerm;

    // Rim lighting.
    float rimAmount = (1.0f - saturate(dot(normalWS, viewWS))) * saturate(rawDiffuseAmount + _RimExtension);
    rimAmount = smoothstep(_RimThresholds.x, _RimThresholds.y, rimAmount);
    half3 rimColor = rimAmount * _RimColor;

    // Combine lighting types.
    return brdf * radiance + rimColor;
}

#endif // TOON_FUNCTIONS_INCLUDED
