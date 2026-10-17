// OpenFusion/FusionMatter — port de SkinnedFusionMatterLightDir_blendSrcalphaInvsrcalpha (ARB decodificado). Dos pasadas.
//  BASE (Cull Back, ZWrite On, Blend SrcAlpha OneMinusSrcAlpha), t=_Time.y
//   vértice (espacio ojo): nE = normalize(N); L = luz0 ; d = (max(nE.L,0)-1)*_ShadowStrength+1 ; col.rgb = d*_Color.rgb + 0.3 ; view = normalize(-eyePos)
//   fragmento: A = tex(uv+(0,0.25t)).rg ; uvB = uv+(0,-0.35t)+A*0.65 ; Cg = tex(uv+(0,-0.15t)).g ; Bb = tex(uvB).b
//     fusion = A.r*_FusionTint + Cg*_BubblesTint*0.5 ; n2 = normalize(IT_MV * nE) ; fusion += sat(2*n2.y - 0.6)*(0.6,0.45,0.24)
//     elec = Bb*_ElectricityTint + (1-sat(nE.view))^2 * _RimColor.a * _RimColor.rgb
//     rgb = fusion*col*1.25 + elec ; a = _Color.a
//  OUTLINE (Cull Front): pos + normal*_Outline, color _OutlineColor.
// IT_MV se aproxima con la rotación (V*M) (válido con escala uniforme). SIN COMPILAR EN UNITY.
Shader "OpenFusion/FusionMatter"
{
    Properties
    {
        _Color ("Main Color", Color) = (0.6,0.6,0.6,1)
        _MainTex ("Combined (R fusion, G bubbles, B electricity)", 2D) = "black" {}
        _FusionTint ("Fusion Tint", Color) = (0,1,0,1)
        _BubblesTint ("Bubbles Tint", Color) = (1,0.976,0.208,1)
        _ElectricityTint ("Electricity Tint", Color) = (0.529,0.576,1,1)
        _RimColor ("Rim Color", Color) = (1,0.976,0.208,1)
        _ShadowStrength ("Shadow Strength", Float) = 0.65
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _Outline ("Outline width", Float) = 0.005
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-100" }
        Pass
        {
            Name "OF_FUSION_BASE"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Back ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST; float4 _Color; float4 _FusionTint; float4 _BubblesTint; float4 _ElectricityTint; float4 _RimColor;
                float _ShadowStrength; float4 _OutlineColor; float _Outline;
            CBUFFER_END
            float4 _OF_Light0Dir;
            struct Attr { float4 pos : POSITION; float3 nrm : NORMAL; float2 uv : TEXCOORD0; };
            struct Vary { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 view : TEXCOORD1; float3 nE : TEXCOORD2; float3 itn : TEXCOORD3; half3 col : COLOR0; };
            Vary vert(Attr v)
            {
                Vary o; o.pos = TransformObjectToHClip(v.pos.xyz); o.uv = v.uv;   // el original no aplica _MainTex_ST
                float3x3 V = (float3x3)UNITY_MATRIX_V;
                float3 nE = normalize(mul(V, TransformObjectToWorldNormal(v.nrm)));
                float3 L = normalize(mul(V, _OF_Light0Dir.xyz));
                float d = (max(dot(nE, L), 0) - 1) * _ShadowStrength + 1;
                o.col = d * _Color.rgb + 0.3;
                float3 eyePos = TransformWorldToView(TransformObjectToWorld(v.pos.xyz));
                o.view = normalize(-eyePos); o.nE = nE;
                o.itn = normalize(mul(mul(V, (float3x3)GetObjectToWorldMatrix()), nE));   // IT_MV * nE (aprox.)
                return o;
            }
            half4 frag(Vary i) : SV_Target
            {
                float t = _Time.y; float2 uv = i.uv;
                float2 A = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(0, 0.25 * t)).rg;
                float Cg = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(0, -0.15 * t)).g;
                float Bb = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(0, -0.35 * t) + A * 0.65).b;
                float3 fusion = A.r * _FusionTint.rgb + Cg * _BubblesTint.rgb * 0.5;
                fusion += saturate(i.itn.y * 2 - 0.6) * float3(0.6, 0.45, 0.24);
                float nv = saturate(dot(normalize(i.nE), i.view)); float rim = (1 - nv) * (1 - nv);
                float3 elec = Bb * _ElectricityTint.rgb + rim * _RimColor.a * _RimColor.rgb;
                return half4(fusion * i.col * 1.25 + elec, _Color.a);
            }
            ENDHLSL
        }
        Pass
        {
            Name "OF_FUSION_OUTLINE"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Front ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST; float4 _Color; float4 _FusionTint; float4 _BubblesTint; float4 _ElectricityTint; float4 _RimColor;
                float _ShadowStrength; float4 _OutlineColor; float _Outline;
            CBUFFER_END
            struct Attr { float4 pos : POSITION; float3 nrm : NORMAL; };
            struct Vary { float4 pos : SV_POSITION; };
            Vary vert(Attr v) { Vary o; o.pos = TransformObjectToHClip(v.pos.xyz + v.nrm * _Outline); return o; }
            half4 frag(Vary i) : SV_Target { return _OutlineColor; }
            ENDHLSL
        }
    }
}
