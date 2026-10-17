// OpenFusion/StationFx — efectos del holograma de la Nano Station (mob/nanomachine.kfm): anillos (gm_06_eff) que giran, circuitos de la esfera (gm_05_eff) que se desplazan y rayos (lighting_N) que parpadean.
// El clip 'nif-default' del modelo trae curvas de material con valores de relleno (no datos reales), por eso la animacion se hace aqui, en el shader (tiempo). Valores de velocidad/parpadeo ESTIMADOS.
// Aditivo (Blend One One), ZWrite Off, Cull Off, ColorMask RGB (como normal_blendOneOne_zwriteOff_cullOff del original: el alfa del destino no se toca). Sin iluminacion. SIN COMPILAR EN UNITY.
Shader "OpenFusion/StationFx"
{
    Properties
    {
        [MainTexture] _MainTex ("Texture", 2D) = "white" {}
        _Tint ("Tint", Color) = (1,1,1,1)
        _Scroll ("UV scroll per second (xy)", Vector) = (0,0,0,0)
        _Spin ("UV spin deg/s", Float) = 0
        _Flicker ("Lightning flicker (0/1)", Float) = 0
        _Seed ("Seed", Float) = 0
        _Pulse ("Pulse amplitude", Float) = 0
        _PulseSpeed ("Pulse speed", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+11" }
        Pass
        {
            Name "OF_STATIONFX"
            Blend One One
            ZWrite Off
            Cull Off
            ColorMask RGB
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST; float4 _Tint; float4 _Scroll; float _Spin; float _Flicker; float _Seed; float _Pulse; float _PulseSpeed;
            CBUFFER_END
            struct Attr { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct Vary { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            float OFHash(float n) { return frac(sin(n * 12.9898 + 78.233) * 43758.5453); }
            Vary vert(Attr v) { Vary o; o.pos = TransformObjectToHClip(v.pos.xyz); o.uv = v.uv * _MainTex_ST.xy + _MainTex_ST.zw; return o; }
            half4 frag(Vary i) : SV_Target
            {
                float t = _Time.y; float2 uv = i.uv;
                if (abs(_Spin) > 0.001)
                {
                    float a = radians(_Spin) * t; float sn = sin(a); float cs = cos(a); float2 p = uv - 0.5;
                    uv = float2(p.x * cs - p.y * sn, p.x * sn + p.y * cs) + 0.5;
                }
                uv += _Scroll.xy * t;
                half4 tx = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                float k = 1.0;
                if (_Flicker > 0.5)
                {
                    float n = OFHash(floor(t * 14.0 + _Seed * 3.1)); float m = OFHash(floor(t * 4.0 + _Seed * 7.7));
                    k = saturate((n - 0.35) * 2.2) * step(0.3, m) * (0.6 + 0.4 * sin(t * 53.0 + _Seed));
                }
                k *= 1.0 + _Pulse * sin(t * _PulseSpeed + _Seed);
                return half4(tx.rgb * _Tint.rgb * k, 1.0);
            }
            ENDHLSL
        }
    }
}
