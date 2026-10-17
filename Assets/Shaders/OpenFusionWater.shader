// OpenFusion/Water — port del shader "ffWater" (subshader de tarjetas con fragment program, ARB decodificado):
//   vertex:  R = (WaveSpeed*t + pos.xzxz) * _WaveScale ; uv0 = R.xy*(0.4,0.45) ; uv1 = R.wz ; view = (camObj - pos).xzy ; ny = normal.y
//   fragment: b = tex(_BumpMap,uv0).rgb + tex(_BumpMap,uv1).rgb - 1 ; d = dot(normalize(view), b)
//             r = tex(_ReflectiveColor,(d,d)) ; rgb = lerp(r.rgb,_HorizonColor.rgb,r.a) ; rgb = lerp(foam, rgb, ny) ; a = r.a*_HorizonColor.a*2
// Sin reflexión/refracción (el WaterMode "Reflective" del motor no existe en URP). Blend SrcAlpha OneMinusSrcAlpha, Transparent+2.
// Las texturas del agua son datos (normal-ish y rampa de fresnel): importar SIN sRGB. SIN COMPILAR EN UNITY.
Shader "OpenFusion/Water"
{
    Properties
    {
        _WaveScale ("Wave scale", Range (0.001,0.15)) = 0.063
        _RefrColor ("Refraction color", Color) = (.34, .85, .92, 1)
        _BumpMap ("Bumpmap (RGB)", 2D) = "bump" {}
        WaveSpeed ("Wave speed (map1 x,y; map2 x,y)", Vector) = (19,9,-16,-7)
        _ReflectiveColor ("Reflective color (RGB) fresnel (A)", 2D) = "white" {}
        _HorizonColor ("Simple water horizon color", Color) = (.172, .463, .435, 1)
        _FoamColor ("Foam color", Color) = (1,1,1,1)
        _MinOpacity ("Min opacity (original max-quality water is nearly opaque)", Range(0,1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+2" }
        Pass
        {
            Name "OF_WATER"
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask RGB
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            TEXTURE2D(_ReflectiveColor); SAMPLER(sampler_ReflectiveColor);
            CBUFFER_START(UnityPerMaterial)
                float _WaveScale; float _MinOpacity; float4 WaveSpeed; float4 _HorizonColor; float4 _FoamColor; float4 _RefrColor;
            CBUFFER_END
            struct Attr { float4 pos : POSITION; float3 nrm : NORMAL; };
            struct Vary { float4 pos : SV_POSITION; float2 uv0 : TEXCOORD0; float2 uv1 : TEXCOORD1; float3 view : TEXCOORD2; float ny : TEXCOORD3; half fog : TEXCOORD4; };
            Vary vert(Attr v)
            {
                Vary o; o.pos = TransformObjectToHClip(v.pos.xyz); o.fog = ComputeFogFactor(o.pos.z);
                float4 r = (WaveSpeed * _Time.x + v.pos.xzxz) * _WaveScale;
                o.uv0 = r.xy * float2(0.4, 0.45); o.uv1 = r.wz;
                float3 camObj = TransformWorldToObject(_WorldSpaceCameraPos);
                o.view = (camObj - v.pos.xyz).xzy; o.ny = v.nrm.y;
                return o;
            }
            half4 frag(Vary i) : SV_Target
            {
                half3 b = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv0).rgb + SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv1).rgb - 1;
                float d = dot(normalize(i.view), b);
                half4 r = SAMPLE_TEXTURE2D(_ReflectiveColor, sampler_ReflectiveColor, float2(d, d));
                half3 rgb = lerp(r.rgb, _HorizonColor.rgb, r.a);
                rgb = lerp(_FoamColor.rgb, rgb, i.ny);
                // El cliente original (gráficos al máximo) usa el agua con refracción/reflexión, casi opaca: aquí se aproxima con una opacidad alta.
                rgb = MixFog(rgb, i.fog);
                return half4(rgb, saturate(r.a * _HorizonColor.a * 2));   // alfa original: ~0.46 mirando de arriba, ~1 en ángulos rasantes
            }
            ENDHLSL
        }
    }
}
