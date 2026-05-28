#ifndef CUSTOM_MIX_FOG_INCLUDED
#define CUSTOM_MIX_FOG_INCLUDED

void MixUnityFog_float(float4 Color, float FogFactor, out float4 FogColor)
{
#ifdef SHADERGRAPH_PREVIEW
    FogColor = Color;
#else
    float fogFactor = ComputeFogFactor(FogFactor);
    FogColor = float4(MixFog(Color.rgb, fogFactor), Color.a);
#endif
}

void MixUnityFog_half(half4 Color, half FogFactor, out half4 FogColor)
{
#ifdef SHADERGRAPH_PREVIEW
    FogColor = Color;
#else
    half fogFactor = ComputeFogFactor(FogFactor);
    FogColor = half4(MixFog(Color.rgb, fogFactor), Color.a);
#endif
}

#endif // CUSTOM_MIX_FOG_INCLUDED
