// Flat three-colour sky for the skybox slot: a pale horizon haze fading to a deep zenith blue,
// with a soft sun disc. Keeps the sky as graphic as the rest of the Sable-style frame.
Shader "Conquest/Sky Gradient"
{
    Properties
    {
        _ZenithColor ("Zenith", Color) = (0.36, 0.56, 0.84, 1)
        _HorizonColor ("Horizon", Color) = (0.86, 0.8, 0.76, 1)
        _GroundColor ("Below Horizon", Color) = (0.78, 0.52, 0.42, 1)
        _Exponent ("Gradient Falloff", Range(0.1, 4)) = 0.6
        _SunColor ("Sun", Color) = (1, 0.96, 0.86, 1)
        _SunSize ("Sun Size", Range(0, 0.2)) = 0.035
        _CloudColor ("Cloud", Color) = (1, 1, 1, 1)
        _CloudShade ("Cloud Underside", Color) = (0.78, 0.8, 0.92, 1)
        _CloudScale ("Cloud Scale", Float) = 0.6
        _CloudCover ("Cloud Cover", Range(0, 1)) = 0.6
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            half4 _ZenithColor;
            half4 _HorizonColor;
            half4 _GroundColor;
            half _Exponent;
            half4 _SunColor;
            half _SunSize;
            half4 _CloudColor;
            half4 _CloudShade;
            float _CloudScale;
            half _CloudCover;

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x),
                            lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y);
            }

            float Fbm(float2 p)
            {
                return Noise(p) * 0.55 + Noise(p * 2.1 + 3.7) * 0.3 + Noise(p * 4.3 + 9.1) * 0.15;
            }

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 dir = normalize(input.direction);
                half up = saturate(dir.y);
                half3 color = lerp(_HorizonColor.rgb, _ZenithColor.rgb, pow(up, _Exponent));
                color = lerp(color, _GroundColor.rgb, saturate(-dir.y * 6.0));
                Light sun = GetMainLight();
                half sunDot = dot(dir, sun.direction);
                color = lerp(color, _SunColor.rgb, step(1.0 - _SunSize * _SunSize, sunDot));

                // flat, hard-edged clouds projected on a dome, drifting slowly
                float2 cloudUV = dir.xz / max(dir.y + 0.15, 0.05) * _CloudScale + _Time.y * float2(0.004, 0.0015);
                float n = Fbm(cloudUV);
                half cloud = step(_CloudCover, n) * smoothstep(0.02, 0.12, dir.y);
                half3 cloudColor = lerp(_CloudShade.rgb, _CloudColor.rgb, step(_CloudCover + 0.06, n));
                color = lerp(color, cloudColor, cloud);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
