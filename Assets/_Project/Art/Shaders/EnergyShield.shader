// Faceted energy bubble for blocking: a faint fill, a bright rim cut into a few hard bands,
// scanlines drifting up, a ring rippling out from the last blocked hit, and facets that
// dissolve away when the shield drops or shatters. The EnergyShield component drives the
// per-renderer values (_Intensity, _HitPosOS, _HitAge, _Dissolve) through a property block.
Shader "Conquest/Energy Shield"
{
    Properties
    {
        [MainColor] _BaseColor ("Colour", Color) = (0.1, 0.5, 1, 1)
        _RimColor ("Rim Colour", Color) = (0.45, 0.9, 1, 1)
        _FillAlpha ("Fill Alpha", Range(0, 1)) = 0.05
        _RimAlpha ("Rim Alpha", Range(0, 1)) = 0.95
        _RimPower ("Rim Power", Range(0.5, 6)) = 2.6
        _Bands ("Rim Bands", Range(1, 6)) = 3
        _ScanDensity ("Scanlines per Metre", Float) = 9
        _ScanSpeed ("Scanline Speed", Float) = 0.8
        _ScanAlpha ("Scanline Alpha", Range(0, 1)) = 0.1
        _RippleSpeed ("Ripple Speed", Float) = 4.5
        _RippleWidth ("Ripple Width", Float) = 0.14
        _RippleTime ("Ripple Duration", Float) = 0.45
        _Intensity ("Intensity", Range(0, 2)) = 1
        _HitPosOS ("Hit Position (object)", Vector) = (0, 0, 0, 0)
        _HitAge ("Hit Age", Float) = 99
        _Dissolve ("Dissolve", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _RimColor;
                half _FillAlpha;
                half _RimAlpha;
                half _RimPower;
                half _Bands;
                float _ScanDensity;
                float _ScanSpeed;
                half _ScanAlpha;
                float _RippleSpeed;
                float _RippleWidth;
                float _RippleTime;
                half _Intensity;
                float4 _HitPosOS;
                float _HitAge;
                half _Dissolve;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionOS = input.positionOS.xyz;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            float Hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Each facet dissolves as a whole, at its own threshold.
                float3 facet = floor(input.positionOS * 6.0);
                clip(Hash(facet) - _Dissolve);

                float3 n = normalize(input.normalWS);
                float3 v = normalize(GetWorldSpaceViewDir(input.positionWS));
                half fresnel = pow(1.0 - saturate(abs(dot(n, v))), _RimPower);
                half rim = ceil(fresnel * _Bands) / _Bands;

                half scan = step(frac(input.positionWS.y * _ScanDensity - _Time.y * _ScanSpeed), 0.18) * _ScanAlpha;

                float distance = length(input.positionOS - _HitPosOS.xyz);
                half life = saturate(1.0 - _HitAge / _RippleTime);
                half ring = step(abs(distance - _HitAge * _RippleSpeed), _RippleWidth) * life;
                half spot = step(distance, 0.22 * life) * life * 0.6;
                half hit = saturate(ring + spot);

                half alpha = _FillAlpha + rim * _RimAlpha + scan + hit;
                half3 color = lerp(_BaseColor.rgb, _RimColor.rgb, saturate(rim + hit));
                alpha = saturate(alpha * _Intensity);
                color *= max(1.0, _Intensity);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
