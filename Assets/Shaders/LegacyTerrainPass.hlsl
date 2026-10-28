// Reuses URP 14 terrain geometry/instancing and splat evaluation only.
// Lighting equation: Unity 2.5 player Hidden/TerrainEngine/Splatmap/Lightmap-FirstPass
// and Lightmap-BaseMap: blended diffuse * baked lightmap * 2, no PBR relighting.
#include "Packages/com.unity.render-pipelines.universal/Shaders/Terrain/TerrainLitInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Shaders/Terrain/TerrainLitPasses.hlsl"
TEXTURE2D(_LegacyLightmap); SAMPLER(sampler_LegacyLightmap);
half4 LegacyTerrainFragment(Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
#ifdef _ALPHATEST_ON
    ClipHoles(input.uvMainAndLM.xy);
#endif
    half weight = 1;
#ifdef TERRAIN_SPLAT_BASEPASS
    half3 diffuse = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uvMainAndLM.xy).rgb;
#else
    float2 controlUV = (input.uvMainAndLM.xy * (_Control_TexelSize.zw - 1) + .5) * _Control_TexelSize.xy;
    half4 control = SAMPLE_TEXTURE2D(_Control, sampler_Control, controlUV);
    half4 diffuseMix, smoothness; half3 unusedNormal = half3(0,0,1);
    SplatmapMix(input.uvMainAndLM, input.uvSplat01, input.uvSplat23, control, weight, diffuseMix, smoothness, unusedNormal);
    half3 diffuse = diffuseMix.rgb;
#endif
    half3 baked = SAMPLE_TEXTURE2D(_LegacyLightmap, sampler_LegacyLightmap, input.uvMainAndLM.xy).rgb;
    half3 color = saturate(diffuse * baked * 2 * weight);
    half fog = InitializeInputDataFog(float4(input.positionWS, 1), input.fogFactor);
#ifdef TERRAIN_SPLAT_ADDPASS
    color = MixFogColor(color, half3(0,0,0), fog);
#else
    color = MixFog(color, fog);
#endif
    return half4(color,1);
}
