Shader "HighStakes/Old Quarter Sky"
{
    Properties
    {
        _FogColor ("Fog Color", Color) = (0.39, 0.46, 0.51, 1)
        _SunDirection ("Sun Direction", Vector) = (0.45, 0.62, -0.65, 0)
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "OldQuarterAtmosphere.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _FogColor;
                float4 _SunDirection;
            CBUFFER_END

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            Varyings Vert(float3 positionOS : POSITION)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(positionOS);
                output.direction = positionOS;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 sky = OldQuarterSkyColor(normalize(input.direction), _SunDirection.xyz, _FogColor.rgb);
                return half4(sky, 1.0);
            }
            ENDHLSL
        }
    }
}
