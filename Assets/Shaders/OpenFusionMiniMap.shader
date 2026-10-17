// OpenFusion/MiniMap — port de MiniMapRender (ShaderLab fijo): rgb = _MainTex.rgb ; a = _DecalTex.a (etapa 2 "combine previous, texture").
// Blend SrcAlpha OneMinusSrcAlpha, ZWrite Off, ZTest Always, Cull Off, sin luz. SIN COMPILAR EN UNITY.
Shader "OpenFusion/MiniMap"
{
    Properties { _MainTex ("Base (RGB)", 2D) = "white" {} _DecalTex ("Decal (RGBA)", 2D) = "black" {} _MainRect ("Main UV rect (u0,v0,du,dv)", Vector) = (0,0,1,1) _DecalRect ("Decal UV rect (x,y,w,h)", Vector) = (0,0,1,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Name "OF_MINIMAP"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_DecalTex); SAMPLER(sampler_DecalTex);
            CBUFFER_START(UnityPerMaterial) float4 _MainTex_ST; float4 _DecalTex_ST; float4 _MainRect; float4 _DecalRect; CBUFFER_END
            struct Attr { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct Vary { float4 pos : SV_POSITION; float2 uv0 : TEXCOORD0; float2 uv1 : TEXCOORD1; };
            Vary vert(Attr v) { Vary o; o.pos = TransformObjectToHClip(v.pos.xyz); // Graphics.DrawTexture restablece _MainTex_ST (se veia la tesela entera): el UV llega ya recortado (sourceRect = _MainRect) y el decal se deriva de el.
                o.uv0 = v.uv; float2 t = (v.uv - _MainRect.xy) / max(_MainRect.zw, 1e-6); o.uv1 = _DecalRect.xy + t * _DecalRect.zw; return o; }
            half4 frag(Vary i) : SV_Target { return half4(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv0).rgb, SAMPLE_TEXTURE2D(_DecalTex, sampler_DecalTex, i.uv1).a); }
            ENDHLSL
        }
    }
}
