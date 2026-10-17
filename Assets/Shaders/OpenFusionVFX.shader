// OpenFusion/VFX — los 11 shaders Custom/VFX/* (con y sin _Additive) en un solo shader con un modo (_VFXMode).
// Cada fórmula sale del ARB decodificado; la mezcla (normal/aditiva) la fija el binder con _SrcBlend/_DstBlend (SrcAlpha/OneMinusSrcAlpha o SrcAlpha/One).
// Estado común: ZWrite Off, Cull Off, ColorMask RGBA, cola Transparent+3 (ScrollDistort: Transparent). SIN COMPILAR EN UNITY.
//  Modo 0 DUAL    (DualScrollingAdditive): c = tex1(uv+S1*t)*Color1 + tex2(uv+S2*t)*Color2 ; a = c.a*vc.a*_MasterOpacity ; rgb = c.rgb*_EmissionStrength
//  Modo 1 POLAR   (PolarScrolling): p=uv-0.5; r=p+Center ; u=frac((atan2(r.y,r.x)*_AngularScale+PI)/2PI + SpeedU*t) ; v=|r|*_RadialScale + SpeedV*t ; col = tex(u,v)*Color*vc*vig
//  Modo 2 FLIPBOOK(RotatingFlipbook): frame=floor(_AnimationSpeed*t) ; rota uv alrededor de Center por _RotationSpeed*t ; celda (frame%Cols, floor(frame/Cols)%Rows) ; col = tex*Color*vc*vig
//  Modo 3 ROTATE  (TextureRotate): rota uv alrededor de Center por _RotationSpeed*t ; col = tex*Color*vc*vig
//  Modo 4 DISTORT (ScrollDistort_wMask_Vignette) y 5 GRADIENT (..._Gradient): ruido -> distorsión, dos texturas (suma o producto según _BlendMode>=0.5), máscara, viñeta o degradado
//  vig = 1 + (saturate(1 - (|uv-0.5|*2)^_VignettePower) - 1)*_VignetteIntensity
Shader "OpenFusion/VFX"
{
    Properties
    {
        [Enum(Dual,0,Polar,1,Flipbook,2,Rotate,3,DistortVignette,4,DistortGradient,5)] _VFXMode ("Mode", Float) = 3
        _MainTex ("Texture 1", 2D) = "white" {}
        _MainTex2 ("Texture 2", 2D) = "white" {}
        _NoiseTex ("Noise", 2D) = "white" {}
        _MaskTex ("Mask", 2D) = "white" {}
        _Color ("Main Color", Color) = (1,1,1,1)
        _Color1 ("Color 1", Color) = (1,1,1,1)
        _Color2 ("Color 2", Color) = (1,1,1,1)
        _ScrollX1 ("Scroll X 1", Float) = 0
        _ScrollY1 ("Scroll Y 1", Float) = 0
        _ScrollX2 ("Scroll X 2", Float) = 0
        _ScrollY2 ("Scroll Y 2", Float) = 0
        _EmissionStrength ("Emission Strength", Float) = 1
        _MasterOpacity ("Master Opacity", Float) = 1
        _ScrollSpeedU ("Scroll Speed U", Float) = 1
        _ScrollSpeedV ("Scroll Speed V", Float) = 1
        _CenterX ("Center X", Float) = 0
        _CenterY ("Center Y", Float) = 0
        _RadialScale ("Radial Scale", Float) = 1
        _AngularScale ("Angular Scale", Float) = 1
        _Columns ("Columns", Float) = 4
        _Rows ("Rows", Float) = 4
        _AnimationSpeed ("Animation Speed", Float) = 10
        _RotationSpeed ("Rotation Speed", Float) = 1
        _VignetteIntensity ("Vignette Intensity", Float) = 0
        _VignettePower ("Vignette Power", Float) = 2
        _BlendMode ("Blend Mode (<0.5 add, >=0.5 multiply)", Float) = 0
        _DistortionStrength ("Distortion Strength", Float) = 0.1
        _NoiseScrollU ("Noise Scroll U", Float) = 0.5
        _NoiseScrollV ("Noise Scroll V", Float) = 0.5
        _NoiseScale ("Noise Scale", Float) = 1
        _MainScrollU ("Main 1 Scroll U", Float) = 0
        _MainScrollV ("Main 1 Scroll V", Float) = 0
        _Main2ScrollU ("Main 2 Scroll U", Float) = 0
        _Main2ScrollV ("Main 2 Scroll V", Float) = 0
        _MaskScrollU ("Mask Scroll U", Float) = 0
        _MaskScrollV ("Mask Scroll V", Float) = 0
        _GradientRotation ("Gradient Rotation", Float) = 0
        _GradientTopIntensity ("Gradient Top Intensity", Float) = 1
        _GradientBottomIntensity ("Gradient Bottom Intensity", Float) = 1
        _GradientTopPower ("Gradient Top Power", Float) = 1
        _GradientBottomPower ("Gradient Bottom Power", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst", Float) = 10
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+3" }
        Pass
        {
            Name "OF_VFX"
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off Cull Off ColorMask RGBA
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_MainTex2); SAMPLER(sampler_MainTex2);
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
            TEXTURE2D(_MaskTex); SAMPLER(sampler_MaskTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST; float4 _MainTex2_ST; float4 _MaskTex_ST;
                float _VFXMode; float4 _Color; float4 _Color1; float4 _Color2;
                float _ScrollX1, _ScrollY1, _ScrollX2, _ScrollY2, _EmissionStrength, _MasterOpacity;
                float _ScrollSpeedU, _ScrollSpeedV, _CenterX, _CenterY, _RadialScale, _AngularScale;
                float _Columns, _Rows, _AnimationSpeed, _RotationSpeed, _VignetteIntensity, _VignettePower;
                float _BlendMode, _DistortionStrength, _NoiseScrollU, _NoiseScrollV, _NoiseScale;
                float _MainScrollU, _MainScrollV, _Main2ScrollU, _Main2ScrollV, _MaskScrollU, _MaskScrollV;
                float _GradientRotation, _GradientTopIntensity, _GradientBottomIntensity, _GradientTopPower, _GradientBottomPower;
            CBUFFER_END
            struct Attr { float4 pos : POSITION; float2 uv : TEXCOORD0; float4 col : COLOR; };
            struct Vary { float4 pos : SV_POSITION; half4 col : COLOR0; float2 uv0 : TEXCOORD0; float2 uv1 : TEXCOORD1; float2 uv2 : TEXCOORD2; float2 uv3 : TEXCOORD3; };
            Vary vert(Attr v)
            {
                Vary o; o.pos = TransformObjectToHClip(v.pos.xyz); o.col = v.col;
                o.uv0 = v.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                o.uv1 = v.uv * _MainTex2_ST.xy + _MainTex2_ST.zw;
                o.uv2 = v.uv * _NoiseScale;
                o.uv3 = v.uv * _MaskTex_ST.xy + _MaskTex_ST.zw;
                return o;
            }
            float Vignette(float2 uv)
            {
                float d = length(uv - 0.5) * 2;
                return 1 + (saturate(1 - pow(d, _VignettePower)) - 1) * _VignetteIntensity;
            }
            float2 RotateAround(float2 uv, float2 c, float th)
            {
                float s = sin(th), co = cos(th); float2 d = uv - c;
                return float2(d.x * co - d.y * s, d.x * s + d.y * co) + c;
            }
            half4 frag(Vary i) : SV_Target
            {
                float t = _Time.y; int mode = (int)(_VFXMode + 0.5);
                if (mode == 0)
                {
                    half4 a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv0 + float2(_ScrollX1, _ScrollY1) * t);
                    half4 b = SAMPLE_TEXTURE2D(_MainTex2, sampler_MainTex2, i.uv0 + float2(_ScrollX2, _ScrollY2) * t);
                    half4 c = a * _Color1 + b * _Color2;
                    return half4(c.rgb * _EmissionStrength, c.a * i.col.a * _MasterOpacity);
                }
                if (mode == 1)
                {
                    float2 r = (i.uv0 - 0.5) + float2(_CenterX, _CenterY);
                    float ang = atan2(r.y, r.x); float rad = length(r);
                    float2 uv = float2(frac((ang * _AngularScale + 3.14159265) * 0.15915494 + _ScrollSpeedU * t), rad * _RadialScale + _ScrollSpeedV * t);
                    return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * _Color * i.col * Vignette(i.uv0);
                }
                if (mode == 2)
                {
                    float frame = floor(_AnimationSpeed * t);
                    float row = floor(frame / _Columns);
                    float2 r = RotateAround(i.uv0, float2(_CenterX, _CenterY), _RotationSpeed * t);
                    float cx = fmod(frame, _Columns), cy = fmod(row, _Rows);
                    float2 uv = float2(cx / _Columns + r.x / _Columns, cy / _Rows + r.y / _Rows);
                    return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * _Color * i.col * Vignette(i.uv0);
                }
                if (mode == 3)
                {
                    float2 uv = RotateAround(i.uv0, float2(_CenterX, _CenterY), _RotationSpeed * t);
                    return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * _Color * i.col * Vignette(i.uv0);
                }
                // 4/5: ScrollDistort con máscara
                float2 n = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, i.uv2 + float2(_NoiseScrollU, _NoiseScrollV) * t).xy - 0.5;
                float2 d2 = n * _DistortionStrength * 2;
                half4 A = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv0 + d2 + float2(_MainScrollU, _MainScrollV) * t) * _Color;
                half4 B = SAMPLE_TEXTURE2D(_MainTex2, sampler_MainTex2, i.uv1 + d2 + float2(_Main2ScrollU, _Main2ScrollV) * t) * _Color2;
                half4 c = (_BlendMode >= 0.5) ? A * B : A + B;
                half m = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, i.uv3 + float2(_MaskScrollU, _MaskScrollV) * t).x;
                float k;
                if (mode == 4) k = Vignette(i.uv0);
                else
                {
                    float th = _GradientRotation, s = sin(th), co = cos(th); float2 p = i.uv0 - 0.5;
                    float g = p.x * s + p.y * co + 0.5;   // el vértice original manda (sin, cos) en texcoord4
                    k = pow(g, _GradientTopPower) * _GradientTopIntensity * pow(1 - g, _GradientBottomPower) * _GradientBottomIntensity;
                }
                return c * m * i.col * k;
            }
            ENDHLSL
        }
    }
}
