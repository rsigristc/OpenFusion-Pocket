// Cara de skybox del cliente 2.5 (RenderFX/Skybox): color = textura * _Tint * 2, sin niebla, dibujada al fondo sin profundidad.
Shader "OpenFusion/SkyFace"
{
    Properties { _MainTex ("Textura", 2D) = "white" {} _Tint ("Tint", Color) = (0.5, 0.5, 0.5, 1) }
    SubShader
    {
        Tags { "RenderType" = "Background" "Queue" = "Background" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "SkyFace"
            Cull Off ZWrite Off ZTest Always
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST; half4 _Tint;
            CBUFFER_END
            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            V vert(A a) { V o; o.pos = TransformObjectToHClip(a.pos.xyz); o.uv = a.uv; return o; }
            half4 frag(V i) : SV_Target
            {
                half4 t = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                return half4(saturate(t.rgb * _Tint.rgb * 2.0h), 1.0h);
            }
            ENDHLSL
        }
    }
}
