// OpenFusion/TerrainLightmap — el terreno del cliente original se multiplica por un lightmap horneado (sombras de árboles/edificios). Esta malla (mismo relieve, +3 cm) se dibuja con blend multiplicativo DstColor*Src.
Shader "OpenFusion/TerrainLightmap"
{
    Properties { _MainTex ("Lightmap", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest+10" "RenderType"="Opaque" "IgnoreProjector"="True" }
        Pass
        {
            Name "OF_LIGHTMAP"
            Blend DstColor Zero
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
            CBUFFER_END
            struct Attr { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct Vary { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; half fog : TEXCOORD1; };
            Vary vert(Attr v)
            {
                Vary o; float3 pw = TransformObjectToWorld(v.pos.xyz);
                // sesgo hacia la cámara a lo largo del rayo de visión (no mueve el píxel en pantalla): el LOD del terreno se aleja de esta malla con la distancia
                float3 tc = _WorldSpaceCameraPos - pw; float dist = max(length(tc), 0.001); pw += tc / dist * (0.12 + 0.008 * dist);
                o.pos = TransformWorldToHClip(pw); o.uv = v.uv; o.fog = ComputeFogFactor(o.pos.z); return o;
            }
            half4 frag(Vary i) : SV_Target { half3 lm = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).rgb; return half4(lerp(half3(1, 1, 1), lm, ComputeFogIntensity(i.fog)), 1); }   // con niebla el multiplicador se desvanece hacia 1 (el terreno ya viene mezclado con el color de niebla)
            ENDHLSL
        }
    }
}
