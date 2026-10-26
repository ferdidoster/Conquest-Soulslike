// Flat weapon swoosh: vertex-coloured ribbon whose fade is cut into a few hard bands, with an
// ink line along the outer edge, so the trail reads like a drawn speed arc rather than a glow.
Shader "Conquest/Flat Trail"
{
    Properties
    {
        [MainColor] _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _Bands ("Fade Bands", Range(1, 8)) = 3
        _EdgeColor ("Edge Line", Color) = (0.18, 0.1, 0.12, 0.8)
        _EdgeWidth ("Edge Width", Range(0, 0.3)) = 0.06
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

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
                half _Bands;
                half4 _EdgeColor;
                half _EdgeWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color * _BaseColor;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half alpha = ceil(saturate(input.color.a) * _Bands) / _Bands * step(0.02, input.color.a);
                half3 color = input.color.rgb;
                // Ink line on the outer (tip) edge while the trail is fresh.
                if (input.uv.y > 1.0 - _EdgeWidth && input.color.a > 0.3)
                {
                    color = _EdgeColor.rgb;
                    alpha = max(alpha, _EdgeColor.a);
                }
                clip(alpha - 0.01);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
