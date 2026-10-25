Shader "OpenFusion/IntroHologram"
{
    Properties { _MainTex ("Actor camera", 2D) = "white" {} _Opacity ("Opacity", Range(0,1)) = .8 }
    SubShader {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Overlay" }
        Pass {
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off ZWrite Off ZTest Always
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex); float _Opacity;
            struct Attr { float4 position:POSITION; float2 uv:TEXCOORD0; };
            struct Vary { float4 position:SV_POSITION; float2 uv:TEXCOORD0; };
            Vary vert(Attr i) { Vary o; o.position=TransformObjectToHClip(i.position.xyz);o.uv=i.uv;return o; }
            half4 frag(Vary i):SV_Target {
                half4 c=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv);
                half light=dot(c.rgb,half3(.3,.59,.11));
                half scan=.7+.3*step(.4,frac(i.uv.y*240+_Time.y*4));
                return half4(half3(.03,.25,1)*(light+.35)*scan,c.a*_Opacity);
            }
            ENDHLSL
        }
    }
}
