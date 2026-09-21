Shader "TideAndTill/StylizedFoliage"
{
    Properties
    {
        _BaseColor ("Leaf Color", Color) = (0.12,0.55,0.24,1)
        _Smoothness ("Smoothness", Range(0,1)) = 0.05
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 200
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Smoothness;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                float sway = sin(_Time.y * 1.35 + world.x * 0.34 + world.z * 0.28) * 0.055;
                world.xz += float2(sway, sway * 0.55) * saturate(input.positionOS.y + 0.3);
                output.positionWS = world;
                output.positionCS = TransformWorldToHClip(world);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                output.shadowCoord = TransformWorldToShadowCoord(world);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half3 normal = normalize(input.normalWS);
                Light mainLight = GetMainLight(input.shadowCoord);
                half lightBand = smoothstep(0.0h, 0.68h, saturate(dot(normal, mainLight.direction)));
                half3 ambient = SampleSH(normal) * 0.72h;
                half3 direct = mainLight.color * (0.25h + lightBand * 0.86h) * mainLight.shadowAttenuation;
                half heightTint = saturate(input.positionWS.y * 0.035h);
                half3 albedo = _BaseColor.rgb * lerp(0.84h, 1.12h, heightTint);
                half3 color = MixFog(albedo * (ambient + direct), input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
    FallBack "Universal Render Pipeline/Lit"
}
