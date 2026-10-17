// OpenFusion/Grass — hierba del terreno (DetailPrototype GrassBillboard del original) dibujada con mallas propias:
// el sistema de detalles del Terrain no se pinta de forma fiable bajo URP. Cada brizna = quad con esquina (uv2.x = +-ancho/2, uv2.y = alto),
// orientado al eje horizontal de la cámara (billboard vertical). Color = textura * mezcla sano/seco (color de vértice).
// Opciones: _OF_GrassDist (m; 0 = sin hierba, la brizna se encoge y desaparece en los últimos 15 m), _OF_SoftVeg (1 = bordes suaves, 0 = recorte duro a 0,5).
// SIN COMPILAR EN UNITY.
Shader "OpenFusion/Grass"
{
    Properties { _MainTex ("Grass", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout" "IgnoreProjector"="True" }
        Pass
        {
            Name "GRASS"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite On
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
            CBUFFER_END
            float _OF_GrassDist; float _OF_SoftVeg;
            struct Attr { float4 pos : POSITION; float2 uv : TEXCOORD0; float2 corner : TEXCOORD1; float4 col : COLOR; };
            struct Vary { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; half4 col : COLOR0; half fog : TEXCOORD1; };
            Vary vert(Attr v)
            {
                Vary o;
                float3 basePos = TransformObjectToWorld(v.pos.xyz);
                float d = distance(basePos, GetCameraPositionWS());
                float fade = saturate((_OF_GrassDist - d) / 15.0);
                float3 right = float3(UNITY_MATRIX_V[0].x, 0, UNITY_MATRIX_V[0].z); right = normalize(right + float3(1e-5, 0, 0));
                float sway = sin(_Time.y * 1.6 + basePos.x * 0.45 + basePos.z * 0.37) * 0.06 * v.uv.y;
                float3 wp = basePos + right * (v.corner.x * fade + sway * v.corner.y) + float3(0, v.corner.y * fade, 0);
                o.pos = TransformWorldToHClip(wp);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex); o.col = half4(v.col.rgb, fade); o.fog = ComputeFogFactor(o.pos.z);
                if (_OF_GrassDist < 1) o.pos = float4(2, 2, 2, 1);
                return o;
            }
            half4 frag(Vary i) : SV_Target
            {
                half4 t = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half a = t.a;
                if (_OF_SoftVeg > 0.5) { clip(a - 0.2); a = saturate((a - 0.2) / 0.4); } else { clip(a - 0.5); a = 1; }
                half3 c = t.rgb * i.col.rgb; c = MixFog(c, i.fog);
                return half4(c, a);
            }
            ENDHLSL
        }
    }
}
