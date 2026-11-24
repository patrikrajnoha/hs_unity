Shader "HighStakes/Boundary Fog"
{
    Properties
    {
        _FogColor ("Fog Color", Color) = (0.39, 0.46, 0.51, 1)
        _SunDirection ("Sun Direction", Vector) = (0.45, 0.62, -0.65, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Cull Front

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
                float3 positionWS : TEXCOORD0;
            };

            Varyings Vert(float3 positionOS : POSITION)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(positionOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 position = input.positionWS;
                float3 direction = normalize(position - GetCameraPositionWS());
                float3 cloudPosition = position * float3(0.09, 0.24, 0.09);
                float clouds = AtmosphereNoise(cloudPosition) * 0.75 + AtmosphereNoise(cloudPosition * 2.07) * 0.25;
                float heightFade = 1.0 - smoothstep(1.0, 21.0, position.y);
                float haze = heightFade * lerp(0.07, 0.23, clouds);
                float3 fog = lerp(_FogColor.rgb, float3(0.58, 0.67, 0.72), haze);
                float3 sky = OldQuarterSkyColor(direction, _SunDirection.xyz, _FogColor.rgb);
                float skyVisibility = smoothstep(-0.03, 0.19, direction.y);
                return half4(lerp(fog, sky, skyVisibility), 1.0);
            }
            ENDHLSL
        }
    }
}
