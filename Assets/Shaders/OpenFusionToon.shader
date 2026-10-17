// OpenFusion/Toon — SkinnedToonShading_*, ToonShading_*, Skin_DirLight_AmbLight_* (2 605 materiales, 26 % del juego).
// Reconstruido a partir de los vertex programs ARB (asm) y de los SetTexture del ShaderLab original.
// SIN COMPILAR EN UNITY. No cubre aún las variantes Rim/RimMatcap (Custom/*) -> se ignoran _RimColor/_RimPower.
//
// Pasada BASE (vertex): P' = P + N*_FatFactor ; u = clamp( N.L + saturate((1 - N.V) * dot(V, -L)), 0.01, 0.99 )
//   (N, V, L en espacio de vista en el original; aquí en mundo: equivalente) ; uv = uv*ST
// Pasada BASE (fragment): c = tex * _Color * 2 ; c *= ToonRamp(u, 1)  (alpha igual, con Blend SrcAlpha OneMinusSrcAlpha)
// Pasada OUTLINE: Cull Front, P' = P + N*(_Outline + _FatFactor), color = _OutlineColor
// Skin_DirLight (_RAMP off): color vértice = saturate(N.L)*_Color + _AmbColor + _Emission ; frag = tex * color * 2, alpha = tex.a
Shader "OpenFusion/Toon"
{
    Properties
    {
        _MainTex ("Base (RGB)", 2D) = "white" {}
        _ShaderMap ("ToonRamp (RGB)", 2D) = "white" {}
        _Color ("Main Color", Color) = (1,1,1,1)
        _AmbColor ("Ambient Color", Color) = (1,1,1,1)
        _Emission ("Emissive Color", Color) = (0,0,0,0)
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _Outline ("Outline width", Range(0, 0.03)) = 0.005
        _FatFactor ("Fat Factor", Range(-0.01, 0.01)) = 0
        _Cutoff ("Alpha cutoff", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        [Toggle(_OF_RAMP)] _Ramp ("Toon ramp (si no: Skin_DirLight lambert)", Float) = 1
        [Toggle(_OF_OUTLINE)] _OutlineOn ("Outline pass", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
        TEXTURE2D(_ShaderMap); SAMPLER(sampler_ShaderMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST; float4 _Color; float4 _AmbColor; float4 _Emission; float4 _OutlineColor;
            float _Outline; float _FatFactor; float _Cutoff;
        CBUFFER_END
        float4 _OF_Light0Dir;   // xyz = hacia la luz (mundo)
        float _OF_ToonOff;      // opcion TOON OUTLINE (GameOptions): 1 = sin contorno
        struct Attr { float4 pos : POSITION; float3 nrm : NORMAL; float2 uv : TEXCOORD0; };
        ENDHLSL

        Pass
        {
            Name "BASE"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _OF_RAMP
            #pragma multi_compile_fog
            struct Vary { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float ramp : TEXCOORD1; half4 col : COLOR0; half fog : TEXCOORD2; };
            Vary vert(Attr v)
            {
                Vary o;
                float3 p = v.pos.xyz + v.nrm * _FatFactor;
                o.pos = TransformObjectToHClip(p); o.uv = TRANSFORM_TEX(v.uv, _MainTex); o.fog = ComputeFogFactor(o.pos.z);
                float3 n = normalize(TransformObjectToWorldNormal(v.nrm));
                float3 L = normalize(_OF_Light0Dir.xyz);
            #if defined(_OF_RAMP)
                float3 V = normalize(GetCameraPositionWS() - TransformObjectToWorld(p));
                float t = (1.0 - dot(n, V)) * dot(V, -L) + dot(n, L);
                o.ramp = clamp(dot(n, L) + saturate(t), 0.01, 0.99);
                o.col = half4(1,1,1,1);
            #else
                o.ramp = 0;
                o.col = saturate(saturate(dot(n, L)) * _Color + _AmbColor + _Emission);
            #endif
                return o;
            }
            half4 frag(Vary i) : SV_Target
            {
                half4 t = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
            #if defined(_OF_RAMP)
                half4 c = saturate(t * _Color * 2);
                half4 r = SAMPLE_TEXTURE2D(_ShaderMap, sampler_ShaderMap, float2(i.ramp, 1));
                c *= r;
            #else
                half4 c; c.rgb = saturate(t.rgb * i.col.rgb * 2); c.a = t.a;
            #endif
                if (_Cutoff > 0) clip(c.a - _Cutoff);
                c.rgb = MixFog(c.rgb, i.fog);
                return c;
            }
            ENDHLSL
        }
        Pass
        {
            Name "OUTLINE"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Front
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _OF_OUTLINE
            struct Vary { float4 pos : SV_POSITION; };
            Vary vert(Attr v)
            {
                Vary o;
            #if defined(_OF_OUTLINE)
                o.pos = TransformObjectToHClip(v.pos.xyz + v.nrm * (_Outline + _FatFactor));
                if (_OF_ToonOff > 0.5) o.pos = float4(2, 2, 2, 1);
            #else
                o.pos = float4(2, 2, 2, 1);   // fuera del frustum: sin outline
            #endif
                return o;
            }
            half4 frag(Vary i) : SV_Target { return _OutlineColor; }
            ENDHLSL
        }
    }
}
