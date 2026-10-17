Shader "Custom/VFX/ScrollDistort_wMask_Gradient_Additive" {
    Properties {
        _MainTex ("Main Texture 1", 2D) = "white" {}
        _MainTex2 ("Main Texture 2", 2D) = "white" {}
        _NoiseTex ("Noise Texture", 2D) = "white" {}
        _MaskTex ("Mask Texture", 2D) = "white" {}
        _Color ("Main Color", Color) = (1,1,1,1)
        _Color2 ("Secondary Color", Color) = (1,1,1,1)
        _BlendMode ("Blend Mode", Float) = 0.0
        _DistortionStrength ("Distortion Strength", Float) = 0.1
        _NoiseScrollU ("Noise Scroll U", Float) = 0.5
        _NoiseScrollV ("Noise Scroll V", Float) = 0.5
        _NoiseScale ("Noise Scale", Float) = 1.0
        _MainScrollU ("Main 1 Scroll U", Float) = 0.0
        _MainScrollV ("Main 1 Scroll V", Float) = 0.0
        _Main2ScrollU ("Main 2 Scroll U", Float) = 0.0
        _Main2ScrollV ("Main 2 Scroll V", Float) = 0.0
        _MaskScrollU ("Mask Scroll U", Float) = 0.0
        _MaskScrollV ("Mask Scroll V", Float) = 0.0
        _GradientRotation ("Gradient Rotation", Float) = 0.0
        _GradientTopIntensity ("Gradient Top Intensity", Float) = 1.0
        _GradientBottomIntensity ("Gradient Bottom Intensity", Float) = 1.0
        _GradientTopPower ("Gradient Top Power", Float) = 1.0
        _GradientBottomPower ("Gradient Bottom Power", Float) = 1.0
    }
    
    Category {
        Tags {"Queue"="Transparent+3" "RenderType"="Transparent+3"}
        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        ColorMask RGBA
        
        SubShader {
            LOD 200
            Pass {
                Name "BASE"
                
                BindChannels {
                    Bind "vertex", Vertex
                    Bind "texcoord", TexCoord0
                }
                
                Program "" {
// Vertex combos: 1
//   opengl - ALU: 41 to 41
//   d3d9 - ALU: 46 to 46
// Fragment combos: 1
//   opengl - ALU: 40 to 40, TEX: 4 to 4
//   d3d9 - ALU: 43 to 43, TEX: 4 to 4
SubProgram "opengl " {
Keywords { }
Bind "vertex", Vertex
Bind "texcoord", TexCoord0
Bind "color", Color
Local 1, [_MainTex_ST]
Local 2, [_MainTex2_ST]
Local 3, [_MaskTex_ST]
Local 4, ([_NoiseScale],0,0,0)
Local 5, ([_GradientRotation],0,0,0)
"!!ARBvp1.0
# 41 ALU
PARAM c[14] = { { 0.5, 24.980804, -24.980804, 0.15915494 },
		program.local[1..5],
		state.matrix.mvp,
		{ 0, 0.5, 1, 0.25 },
		{ -60.145809, 60.145809, 85.453789, -85.453789 },
		{ -64.939354, 64.939354, 19.739208, -19.739208 },
		{ -1, 1, -9, 0.75 } };
TEMP R0;
TEMP R1;
TEMP R2;
TEMP R3;
TEMP R4;
MOV R0.x, c[0].w;
MUL R0.x, R0, c[5];
FRC R0.w, R0.x;
ADD R0.y, R0.x, -c[10].w;
FRC R1.w, R0.y;
ADD R1.xyz, -R0.w, c[10];
MUL R2.xyz, R1, R1;
MUL R3.xyz, R2, c[0].yzyw;
ADD R0.xyz, -R1.w, c[10];
MUL R0.xyz, R0, R0;
MUL R1.xyz, R0, c[0].yzyw;
ADD R1.xyz, R1, c[11].xyxw;
MAD R1.xyz, R1, R0, c[11].zwzw;
MAD R1.xyz, R1, R0, c[12].xyxw;
MAD R1.xyz, R1, R0, c[12].zwzw;
ADD R3.xyz, R3, c[11].xyxw;
MAD R3.xyz, R3, R2, c[11].zwzw;
MAD R3.xyz, R3, R2, c[12].xyxw;
MAD R1.xyz, R1, R0, c[13].xyxw;
MAD R3.xyz, R3, R2, c[12].zwzw;
SLT R4.x, R1.w, c[10].w;
SGE R4.yz, R1.w, c[13].xzww;
MOV R0.xz, R4;
DP3 R0.y, R4, c[13].xyxw;
DP3 result.texcoord[4].x, R1, -R0;
MAD R0.xyz, R3, R2, c[13].xyxw;
SLT R1.x, R0.w, c[10].w;
SGE R1.yz, R0.w, c[13].xzww;
MOV R2.xz, R1;
DP3 R2.y, R1, c[13].xyxw;
DP3 result.texcoord[4].y, R0, -R2;
MOV result.color, vertex.color;
MAD result.texcoord[0].xy, vertex.texcoord[0], c[1], c[1].zwzw;
MAD result.texcoord[1].xy, vertex.texcoord[0], c[2], c[2].zwzw;
MUL result.texcoord[2].xy, vertex.texcoord[0], c[4].x;
ADD result.texcoord[3].zw, vertex.texcoord[0].xyxy, -c[0].x;
MAD result.texcoord[3].xy, vertex.texcoord[0], c[3], c[3].zwzw;
DP4 result.position.w, vertex.position, c[9];
DP4 result.position.z, vertex.position, c[8];
DP4 result.position.y, vertex.position, c[7];
DP4 result.position.x, vertex.position, c[6];
END
# 41 instructions, 5 R-regs
"
}

SubProgram "d3d9 " {
Keywords { }
Bind "vertex", Vertex
Bind "texcoord", TexCoord0
Bind "color", Color
Local 4, [_MainTex_ST]
Local 5, [_MainTex2_ST]
Local 6, [_MaskTex_ST]
Local 7, ([_NoiseScale],0,0,0)
Local 8, ([_GradientRotation],0,0,0)
Matrix 0, [glstate_matrix_mvp]
"vs_1_1
; 46 ALU
def c9, -0.50000000, 0.15915494, -0.25000000, 0.25000000
def c10, 0.00000000, 0.50000000, 1.00000000, -1.00000000
def c11, -9.00000000, 0.75000000, 85.45378876, -85.45378876
def c12, 24.98080444, -24.98080444, -60.14580917, 60.14580917
def c13, -64.93935394, 64.93935394, 19.73920822, -19.73920822
dcl_position v0
dcl_texcoord0 v1
dcl_color v2
mov r0.y, c8.x
mad r0.y, r0, c9, c9.z
frc r1.y, r0
mov r1.w, r1.y
mov r0.x, c8
mul r0.x, c9.y, r0
frc r0.y, r0.x
mov r0.w, r0.y
add r1.xyz, -r0.w, c10
mul r2.xyz, r1, r1
mad r3.xyz, r2, c12.xyxw, c12.zwzw
mad r3.xyz, r3, r2, c11.zwzw
mad r3.xyz, r3, r2, c13.xyxw
add r0.xyz, -r1.w, c10
mul r0.xyz, r0, r0
mad r1.xyz, r0, c12.xyxw, c12.zwzw
mad r1.xyz, r1, r0, c11.zwzw
mad r1.xyz, r1, r0, c13.xyxw
mad r1.xyz, r1, r0, c13.zwzw
mad r1.xyz, r1, r0, c10.wzww
mad r3.xyz, r3, r2, c13.zwzw
slt r4.x, r1.w, c9.w
sge r4.yz, r1.w, c11.xxyw
mov r0.xz, r4
dp3 r0.y, r4, c10.wzww
dp3 oT4.x, r1, -r0
mad r0.xyz, r3, r2, c10.wzww
slt r1.x, r0.w, c9.w
sge r1.yz, r0.w, c11.xxyw
mov r2.xz, r1
dp3 r2.y, r1, c10.wzww
dp3 oT4.y, r0, -r2
mov oD0, v2
mad oT0.xy, v1, c4, c4.zwzw
mad oT1.xy, v1, c5, c5.zwzw
mul oT2.xy, v1, c7.x
add oT3.zw, v1.xyxy, c9.x
mad oT3.xy, v1, c6, c6.zwzw
dp4 oPos.w, v0, c3
dp4 oPos.z, v0, c2
dp4 oPos.y, v0, c1
dp4 oPos.x, v0, c0
"
}

SubProgram "opengl " {
Keywords { }
Local 0, [_Time]
Local 1, [_Color]
Local 2, [_Color2]
Local 3, ([_BlendMode],0,0,0)
Local 4, ([_DistortionStrength],0,0,0)
Local 5, ([_NoiseScrollU],0,0,0)
Local 6, ([_NoiseScrollV],0,0,0)
Local 7, ([_MainScrollU],0,0,0)
Local 8, ([_MainScrollV],0,0,0)
Local 9, ([_Main2ScrollU],0,0,0)
Local 10, ([_Main2ScrollV],0,0,0)
Local 11, ([_MaskScrollU],0,0,0)
Local 12, ([_MaskScrollV],0,0,0)
Local 13, ([_GradientTopIntensity],0,0,0)
Local 14, ([_GradientBottomIntensity],0,0,0)
Local 15, ([_GradientTopPower],0,0,0)
Local 16, ([_GradientBottomPower],0,0,0)
SetTexture [_NoiseTex] {2D}
SetTexture [_MainTex] {2D}
SetTexture [_MainTex2] {2D}
SetTexture [_MaskTex] {2D}
"!!ARBfp1.0
# 40 ALU, 4 TEX
PARAM c[18] = { program.local[0..16],
		{ 0.5, 1 } };
TEMP R0;
TEMP R1;
TEMP R2;
TEMP R3;
MOV R3.zw, fragment.texcoord[4].xyxy;
MUL R3.y, fragment.texcoord[3].w, R3.w;
MAD R3.y, fragment.texcoord[3].z, R3.z, R3;
MOV R1.x, c[7];
MOV R1.y, c[8].x;
ADD R3.y, R3, c[17].x;
MOV R0.y, c[6].x;
MOV R0.x, c[5];
MAD R0.xy, R0, c[0].y, fragment.texcoord[2];
TEX R0.xy, R0, texture[0], 2D;
ADD R0.xy, R0, -c[17].x;
MUL R0.zw, R0.xyxy, c[4].x;
ADD R0.xy, fragment.texcoord[0], R0.zwzw;
MAD R0.xy, R1, c[0].y, R0;
MOV R1.x, c[9];
MOV R1.y, c[10].x;
ADD R0.zw, R0, fragment.texcoord[1].xyxy;
MAD R0.zw, R1.xyxy, c[0].y, R0;
MOV R1.y, c[12].x;
MOV R1.x, c[11];
MAD R1.xy, R1, c[0].y, fragment.texcoord[3];
TEX R2, R0.zwzw, texture[2], 2D;
TEX R0, R0, texture[1], 2D;
TEX R3.x, R1, texture[3], 2D;
MUL R2, R2, c[2];
MUL R1, R0, c[1];
MUL R1, R1, R2;
MAD R0, R0, c[1], R2;
ADD R2.y, -R3, c[17];
POW R2.y, R2.y, c[16].x;
MUL R2.z, R2.y, c[14].x;
POW R2.x, R3.y, c[15].x;
MUL R2.x, R2, c[13];
MOV R2.y, c[17].x;
ADD R2.y, -R2, c[3].x;
CMP R0, R2.y, R1, R0;
MUL R2.x, R2, R2.z;
MUL R1.x, R3, R2;
MUL R0, R0, fragment.color.primary;
MUL result.color, R0, R1.x;
END
# 40 instructions, 4 R-regs
"
}

SubProgram "d3d9 " {
Keywords { }
Local 0, [_Time]
Local 1, [_Color]
Local 2, [_Color2]
Local 3, ([_BlendMode],0,0,0)
Local 4, ([_DistortionStrength],0,0,0)
Local 5, ([_NoiseScrollU],0,0,0)
Local 6, ([_NoiseScrollV],0,0,0)
Local 7, ([_MainScrollU],0,0,0)
Local 8, ([_MainScrollV],0,0,0)
Local 9, ([_Main2ScrollU],0,0,0)
Local 10, ([_Main2ScrollV],0,0,0)
Local 11, ([_MaskScrollU],0,0,0)
Local 12, ([_MaskScrollV],0,0,0)
Local 13, ([_GradientTopIntensity],0,0,0)
Local 14, ([_GradientBottomIntensity],0,0,0)
Local 15, ([_GradientTopPower],0,0,0)
Local 16, ([_GradientBottomPower],0,0,0)
SetTexture [_NoiseTex] {2D}
SetTexture [_MainTex] {2D}
SetTexture [_MainTex2] {2D}
SetTexture [_MaskTex] {2D}
"ps_2_0
; 43 ALU, 4 TEX
dcl_2d s0
dcl_2d s1
dcl_2d s2
dcl_2d s3
def c17, -0.50000000, 0.50000000, 1.00000000, 0
dcl t0.xy
dcl t1.xy
dcl t2.xy
dcl t3
dcl t4.xy
dcl v0
mov r0.y, c6.x
mov r0.x, c5
mad r0.xy, r0, c0.y, t2
mov r1.x, c7
mov r1.y, c8.x
texld r0, r0, s0
add r0.xy, r0, c17.x
mul r0.xy, r0, c4.x
add r2.xy, t0, r0
add r3.xy, r0, t1
mad r1.xy, r1, c0.y, r2
mov r0.y, c12.x
mov r0.x, c11
mad r2.xy, r0, c0.y, t3
mov r0.x, c9
mov r0.y, c10.x
mad r0.xy, r0, c0.y, r3
texld r5, r2, s3
texld r0, r0, s2
texld r1, r1, s1
mul r4, r0, c2
mad r2, r1, c1, r4
mul r3, r1, c1
mov r0.y, t4
mul r4, r3, r4
mov r3.x, c3
add r3.x, c17, r3
mul r1.x, t3.w, r0.y
mov r0.x, t4
mad r0.x, t3.z, r0, r1
add r0.x, r0, c17.y
add r1.x, -r0, c17.z
cmp r3, r3.x, r2, r4
pow r2, r1.x, c16.x
pow r1, r0.x, c15.x
mov r0.x, r2
mul r1.x, r1, c13
mul r0.x, r0, c14
mul r0.x, r1, r0
mul r0.x, r5, r0
mul r1, r3, v0
mul r0, r1, r0.x
mov oC0, r0
"
}

}
#LINE 124

            }
        }
        
        SubShader {
            LOD 100
            Pass {
                SetTexture [_MainTex] {
                    constantColor [_Color]
                    Combine texture * constant
                }
            }
        }
    }
    
    FallBack "Diffuse"
}
