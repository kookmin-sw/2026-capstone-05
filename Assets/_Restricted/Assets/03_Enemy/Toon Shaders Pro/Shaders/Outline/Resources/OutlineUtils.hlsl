#ifndef TOON_OUTLINE_UTILS_INCLUDED
#define TOON_OUTLINE_UTILS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

float GetSceneDepth(float2 uv)
{
#if UNITY_REVERSED_Z
    return SampleSceneDepth(uv);
#else
    return lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(uv));
#endif
}

#endif // TOON_OUTLINE_UTILS_INCLUDED
