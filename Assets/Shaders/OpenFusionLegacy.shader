// OpenFusion/Legacy — emula el pipeline fijo de Unity 2.5 para las familias normal_*, particle_*, normal_glow_*,
// fusionStar y MiniMapRender (6 600 + 500 + 32 materiales de Retrobution). URP (Unity 6 LTS), HLSL.
// SIN COMPILAR EN UNITY (no hay editor en el sandbox): validar en el primer build.
//
//  LIT   (Lighting On):   color = tex * primary * 2,  primary = Emission + Ambient*_OF_Ambient + Diffuse*max(N.L,0)*_OF_Light0Color
//                         (iluminación por vértice, como el pipeline fijo). alpha = tex.a (o tex.a*primary.a con _AlphaPrimary)
//  PARTICLE (Lighting Off): color = tex * (_TintColor * vertexColor) * 2 ; alpha = tex.a * tint.a * vc.a
//  GLOW: lit = lerp(primary, 0.5, glowMap.a); color = tex * lit (sin x2) ; alpha = tex.a * primary.a
Shader "OpenFusion/Legacy"
{
    Properties
    {
        _MainTex ("Base (RGB) Alpha", 2D) = "white" {}
        _BumpMap ("Glow (RGBA)", 2D) = "black" {}
        _Color ("Diffuse", Color) = (1,1,1,1)
        _AmbColor ("Ambient", Color) = (1,1,1,1)
        _Emission ("Emission", Color) = (0,0,0,0)
        _TintColor ("Tint", Color) = (0.5,0.5,0.5,0.5)
        _Cutoff ("Alpha cutoff", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst", Float) = 0
        [Enum(Off,0,On,1)] _ZWrite ("ZWrite", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        [Enum(Off,0,Greater,1,GEqual,2)] _AlphaTestMode ("AlphaTest", Float) = 0
        [Toggle(_OF_LIT)] _Lit ("Lit (Lighting On)", Float) = 1
        [Toggle(_OF_PARTICLE)] _Particle ("Particle (tint*vertex color)", Float) = 0
        [Toggle(_OF_VERTEX_AD)] _VertexAD ("ColorMaterial AmbientAndDiffuse", Float) = 0
        [Toggle(_OF_GLOW)] _Glow ("Glow map", Float) = 0
        [Toggle(_OF_ALPHA_PRIMARY)] _AlphaPrimary ("alpha = tex*primary", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" }
        Pass
        {
            Name "OF_LEGACY"
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _OF_LIT
            #pragma shader_feature_local _OF_PARTICLE
            #pragma shader_feature_local _OF_VERTEX_AD
            #pragma shader_feature_local _OF_GLOW
            #pragma shader_feature_local _OF_ALPHA_PRIMARY
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST; float4 _Color; float4 _AmbColor; float4 _Emission; float4 _TintColor;
                float _Cutoff; float _AlphaTestMode; float _DstBlend;
            CBUFFER_END
            // Globales que fija OpenFusionLighting.cs (luz 0 del escenario + RenderSettings.ambientLight)
            float4 _OF_Light0Dir;     // xyz = dirección HACIA la luz (mundo)
            float4 _OF_Light0Color;   // rgb = color*intensidad
            float4 _OF_Ambient;       // rgb = ambiente de escena
            float _OF_GlowOff;        // opcion GLOW (GameOptions): 1 = sin mapa de brillo
            float4 _OF_ObjFade;       // opcion OBJECT FADING: x = activo, y = inicio, z = fin (distancia a la camara, disolucion por ruido)

            struct Attr { float4 pos : POSITION; float3 nrm : NORMAL; float2 uv : TEXCOORD0; float4 col : COLOR; };
            struct Vary { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; half4 col : COLOR0; half fog : TEXCOORD1; float fdist : TEXCOORD2; };

            Vary vert(Attr v)
            {
                Vary o; o.pos = TransformObjectToHClip(v.pos.xyz); o.uv = TRANSFORM_TEX(v.uv, _MainTex); o.fog = ComputeFogFactor(o.pos.z);
            #if !defined(_OF_PARTICLE)
                {   // distancia para el desvanecimiento; los objetos pegados a la camara (cielo, arma en mano) no se desvanecen
                    float3 wp = TransformObjectToWorld(v.pos.xyz); float3 ow = float3(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13, unity_ObjectToWorld._m23);
                    o.fdist = distance(_WorldSpaceCameraPos, ow) < 20.0 ? 0.0 : distance(_WorldSpaceCameraPos, wp);
                }
            #else
                o.fdist = 0.0;
            #endif
            #if defined(_OF_PARTICLE)
                o.col = _TintColor * v.col;
            #elif defined(_OF_LIT)
                float3 n = TransformObjectToWorldNormal(v.nrm);
                float ndl = saturate(dot(n, _OF_Light0Dir.xyz));
                #if defined(_OF_VERTEX_AD)
                    float4 diff = v.col, amb = v.col;
                #else
                    float4 diff = _Color, amb = _AmbColor;
                #endif
                float4 c; c.rgb = _Emission.rgb + amb.rgb * _OF_Ambient.rgb + diff.rgb * _OF_Light0Color.rgb * ndl; c.a = diff.a;
                o.col = saturate(c);
            #else
                o.col = v.col;
            #endif
                return o;
            }

            half4 frag(Vary i) : SV_Target
            {
                half4 t = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half4 c;
            #if defined(_OF_PARTICLE)
                c.rgb = saturate(t.rgb * i.col.rgb * 2); c.a = t.a * i.col.a;
            #elif defined(_OF_GLOW)
                half4 g = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv);
                half3 lit = lerp(i.col.rgb, half3(0.5,0.5,0.5), g.a * (1.0 - _OF_GlowOff));
                c.rgb = t.rgb * lit; c.a = t.a * i.col.a;
            #elif defined(_OF_LIT)
                c.rgb = saturate(t.rgb * i.col.rgb * 2);
                #if defined(_OF_ALPHA_PRIMARY)
                    c.a = t.a * i.col.a;
                #else
                    c.a = t.a;
                #endif
            #else
                c = t * i.col;
            #endif
            #if !defined(_OF_PARTICLE)
                if (_OF_ObjFade.x > 0.5)
                {   // OBJECT FADING: disolucion por ruido entre y..z (ruido de gradiente entrelazado en pixeles de pantalla)
                    float fade = saturate((i.fdist - _OF_ObjFade.y) / max(_OF_ObjFade.z - _OF_ObjFade.y, 1.0));
                    float n = frac(52.9829189 * frac(dot(i.pos.xy, float2(0.06711056, 0.00583715))));
                    clip(n - fade * 0.999);
                }
            #endif
                if (_AlphaTestMode > 0.5) clip((_AlphaTestMode < 1.5 ? c.a - _Cutoff - 1e-4 : c.a - _Cutoff));
                // Niebla del original (RenderSettings.fog): mezcla hacia el color de niebla; con blend aditivo (Dst = One) se mezcla hacia negro, como el pipeline fijo.
                c.rgb = (_DstBlend > 0.5 && _DstBlend < 1.5) ? MixFogColor(c.rgb, half3(0, 0, 0), i.fog) : MixFog(c.rgb, i.fog);
                return c;
            }
            ENDHLSL
        }
    }
}
