#ifndef CONQUEST_TOON_COMMON_INCLUDED
#define CONQUEST_TOON_COMMON_INCLUDED

// Shared by every pass of Conquest/Toon Flat so the SRP Batcher sees one material layout.

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4 _BaseColor;
    half4 _ShadowColor;
    half _ShadowThreshold;
    half _ShadowSoftness;
    half _GritStrength;
    half _GritScale;
    half _VertexColorStrength;
    half4 _EmissionColor;
    half _SwayStrength;
    half _SwaySpeed;
CBUFFER_END

struct ToonAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv : TEXCOORD0;
    half4 color : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

// Wind for cloth: the mesh's vertex colour alpha says how free a vertex is (0 = tied down).
float3 ToonSway(float3 positionWS, half weight)
{
    float phase = _Time.y * _SwaySpeed + positionWS.x * 0.35 + positionWS.z * 0.27;
    float gust = sin(phase) * 0.7 + sin(phase * 2.3 + 1.7) * 0.3;
    return positionWS + float3(1.0, 0.15, 0.6) * (gust * _SwayStrength * weight);
}

float3 ToonWorldPosition(ToonAttributes input)
{
    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    return ToonSway(positionWS, input.color.a * _VertexColorStrength);
}

float ToonHash(float3 p)
{
    p = frac(p * 0.3183099 + 0.1);
    p *= 17.0;
    return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
}

float ToonNoise(float3 x)
{
    float3 i = floor(x);
    float3 f = frac(x);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(lerp(ToonHash(i + float3(0, 0, 0)), ToonHash(i + float3(1, 0, 0)), f.x),
                     lerp(ToonHash(i + float3(0, 1, 0)), ToonHash(i + float3(1, 1, 0)), f.x), f.y),
                lerp(lerp(ToonHash(i + float3(0, 0, 1)), ToonHash(i + float3(1, 0, 1)), f.x),
                     lerp(ToonHash(i + float3(0, 1, 1)), ToonHash(i + float3(1, 1, 1)), f.x), f.y), f.z);
}

// Procedural wear: large blotches plus fine speckle, in world space so modular pieces line up.
half ToonGrit(float3 positionWS)
{
    float3 p = positionWS * _GritScale;
    float blotch = ToonNoise(p * 0.35);
    float speckle = ToonNoise(p * 4.1);
    float grit = blotch * 0.65 + speckle * 0.35;
    // posterise slightly so the wear reads as painted patches, not smooth noise
    grit = floor(grit * 4.0 + 0.5) / 4.0;
    return 1.0 + _GritStrength * (grit - 0.5);
}

// Sections (LevelSections): the section the player is in is drawn normally, the rest of the level
// dimmed toward _ConquestSectionTint (glow partly kept, so lamps still show the way). Kit renderers
// on rendering layer 8 are dimmed per pixel from the section map, layer 10 dims a whole renderer,
// and layer 9 marks a cut-away wall, drawn only up to _ConquestCutHeight.
// _ConquestSection is 0 when sections are off, otherwise the current section index + 1.
TEXTURE2D(_ConquestSectionMap);
SAMPLER(sampler_ConquestSectionMap);
float4x4 _ConquestSectionWorldToMap;
float _ConquestSection;
float _ConquestSectionHasMap;
half4 _ConquestSectionTint;
half _ConquestSectionGlow;
float _ConquestCutHeight;

#define CONQUEST_SECTION_LAYER (1u << 8)
#define CONQUEST_CUT_LAYER (1u << 9)
#define CONQUEST_DIM_LAYER (1u << 10)

bool ToonOutsideSection(float3 positionWS)
{
    if (_ConquestSection < 0.5)
        return false;
    uint layer = asuint(unity_RenderingLayer.x);
    if ((layer & CONQUEST_DIM_LAYER) != 0u)
        return true;
    if ((layer & CONQUEST_SECTION_LAYER) == 0u || _ConquestSectionHasMap < 0.5)
        return false;
    float2 uv = mul(_ConquestSectionWorldToMap, float4(positionWS, 1.0)).xy;
    float id = SAMPLE_TEXTURE2D_LOD(_ConquestSectionMap, sampler_ConquestSectionMap, saturate(uv), 0).r * 255.0;
    bool inside = all(uv >= 0.0) && all(uv <= 1.0) && abs(id - _ConquestSection) < 0.5;
    return !inside;
}

// Cut-away walls: every pass but the shadow caster, so they still cast their full shadow.
void ToonSectionClip(float3 positionWS)
{
    if (_ConquestSection > 0.5 && (asuint(unity_RenderingLayer.x) & CONQUEST_CUT_LAYER) != 0u)
        clip(_ConquestCutHeight - positionWS.y);
}

half3 ToonSectionShade(half3 color, half3 emission, float3 positionWS)
{
    if (!ToonOutsideSection(positionWS))
        return color;
    return (color - emission) * _ConquestSectionTint.rgb + emission * _ConquestSectionGlow;
}

#endif
