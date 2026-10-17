// OpenFusion/RtBackdrop — fondo opaco sin iluminacion para los RenderTexture de modelos 3D de la interfaz (NanoModelRt): quad a distancia fija de la camara con la region del fondo del panel.
// Escribe color y alfa de la textura (el alfa del fondo se conserva) y profundidad (ZWrite On) para que el modelo se dibuje encima y lo aditivo se sume al fondo, como la camara DepthOnly del original. SIN COMPILAR EN UNITY.
Shader "OpenFusion/RtBackdrop"
{
    Properties
    {
        [MainTexture] _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "OF_RTBACKDROP"
            Blend Off
            ZWrite On
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
            CBUFFER_END
            struct Attr { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct Vary { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            Vary vert(Attr v) { Vary o; o.pos = TransformObjectToHClip(v.pos.xyz); o.uv = v.uv * _MainTex_ST.xy + _MainTex_ST.zw; return o; }
            half4 frag(Vary i) : SV_Target { return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv); }
            ENDHLSL
        }
    }
}
