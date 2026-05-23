Shader "NUNBORA/Interactable Glint Sweep"
{
    Properties
    {
        _SweepColor ("Sweep Color", Color) = (1, 1, 1, 1)
        _PeakIntensity ("Peak Intensity", Float) = 0.35
        _StartTime ("Start Time", Float) = 0
        _Duration ("Duration", Float) = 0.45
        _SweepOrigin ("Sweep Origin", Vector) = (0, 0, 0, 0)
        _SweepDirection ("Sweep Direction", Vector) = (1, 0, 0, 0)
        _SweepLength ("Sweep Length", Float) = 1
        _SweepWidth ("Sweep Width", Float) = 0.18
        _SweepSoftness ("Sweep Softness", Float) = 0.25
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+25"
        }

        Pass
        {
            Name "GlintSweep"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _SweepColor;
                float _PeakIntensity;
                float _StartTime;
                float _Duration;
                float4 _SweepOrigin;
                float4 _SweepDirection;
                float _SweepLength;
                float _SweepWidth;
                float _SweepSoftness;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalize(normalInputs.normalWS);
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(positionInputs.positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float duration = max(_Duration, 0.0001);
                float progress = saturate((_Time.y - _StartTime) / duration);
                float3 direction = normalize(_SweepDirection.xyz);
                float sweepLength = max(_SweepLength, 0.0001);
                float sweepPosition = progress * sweepLength;
                float objectPosition = dot(input.positionWS - _SweepOrigin.xyz, direction);
                float bandDistance = abs(objectPosition - sweepPosition);

                float width = max(_SweepWidth, 0.0001);
                float softness = max(_SweepSoftness, 0.0001);
                float band = 1.0 - smoothstep(width, width + softness, bandDistance);
                float lifetime = sin(progress * PI);
                float fresnel = pow(1.0 - saturate(dot(normalize(input.normalWS), normalize(input.viewDirWS))), 1.25);
                float alpha = band * lifetime * (0.35 + fresnel * 0.65) * _PeakIntensity;

                return half4(_SweepColor.rgb * _PeakIntensity, alpha * _SweepColor.a);
            }
            ENDHLSL
        }
    }
}
