Shader "Custom/SkinnedToonShading_blendSrcalphaInvsrcalpha_Rim" {
    Properties {
        _Color ("Main Color", Color) = (0.5,0.5,0.5,1)
        _AmbColor ("Ambient Color", Color) = (0,0,0,1)
        _Emission ("Emmisive Color", Color) = (0.235,0.235,0.235,0)
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _Outline ("Outline width", Range (.002, 0.03)) = .005
        _MainTex ("Base (RGB)", 2D) = "white" {}
        _SpecMap ("SpecRamp (RGB)", 2D) = "" {}     
        _ShaderMap ("ToonRamp (RGB)", 2D) = "white" {}
        _EmissionStrength ("Emission Strength", Float) = 1
        _RimPower ("Rim Power", Float) = 3
        _RimIntensity ("Rim Intensity", Float) = 3
        _RimColor ("Rim Color", Color) = (1, 1, 1, 1)
        _OverlayColor1 ("Overlay Color 1", Color) = (1, 1, 1, 1)
        _OverlayTex1 ("Overlay Texture 1", 2D) = "white" {}
        _Overlay1ScrollSpeed ("Overlay 1 Scroll Speed", Vector) = (0, 0, 0, 0)
        _OverlayColor2 ("Overlay Color 2", Color) = (1, 1, 1, 1)
        _OverlayTex2 ("Overlay Texture 2", 2D) = "white" {}
        _Overlay2ScrollSpeed ("Overlay 2 Scroll Speed", Vector) = (0, 0, 0, 0)
        _UseOverlays ("Use Overlays", Range(0, 1)) = 0
        _Transparency ("Transparency", Range(0, 1)) = 0.5
    }
    Category {
        Tags {"Queue"="Transparent-100"}
        Blend SrcAlpha OneMinusSrcAlpha 
        ColorMask RGBA
        SubShader {
            LOD 300
            Pass {
                Name "BASE"
                BindChannels {
                    Bind "vertex", Vertex
                    Bind "normal", Normal
                    Bind "texcoord", TexCoord0
                }
                Lighting On
                Program "" {
// Vertex combos: 1
//   opengl - ALU: 47 to 47
//   d3d9 - ALU: 48 to 48
// Fragment combos: 1
//   opengl - ALU: 28 to 28, TEX: 4 to 4
//   d3d9 - ALU: 28 to 28, TEX: 4 to 4
SubProgram "opengl " {
Keywords { }
Bind "vertex", Vertex
Bind "normal", Normal
Bind "texcoord", TexCoord0
Local 1, [_Time]
Local 2, [_Overlay1ScrollSpeed]
Local 3, [_Overlay2ScrollSpeed]
Local 4, ([_FatFactor],0,0,0)
Local 5, [_MainTex_ST]
"!!ARBvp1.0
# 47 ALU
PARAM c[19] = { { 1, 0, 0.0099999998, 0.99000001 },
		program.local[1..5],
		state.light[0].position,
		state.matrix.modelview[0],
		state.matrix.mvp,
		state.matrix.modelview[0].invtrans };
TEMP R0;
TEMP R1;
TEMP R2;
TEMP R3;
MUL R0.xyz, vertex.normal, c[4].x;
ADD R2.xyz, vertex.position, R0;
MOV R2.w, vertex.position;
MOV R1.x, c[15].w;
MOV R1.y, c[16].w;
MOV R1.z, c[17].w;
MOV R1.w, c[18];
DP4 R0.x, R2, c[7];
DP4 R0.y, R2, c[8];
DP4 R0.z, R2, c[9];
DP4 R0.w, R2, c[10];
ADD R0, R1, -R0;
DP4 R0.w, R0, R0;
RSQ R0.w, R0.w;
DP3 R1.y, vertex.normal, c[16];
DP3 R1.x, vertex.normal, c[15];
DP3 R1.z, vertex.normal, c[17];
DP3 R1.w, R1, R0;
ADD R3.x, -R1.w, c[0];
DP3 R3.y, R0, -c[6];
MUL result.texcoord[5].xyz, R0.w, R0;
DP4 R0.x, R2, c[13];
DP3 R1.w, R1, c[6];
MAD R3.x, R3, R3.y, R1.w;
MIN R3.x, R3, c[0];
MAX R3.x, R3, c[0].y;
ADD R1.w, R1, R3.x;
MAX R1.w, R1, c[0].z;
DP3 R0.y, R1, R1;
RSQ R0.y, R0.y;
MOV result.position.z, R0.x;
MOV result.fogcoord.x, R0;
MOV R0.x, c[1].y;
MIN result.texcoord[1].x, R1.w, c[0].w;
DP4 result.position.x, R2, c[11];
DP4 result.position.y, R2, c[12];
DP4 result.position.w, R2, c[14];
MUL result.texcoord[4].xyz, R0.y, R1;
MOV result.texcoord[1].yzw, c[0].x;
MOV result.texcoord[0].zw, c[0].x;
MAD result.texcoord[0].xy, vertex.texcoord[0], c[5], c[5].zwzw;
MOV result.texcoord[2].zw, c[0].y;
MAD result.texcoord[2].xy, R0.x, c[2], vertex.texcoord[0];
MOV result.texcoord[3].zw, c[0].y;
MAD result.texcoord[3].xy, R0.x, c[3], vertex.texcoord[0];
MOV result.texcoord[4].w, c[0].y;
MOV result.texcoord[5].w, c[0].y;
END
# 47 instructions, 4 R-regs
"
}

SubProgram "d3d9 " {
Keywords { }
Bind "vertex", Vertex
Bind "normal", Normal
Bind "texcoord", TexCoord0
Local 12, [_Time]
Local 13, [_Overlay1ScrollSpeed]
Local 14, [_Overlay2ScrollSpeed]
Local 15, ([_FatFactor],0,0,0)
Local 16, [_MainTex_ST]
Local 17, [glstate_light0_position]
Matrix 0, [glstate_matrix_modelview0]
Matrix 4, [glstate_matrix_mvp]
Matrix 8, [glstate_matrix_invtrans_modelview0]
"vs_1_1
; 48 ALU
def c18, 1.00000000, 0.00000000, 0.01000000, 0.99000001
dcl_position v0
dcl_normal v1
dcl_texcoord0 v2
mul r0.xyz, v1, c15.x
add r2.xyz, v0, r0
mov r2.w, v0
mov r1.x, c8.w
mov r1.y, c9.w
mov r1.z, c10.w
mov r1.w, c11
dp4 r0.x, r2, c0
dp4 r0.y, r2, c1
dp4 r0.z, r2, c2
dp4 r0.w, r2, c3
add r0, r1, -r0
dp4 r0.w, r0, r0
rsq r0.w, r0.w
dp3 r1.y, v1, c9
dp3 r1.x, v1, c8
dp3 r1.z, v1, c10
dp3 r1.w, r1, r0
add r3.x, -r1.w, c18
dp3 r3.y, r0, -c17
mul oT5.xyz, r0.w, r0
dp4 r0.x, r2, c6
dp3 r1.w, r1, c17
mad r3.x, r3, r3.y, r1.w
min r3.x, r3, c18
max r3.x, r3, c18.y
add r1.w, r1, r3.x
max r1.w, r1, c18.z
dp3 r0.y, r1, r1
rsq r0.y, r0.y
mov oPos.z, r0.x
mul oT4.xyz, r0.y, r1
mov oFog, r0.x
mov r0.xy, c13
mad oT2.xy, c12.y, r0, v2
mov r0.xy, c14
min oT1.x, r1.w, c18.w
dp4 oPos.x, r2, c4
dp4 oPos.y, r2, c5
dp4 oPos.w, r2, c7
mov oT1.yzw, c18.x
mov oT0.zw, c18.x
mad oT0.xy, v2, c16, c16.zwzw
mov oT2.zw, c18.y
mov oT3.zw, c18.y
mad oT3.xy, c12.y, r0, v2
mov oT4.w, c18.y
mov oT5.w, c18.y
"
}

SubProgram "opengl " {
Keywords { }
Local 0, [_Color]
Local 1, ([_EmissionStrength],0,0,0)
Local 2, ([_RimPower],0,0,0)
Local 3, ([_RimIntensity],0,0,0)
Local 4, [_RimColor]
Local 5, [_OverlayColor1]
Local 6, [_OverlayColor2]
Local 7, ([_UseOverlays],0,0,0)
Local 8, ([_Transparency],0,0,0)
SetTexture [_MainTex] {2D}
SetTexture [_ShaderMap] {2D}
SetTexture [_OverlayTex1] {2D}
SetTexture [_OverlayTex2] {2D}
"!!ARBfp1.0
# 28 ALU, 4 TEX
PARAM c[10] = { program.local[0..8],
		{ 0.5, 2, 0, 1 } };
TEMP R0;
TEMP R1;
TEMP R2;
TEMP R3;
TEMP R4;
TEX R3, fragment.texcoord[2], texture[2], 2D;
TEX R2, fragment.texcoord[3], texture[3], 2D;
MOV R4.xyz, fragment.texcoord[5];
DP3 R4.x, fragment.texcoord[4], R4;
MAX R4.x, R4, c[9].z;
ADD R4.x, -R4, c[9].w;
POW R4.x, R4.x, c[2].x;
MOV R0.x, fragment.texcoord[1];
MOV R0.y, c[9].x;
MUL R4.xyz, R4.x, c[4];
TEX R1, R0, texture[1], 2D;
TEX R0, fragment.texcoord[0], texture[0], 2D;
MUL R0, R0, c[0];
MUL R0, R0, R1;
MUL R0, R0, c[9].y;
MUL R1.xyz, R4, c[3].x;
MAD R0.xyz, R0, c[1].x, R1;
MUL R1, R3, c[5];
ADD R1.xyz, R1, -R0;
MOV R3.x, c[9];
MAD R1.xyz, R1.w, R1, R0;
ADD R3.x, -R3, c[7];
CMP R0.xyz, -R3.x, R1, R0;
MUL R1, R2, c[6];
ADD R1.xyz, R1, -R0;
MAD R1.xyz, R1.w, R1, R0;
CMP result.color.xyz, -R3.x, R1, R0;
MUL result.color.w, R0, c[8].x;
END
# 28 instructions, 5 R-regs
"
}

SubProgram "d3d9 " {
Keywords { }
Local 0, [_Color]
Local 1, ([_EmissionStrength],0,0,0)
Local 2, ([_RimPower],0,0,0)
Local 3, ([_RimIntensity],0,0,0)
Local 4, [_RimColor]
Local 5, [_OverlayColor1]
Local 6, [_OverlayColor2]
Local 7, ([_UseOverlays],0,0,0)
Local 8, ([_Transparency],0,0,0)
SetTexture [_MainTex] {2D}
SetTexture [_ShaderMap] {2D}
SetTexture [_OverlayTex1] {2D}
SetTexture [_OverlayTex2] {2D}
"ps_2_0
; 28 ALU, 4 TEX
dcl_2d s0
dcl_2d s1
dcl_2d s2
dcl_2d s3
def c9, 0.50000000, 2.00000000, -0.50000000, 0.00000000
def c10, 1.00000000, 0, 0, 0
dcl t0.xy
dcl t1.x
dcl t2.xy
dcl t3.xy
dcl t4.xyz
dcl t5.xyz
texld r1, t3, s3
texld r2, t2, s2
texld r5, t0, s0
mul r1, r1, c6
mov r0.x, t1
mov r0.y, c9.x
mul r2, r2, c5
texld r4, r0, s1
mov r0.xyz, t5
dp3 r0.x, t4, r0
max r0.x, r0, c9.w
add r0.x, -r0, c10
pow r3, r0.x, c2.x
mul r0, r5, c0
mul r4, r0, r4
mov r0.x, r3
mul r3, r4, c9.y
mul r0.xyz, r0.x, c4
mul r0.xyz, r0, c3.x
mad r3.xyz, r3, c1.x, r0
add r0.xyz, -r3, r2
mad r2.xyz, r2.w, r0, r3
mov r0.x, c7
add r0.x, c9.z, r0
cmp r2.xyz, -r0.x, r3, r2
add r1.xyz, -r2, r1
mad r1.xyz, r1.w, r1, r2
cmp r0.xyz, -r0.x, r2, r1
mul r0.w, r3, c8.x
mov oC0, r0
"
}

}
#LINE 226

            }
            Pass {
                Name "OUTLINE"
                Tags { "LightMode" = "Always" }
                BindChannels {
                    Bind "vertex", Vertex
                    Bind "normal", Normal
                }
                Program "" {
// Vertex combos: 1
//   opengl - ALU: 12 to 12
//   d3d9 - ALU: 12 to 12
SubProgram "opengl " {
Keywords { }
Bind "vertex", Vertex
Bind "normal", Normal
Local 1, [_OutlineColor]
Local 2, ([_FatFactor],0,0,0)
Local 3, ([_Outline],0,0,0)
"!!ARBvp1.0
# 12 ALU
PARAM c[8] = { program.local[0..3],
		state.matrix.mvp };
TEMP R0;
TEMP R1;
MOV R0.x, c[3];
ADD R0.x, R0, c[2];
MUL R0.xyz, vertex.normal, R0.x;
MOV R0.w, vertex.position;
ADD R0.xyz, R0, vertex.position;
DP4 R1.x, R0, c[6];
DP4 result.position.x, R0, c[4];
DP4 result.position.y, R0, c[5];
MOV result.position.z, R1.x;
DP4 result.position.w, R0, c[7];
MOV result.fogcoord.x, R1;
MOV result.color, c[1];
END
# 12 instructions, 2 R-regs
"
}

SubProgram "d3d9 " {
Keywords { }
Bind "vertex", Vertex
Bind "normal", Normal
Local 4, [_OutlineColor]
Local 5, ([_FatFactor],0,0,0)
Local 6, ([_Outline],0,0,0)
Matrix 0, [glstate_matrix_mvp]
"vs_1_1
; 12 ALU
dcl_position v0
dcl_normal v1
mov r0.x, c5
add r0.x, c6, r0
mul r0.xyz, v1, r0.x
mov r0.w, v0
add r0.xyz, r0, v0
dp4 r1.x, r0, c2
dp4 oPos.x, r0, c0
dp4 oPos.y, r0, c1
mov oPos.z, r1.x
dp4 oPos.w, r0, c3
mov oFog, r1.x
mov oD0, c4
"
}

}
#LINE 302

                Cull Front
                ZWrite On
                ColorMask RGBA
                Blend SrcAlpha OneMinusSrcAlpha
                SetTexture [_MainTex] { combine primary }
            }
        }
    }
    FallBack "Skin_DirLight_AmbLight_blendSrcalphaInvsrcalpha", 1
}