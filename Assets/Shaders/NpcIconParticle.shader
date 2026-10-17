// OpenFusion/NpcIconParticle (URP, HLSL) — port del shader original "particle_blendOneOne_zwriteOff_cullOff": Blend One One (aditivo), ZWrite Off, Cull Off, AlphaTest > 0.01,
// cola Transparent+3, ZTest LEqual. rgb = tex.rgb * color.rgb (el tinte 0.5 doblado es neutro). ES 669 usa SrcAlpha OneMinusSrcColor (se fija por material).
Shader "OpenFusion/NpcIconParticle"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        [HideInInspector] _SrcBlend ("Src", Float) = 1
        [HideInInspector] _DstBlend ("Dst", Float) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+3" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Name "OF_ICON"
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            ZTest [_ZTest]
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
            CBUFFER_END
            struct Attr { float4 pos : POSITION; float4 col : COLOR; float2 uv : TEXCOORD0; };
            struct Vary { float4 pos : SV_POSITION; float4 col : COLOR; float2 uv : TEXCOORD0; };
            Vary vert(Attr v) { Vary o; o.pos = TransformObjectToHClip(v.pos.xyz); o.col = v.col; o.uv = v.uv * _MainTex_ST.xy + _MainTex_ST.zw; return o; }
            half4 frag(Vary i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.col;
                clip(c.a - 0.01);
                return c;
            }
            ENDHLSL
        }
    }
}
