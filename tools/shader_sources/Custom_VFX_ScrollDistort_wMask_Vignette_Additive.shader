Shader "Custom/VFX/ScrollDistort_wMask_Vignette_Additive" {
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
        _VignetteIntensity ("Vignette Intensity", Float) = 0.0
        _VignettePower ("Vignette Power", Float) = 2.0
    }
    
    Category {
        Tags {"Queue"="Transparent" "RenderType"="Transparent+3"}
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
//   opengl - ALU: 9 to 9
//   d3d9 - ALU: 9 to 9
// Fragment combos: 1
//   opengl - ALU: 42 to 42, TEX: 4 to 4
//   d3d9 - ALU: 42 to 42, TEX: 4 to 4
SubProgram "opengl " {
Keywords { }
Bind "vertex", Vertex
Bind "texcoord", TexCoord0
Bind "color", Color
Local 1, [_MainTex_ST]
Local 2, [_MainTex2_ST]
Local 3, [_MaskTex_ST]
Local 4, ([_NoiseScale],0,0,0)
"!!ARBvp1.0
# 9 ALU
PARAM c[9] = { program.local[0..4],
		state.matrix.mvp };
MOV result.color, vertex.color;
MAD result.texcoord[0].xy, vertex.texcoord[0], c[1], c[1].zwzw;
MAD result.texcoord[1].xy, vertex.texcoord[0], c[2], c[2].zwzw;
MUL result.texcoord[2].xy, vertex.texcoord[0], c[4].x;
MAD result.texcoord[3].xy, vertex.texcoord[0], c[3], c[3].zwzw;
DP4 result.position.w, vertex.position, c[8];
DP4 result.position.z, vertex.position, c[7];
DP4 result.position.y, vertex.position, c[6];
DP4 result.position.x, vertex.position, c[5];
END
# 9 instructions, 0 R-regs
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
Matrix 0, [glstate_matrix_mvp]
"vs_1_1
; 9 ALU
dcl_position v0
dcl_texcoord0 v1
dcl_color v2
mov oD0, v2
mad oT0.xy, v1, c4, c4.zwzw
mad oT1.xy, v1, c5, c5.zwzw
mul oT2.xy, v1, c7.x
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
Local 13, ([_VignetteIntensity],0,0,0)
Local 14, ([_VignettePower],0,0,0)
SetTexture [_NoiseTex] {2D}
SetTexture [_MainTex] {2D}
SetTexture [_MainTex2] {2D}
SetTexture [_MaskTex] {2D}
"!!ARBfp1.0
# 42 ALU, 4 TEX
PARAM c[16] = { program.local[0..14],
		{ 0.5, 2, 1 } };
TEMP R0;
TEMP R1;
TEMP R2;
TEMP R3;
MOV R0.y, c[6].x;
MOV R0.x, c[5];
MAD R0.xy, R0, c[0].y, fragment.texcoord[2];
TEX R0.xy, R0, texture[0], 2D;
ADD R0.xy, R0, -c[15].x;
MUL R0.xy, R0, c[4].x;
MUL R0.zw, R0.xyxy, c[15].y;
ADD R1.xy, fragment.texcoord[0], R0.zwzw;
MOV R0.x, c[7];
MOV R0.y, c[8].x;
MAD R0.xy, R0, c[0].y, R1;
ADD R1.xy, R0.zwzw, fragment.texcoord[1];
MOV R0.z, c[9].x;
MOV R0.w, c[10].x;
MAD R0.zw, R0, c[0].y, R1.xyxy;
MOV R1.y, c[12].x;
MOV R1.x, c[11];
MAD R2.xy, R1, c[0].y, fragment.texcoord[3];
TEX R1, R0.zwzw, texture[2], 2D;
TEX R3.x, R2, texture[3], 2D;
TEX R0, R0, texture[1], 2D;
ADD R2.xy, fragment.texcoord[0], -c[15].x;
MUL R2.xy, R2, R2;
ADD R2.x, R2, R2.y;
RSQ R3.y, R2.x;
RCP R3.y, R3.y;
MUL R3.y, R3, c[15];
MUL R1, R1, c[2];
MUL R2, R0, c[1];
MAD R0, R0, c[1], R1;
MUL R2, R2, R1;
POW R3.y, R3.y, c[14].x;
ADD_SAT R1.y, -R3, c[15].z;
MOV R1.x, c[15];
ADD R1.x, -R1, c[3];
CMP R0, R1.x, R2, R0;
ADD R1.y, R1, -c[15].z;
MUL R1.x, R1.y, c[13];
MUL R0, R0, R3.x;
ADD R1.x, R1, c[15].z;
MUL R0, R0, fragment.color.primary;
MUL result.color, R0, R1.x;
END
# 42 instructions, 4 R-regs
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
Local 13, ([_VignetteIntensity],0,0,0)
Local 14, ([_VignettePower],0,0,0)
SetTexture [_NoiseTex] {2D}
SetTexture [_MainTex] {2D}
SetTexture [_MainTex2] {2D}
SetTexture [_MaskTex] {2D}
"ps_2_0
; 42 ALU, 4 TEX
dcl_2d s0
dcl_2d s1
dcl_2d s2
dcl_2d s3
def c15, 2.00000000, -0.50000000, 1.00000000, -1.00000000
dcl t0.xy
dcl t1.xy
dcl t2.xy
dcl t3.xy
dcl v0
mov r0.y, c6.x
mov r0.x, c5
mad r0.xy, r0, c0.y, t2
texld r0, r0, s0
add r0.xy, r0, c15.y
mul r0.xy, r0, c4.x
mul r1.xy, r0, c15.x
add r2.xy, t0, r1
add r3.xy, r1, t1
mov r1.y, c12.x
mov r1.x, c11
mov r0.x, c7
mov r0.y, c8.x
mad r0.xy, r0, c0.y, r2
mad r2.xy, r1, c0.y, t3
mov r1.x, c9
mov r1.y, c10.x
mad r1.xy, r1, c0.y, r3
texld r5, r2, s3
texld r2, r0, s1
texld r1, r1, s2
add r0.xy, t0, c15.y
mul r0.xy, r0, r0
add r0.x, r0, r0.y
rsq r0.x, r0.x
rcp r0.x, r0.x
mul r3, r1, c2
mul r0.x, r0, c15
pow r1, r0.x, c14.x
mul r0, r2, c1
mad r4, r2, c1, r3
mul r2, r0, r3
mov r0.x, r1
mov r1.x, c3
add_sat r0.x, -r0, c15.z
add r1.x, c15.y, r1
add r0.x, r0, c15.w
mul r0.x, r0, c13
cmp r1, r1.x, r4, r2
mul r1, r1, r5.x
add r0.x, r0, c15.z
mul r1, r1, v0
mul r0, r1, r0.x
mov oC0, r0
"
}

}
#LINE 121

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