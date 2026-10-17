// OpenFusion/Scroll — port de retro_diffuseFade y Skin_FusionEffect_*_tutorial (mismo programa, decodificado del ARB):
//   color por vértice: rgb = max(N.L,0)*_Color.rgb + _Emission.rgb ; a = saturate(_Color.a*(1+max(N.L,0)))   (sin ambiente)
//   st = _Speed*_Time.y ; uv0 = (u, v+st) ; uv1 = (u, v-st) ; uv2 = (st + clip.x, clip.y) con clip = MVP*(normal,1)
//   etapas: c = tex0(uv0)*primary*2 ; rgb += tex1(uv1).rgb + tex2(uv2).rgb ; a = tex0.a*primary.a*2
//   (_MainTex, _BumpMap, _ShaderMap). Blend SrcAlpha OneMinusSrcAlpha, ColorMask RGB, Transparent-100. SIN COMPILAR EN UNITY.
Shader "OpenFusion/Scroll"
{
    Properties
    {
        _Color ("Main Color", Color) = (1,1,1,1)
        _Emission ("Emission", Color) = (0,0,0,0)
        _MainTex ("Tex0", 2D) = "black" {}
        _BumpMap ("Tex1", 2D) = "black" {}
        _ShaderMap ("Tex2", 2D) = "black" {}
        _Speed ("Speed", Range (-10,10)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-100" }
        Pass
        {
            Name "OF_SCROLL"
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask RGB
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            TEXTURE2D(_ShaderMap); SAMPLER(sampler_ShaderMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST; float4 _Color; float4 _Emission; float _Speed;
            CBUFFER_END
            float4 _OF_Light0Dir;
            struct Attr { float4 pos : POSITION; float3 nrm : NORMAL; float2 uv : TEXCOORD0; };
            struct Vary { float4 pos : SV_POSITION; half4 col : COLOR0; float2 uv0 : TEXCOORD0; float2 uv1 : TEXCOORD1; float2 uv2 : TEXCOORD2; };
            Vary vert(Attr v)
            {
                Vary o; o.pos = TransformObjectToHClip(v.pos.xyz);
                float2 uv = TRANSFORM_TEX(v.uv, _MainTex); float st = _Speed * _Time.y;
                float ndl = max(dot(TransformObjectToWorldNormal(v.nrm), _OF_Light0Dir.xyz), 0);
                o.col = saturate(half4(ndl * _Color.rgb + _Emission.rgb, ndl * _Color.a + _Color.a));
                o.uv0 = float2(uv.x, uv.y + st); o.uv1 = float2(uv.x, uv.y - st);
                float4 clipN = TransformObjectToHClip(v.nrm);   // MVP * (normal, 1), tal cual el original
                o.uv2 = float2(st + clipN.x, clipN.y);
                return o;
            }
            half4 frag(Vary i) : SV_Target
            {
                half4 t0 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv0);
                half3 rgb = t0.rgb * i.col.rgb * 2 + SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv1).rgb + SAMPLE_TEXTURE2D(_ShaderMap, sampler_ShaderMap, i.uv2).rgb;
                return half4(saturate(rgb), saturate(t0.a * i.col.a * 2));
            }
            ENDHLSL
        }
    }
}
