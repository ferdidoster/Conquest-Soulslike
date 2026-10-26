// Flat, hard-edged splats for blood (and any other liquid or mark), Sable style: a wobbly blob
// cut out of a quad, a darker wet core, satellite droplets, and the toon shadow tint so a splat in
// shade sits in the shade. Used for ground decals and, with vertex colour, for particle droplets.
// The blob's shape is seeded by the object's position, so every decal is different.
Shader "Conquest/Flat Splat"
{
    Properties
    {
        [MainColor] _BaseColor ("Color", Color) = (0.55, 0.04, 0.06, 1)
        _CoreColor ("Wet Core", Color) = (0.42, 0.02, 0.05, 1)
        _CoreSize ("Core Size", Range(0, 1)) = 0.55
        _ShadowColor ("Shadow Tint", Color) = (0.62, 0.5, 0.62, 1)
        _EdgeNoise ("Edge Wobble", Range(0, 0.4)) = 0.18
        [Toggle] _Satellites ("Satellite Droplets", Float) = 1
        [Toggle] _UseVertexColor ("Use Vertex Colour", Float) = 0
        [Toggle] _ReceiveShadows ("Receive Shadows", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Cull Off
        Offset -1, -1

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _CoreColor;
                half _CoreSize;
                half4 _ShadowColor;
                half _EdgeNoise;
                half _Satellites;
                half _UseVertexColor;
                half _ReceiveShadows;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                half4 color : TEXCOORD2;
                float seed : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash(float n) { return frac(sin(n) * 43758.5453); }

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv;
                output.color = input.color;
                float3 origin = TransformObjectToWorld(float3(0, 0, 0));
                output.seed = Hash(dot(origin, float3(12.9898, 78.233, 37.719)));
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float2 p = input.uv * 2.0 - 1.0;
                float seed = input.seed * 97.0;

                // Main blob: a circle whose radius wobbles with the angle.
                float angle = atan2(p.y, p.x);
                float wobble = sin(angle * 3.0 + seed) * 0.5 + sin(angle * 7.0 + seed * 1.7) * 0.3 +
                               sin(angle * 13.0 + seed * 2.3) * 0.2;
                float radius = 0.62 + wobble * _EdgeNoise;
                float d = length(p) / radius;
                float inside = step(d, 1.0);

                // Satellite droplets scattered around the blob.
                if (_Satellites > 0.5)
                {
                    [unroll]
                    for (int i = 0; i < 6; i++)
                    {
                        float a = Hash(seed + i * 3.1) * 6.2831;
                        float dist = lerp(0.68, 0.92, Hash(seed + i * 5.7));
                        float r = lerp(0.035, 0.1, Hash(seed + i * 8.3));
                        float2 centre = float2(cos(a), sin(a)) * dist;
                        inside = max(inside, step(length(p - centre), r));
                    }
                }

                clip(inside - 0.5);

                half3 color = d < _CoreSize ? _CoreColor.rgb : _BaseColor.rgb;
                if (_UseVertexColor > 0.5) color *= input.color.rgb;

                if (_ReceiveShadows > 0.5)
                {
                    Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                    color = lerp(color * _ShadowColor.rgb, color, light.shadowAttenuation);
                }

                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
