// Ink lines for the whole frame, drawn by a Full Screen Pass Renderer Feature ("Conquest Outline").
// Edges are found where depth jumps (silhouettes) or the surface normal turns (creases), which
// gives the drawn, comic-book contour of Sable on flat-shaded geometry.
Shader "Conquest/Outline Fullscreen"
{
    Properties
    {
        _LineColor ("Line Colour", Color) = (0.16, 0.09, 0.1, 1)
        _Thickness ("Thickness (px)", Range(0.5, 4)) = 1.25
        _DepthThreshold ("Depth Threshold", Range(0.001, 0.5)) = 0.04
        _NormalThreshold ("Normal Threshold", Range(0.01, 1)) = 0.35
        _FadeStart ("Fade Start (m)", Float) = 70
        _FadeEnd ("Fade End (m)", Float) = 160
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend Off

        Pass
        {
            Name "Outline"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            half4 _LineColor;
            float _Thickness;
            float _DepthThreshold;
            float _NormalThreshold;
            float _FadeStart;
            float _FadeEnd;

            float EyeDepth(float2 uv)
            {
                float raw = SampleSceneDepth(uv);
                if (unity_OrthoParams.w > 0.5)
                {
                    #if UNITY_REVERSED_Z
                    raw = 1.0 - raw;
                    #endif
                    return lerp(_ProjectionParams.y, _ProjectionParams.z, raw);
                }
                return LinearEyeDepth(raw, _ZBufferParams);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv);

                float2 texel = _Thickness / _ScreenParams.xy;
                float2 offsets[4] = { float2(texel.x, 0), float2(-texel.x, 0), float2(0, texel.y), float2(0, -texel.y) };

                float depth = EyeDepth(uv);
                float3 normal = SampleSceneNormals(uv);
                float depthEdge = 0;
                float normalEdge = 0;
                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    float d = EyeDepth(uv + offsets[i]);
                    // relative depth jump, only counted on the far side so lines stay one pixel wide
                    depthEdge = max(depthEdge, (d - depth) / max(depth, 0.001));
                    // a crease only counts when the other surface spans two pixels: a sub-pixel
                    // strip or lip is hit by one tap here and there and would draw as dashes
                    float3 n1 = SampleSceneNormals(uv + offsets[i]);
                    float3 n2 = SampleSceneNormals(uv + offsets[i] * 2.0);
                    normalEdge = max(normalEdge, min(1.0 - dot(normal, n1), 1.0 - dot(normal, n2)));
                }

                float edge = max(step(_DepthThreshold, depthEdge), step(_NormalThreshold, normalEdge));
                // nothing to outline on the sky
                edge *= step(depth, _ProjectionParams.z * 0.99);
                edge *= 1.0 - saturate((depth - _FadeStart) / max(_FadeEnd - _FadeStart, 0.001));

                // never lighter than the scene: in dark levels a fixed line colour would glow
                half3 ink = min(_LineColor.rgb, scene.rgb * 0.55);
                return half4(lerp(scene.rgb, ink, edge * _LineColor.a), scene.a);
            }
            ENDHLSL
        }
    }
}
