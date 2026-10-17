// "Estrella de fusión" (planeta) del cielo del cliente 2.5: plano texturizado hijo de la cámara, mezcla alfa, sin profundidad, dibujado justo después del skybox.
Shader "OpenFusion/SkyStar"
{
    Properties { _MainTex ("Textura", 2D) = "white" {} _Brightness ("Brillo", Float) = 1 }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Background+1" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "SkyStar"
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off ZWrite Off ZTest Always
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST; half _Brightness;
            CBUFFER_END
            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            V vert(A a) { V o; o.pos = TransformObjectToHClip(a.pos.xyz); o.uv = a.uv; return o; }
            half4 frag(V i) : SV_Target { half4 t = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv); return half4(saturate(t.rgb * _Brightness), t.a); }
            ENDHLSL
        }
    }
}
