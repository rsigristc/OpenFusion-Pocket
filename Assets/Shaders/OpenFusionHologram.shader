// OpenFusion/Hologram — port de Custom/VFX/Unique/HologramSolid_Additive (dos pasadas, ARB decodificado). _MaskTex empaquetada (RGBA).
//  Pasada 1 (Blend SrcAlpha OneMinusSrcAlpha): m = mask.a ; rgb = lerp(_ColorB, _ColorA, m) ; a = _MasterOpacity*_BaseOpacity*vc.a
//  Pasada 2 (Blend SrcAlpha One), t=_Time.y: m = mask.a ; k = lerp(white, _ColorA, m) (rgba)
//    sR = mask(uv*(1.25,1)+(0.2t,0)).r ; sG = mask(uv+(0.35,0.7)t).g ; sB = mask(uv*(0.65,1)+(0.1t,0)).b
//    c = k*_OverlayRColor*sR*_OverlayROpacity + k*_OverlayGColor*sG*_OverlayGOpacity + k*_OverlayBColor*sB*_OverlayBOpacity
//    a = c.a * ((m-1)*_MaskAlphaOpacity+1)*_MasterOpacity*vc.a ; rgb = c.rgb
// Cull Off, Transparent-100. SIN COMPILAR EN UNITY.
Shader "OpenFusion/Hologram"
{
    Properties
    {
        [HideInInspector] [MainTexture] _MainTex ("Unused (evita Material.mainTexture sin propiedad)", 2D) = "white" {}
        _MaskTex ("Packed Mask (RGB+A)", 2D) = "white" {}
        _ColorA ("Color A (Alpha)", Color) = (1,0.859,0.067,1)
        _ColorB ("Color B (Inverted Alpha)", Color) = (0.871,0.588,0.063,1)
        _MasterOpacity ("Master Opacity", Float) = 1
        _BaseOpacity ("Base Opacity", Float) = 0.85
        _MaskAlphaOpacity ("Mask Alpha Opacity", Float) = 0.65
        _OverlayRColor ("Overlay R Color", Color) = (1,0.235,0,1)
        _OverlayROpacity ("Overlay R Opacity", Float) = 1
        _OverlayGColor ("Overlay G Color", Color) = (0.647,0.529,0.529,1)
        _OverlayGOpacity ("Overlay G Opacity", Float) = 0.25
        _OverlayBColor ("Overlay B Color", Color) = (0.569,0.353,0.055,1)
        _OverlayBOpacity ("Overlay B Opacity", Float) = 0.6
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-100" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_MaskTex); SAMPLER(sampler_MaskTex);
        CBUFFER_START(UnityPerMaterial)
            float4 _MaskTex_ST; float4 _ColorA; float4 _ColorB; float _MasterOpacity, _BaseOpacity, _MaskAlphaOpacity;
            float4 _OverlayRColor; float _OverlayROpacity; float4 _OverlayGColor; float _OverlayGOpacity; float4 _OverlayBColor; float _OverlayBOpacity;
        CBUFFER_END
        struct Attr { float4 pos : POSITION; float2 uv : TEXCOORD0; float4 col : COLOR; };
        struct Vary { float4 pos : SV_POSITION; half4 col : COLOR0; float2 uv : TEXCOORD0; };
        Vary vert(Attr v) { Vary o; o.pos = TransformObjectToHClip(v.pos.xyz); o.col = v.col; o.uv = v.uv * _MaskTex_ST.xy + _MaskTex_ST.zw; return o; }
        ENDHLSL
        Pass
        {
            Name "OF_HOLO_BASE"
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off ColorMask RGBA
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag(Vary i) : SV_Target
            {
                half m = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, i.uv).a;
                return half4(lerp(_ColorB.rgb, _ColorA.rgb, m), _MasterOpacity * _BaseOpacity * i.col.a);
            }
            ENDHLSL
        }
        Pass
        {
            Name "OF_HOLO_OVERLAY"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha One
            Cull Off ColorMask RGBA
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag(Vary i) : SV_Target
            {
                float t = _Time.y; float2 uv = i.uv;
                half sG = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, uv + float2(0.35, 0.7) * t).g;
                half sR = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, float2(uv.x * 1.25 + 0.2 * t, uv.y)).r;
                half m  = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, uv).a;
                half sB = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, float2(uv.x * 0.65 + 0.1 * t, uv.y)).b;
                half4 k = lerp(half4(1,1,1,1), _ColorA, m);
                half4 c = k * _OverlayRColor * sR * _OverlayROpacity + k * _OverlayGColor * sG * _OverlayGOpacity + k * _OverlayBColor * sB * _OverlayBOpacity;
                float a = c.a * ((m - 1) * _MaskAlphaOpacity + 1) * _MasterOpacity * i.col.a;
                return half4(c.rgb, a);
            }
            ENDHLSL
        }
    }
}
