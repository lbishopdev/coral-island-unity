Shader "TideAndTill/StylizedWater"
{
    Properties
    {
        _BaseColor ("Water Color", Color) = (0.05,0.62,0.74,0.72)
        _Smoothness ("Smoothness", Range(0,1)) = 0.8
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        LOD 200
        Pass
        {
            Name "WaterForward"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Smoothness;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half fogFactor : TEXCOORD1;
                half wave : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                float waveA = sin(world.x * 0.18 + _Time.y * 0.9);
                float waveB = sin(world.z * 0.23 - _Time.y * 0.72);
                world.y += (waveA + waveB) * 0.055;
                output.positionWS = world;
                output.positionCS = TransformWorldToHClip(world);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                output.wave = waveA * waveB;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half ripples = pow(saturate(0.5h + 0.5h * sin(input.positionWS.x * 0.7h + input.positionWS.z * 0.58h + _Time.y * 1.6h)), 7.0h);
                half horizon = saturate(input.positionWS.y * 0.1h + 0.55h);
                half3 color = _BaseColor.rgb * lerp(0.76h, 1.13h, horizon);
                color += ripples * half3(0.22h, 0.35h, 0.32h);
                color = MixFog(color, input.fogFactor);
                return half4(color, _BaseColor.a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
