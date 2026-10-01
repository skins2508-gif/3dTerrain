Shader "VillageValley/GrassFlowerWind"
{
    Properties { _MainTex("Texture", 2D) = "white" {} _Cutoff("Alpha Cutoff", Range(0,1)) = 0.3 _WindStrength("Wind Strength", Float) = 0.38 _WindSpeed("Wind Speed", Float) = 1.25 _WindColor("Wind Color", Color) = (1,0.08,0.04,1) _WindColorStrength("Wind Color Strength", Range(0,1)) = 0.72 _MaxDistance("Max Distance", Float) = 320 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        Cull Off
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float4 _MainTex_ST; float4 _WindColor; float _Cutoff, _WindStrength, _WindSpeed, _WindColorStrength, _MaxDistance; float3 _CameraPosition;
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionHCS:SV_POSITION; float2 uv:TEXCOORD0; float2 fadeWind:TEXCOORD1; };
            Varyings vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                float phase = world.x * 0.071 + world.z * 0.053 + _Time.y * _WindSpeed;
                world.xz += float2(sin(phase), cos(phase * 0.83)) * _WindStrength * input.positionOS.y;
                output.positionHCS = TransformWorldToHClip(world);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.fadeWind.x = saturate((_MaxDistance - distance(world, _CameraPosition)) / 35.0);
                output.fadeWind.y = saturate(abs(sin(phase)) * _WindStrength);
                return output;
            }
            half4 frag(Varyings input):SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                clip(color.a * input.fadeWind.x - _Cutoff);
                color.rgb = lerp(color.rgb, _WindColor.rgb, input.fadeWind.y * _WindColorStrength);
                return color;
            }
            ENDHLSL
        }
    }
}
