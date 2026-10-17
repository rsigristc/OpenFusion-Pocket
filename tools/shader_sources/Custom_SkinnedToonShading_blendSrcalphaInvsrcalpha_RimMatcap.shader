Shader "Custom/SkinnedToonShading_blendSrcalphaInvsrcalpha_RimMatcap" {
    Properties {
        _Color ("Main Color", Color) = (0.5,0.5,0.5,1)
        _AmbColor ("Ambient Color", Color) = (0,0,0,1)
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _Outline ("Outline width", Float) = .005
        _MainTex ("Base (RGB)", 2D) = "white" {}
        _SpecMap ("SpecRamp (RGB)", 2D) = "" {}
        _ShaderMap ("ToonRamp (RGB)", 2D) = "white" {}
        _EmissionStrength ("Emission Strength", Float) = 1
        _RimPower ("Rim Power", Float) = 3
        _RimIntensity ("Rim Intensity", Float) = 3
        _RimColor ("Rim Color", Color) = (1, 1, 1, 1)
        _MaskTex ("Mask (R=Rim, G=Emission, B=MatCap, A=MatCap BW)", 2D) = "white" {}
        _EmissionIntensity ("Emission Intensity", Float) = 0.5
        _MatCapStrength ("MatCap Strength", Float) = 0.25
        _MatCapBlendMode ("MatCap Blend (0=Add, 1=Multiply)", Float) = 0
        _Transparency ("Transparency", Float) = 0.5
    }
   
    Category {
        Tags {"Queue"="Transparent-100"}
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask RGBA
       
        // SubShader with BOTH passes - LOD 300 (with outline)
        SubShader {
            LOD 300
           
            // BASE PASS
            Pass {
                Name "BASE"
                Cull Back
                ZWrite On
                ZTest LEqual
               
                BindChannels {
                    Bind "vertex", Vertex
                    Bind "normal", Normal
                    Bind "texcoord", TexCoord0
                }
                Lighting On
                Program "" {
// Vertex combos: 1
//   opengl - ALU: 47 to 47
//   d3d9 - ALU: 47 to 47
// Fragment combos: 1
//   opengl - ALU: 39 to 39, TEX: 4 to 4
//   d3d9 - ALU: 39 to 39, TEX: 4 to 4
SubProgram "opengl " {
Keywords { }
Bind "vertex", Vertex
Bind "normal", Normal
Bind "texcoord", TexCoord0
Local 1, ([_FatFactor],0,0,0)
Local 2, [_MainTex_ST]
"!!ARBvp1.0
# 47 ALU
PARAM c[17] = { { 1, 0, 0.0099999998, 0.99000001 },
		program.local[1..2],
		state.light[0].position,
		state.matrix.modelview[0],
		state.matrix.mvp,
		state.matrix.modelview[0].invtrans,
		{ 0.5 } };
TEMP R0;
TEMP R1;
TEMP R2;
TEMP R3;
MUL R0.xyz, vertex.normal, c[1].x;
ADD R1.xyz, vertex.position, R0;
MOV R1.w, vertex.position;
DP4 R0.w, R1, c[7];
DP4 R0.z, R1, c[6];
DP4 R0.y, R1, c[5];
DP4 R0.x, R1, c[4];
MOV R2.w, c[15];
MOV R2.z, c[14].w;
MOV R2.y, c[13].w;
MOV R2.x, c[12].w;
ADD R2, R2, -R0;
DP4 R2.w, R2, R2;
RSQ R2.w, R2.w;
MUL R3.xyz, R2.w, R2;
DP3 R0.z, vertex.normal, c[6];
DP3 R0.x, vertex.normal, c[4];
DP3 R0.y, vertex.normal, c[5];
DP3 R0.w, R0, R0;
RSQ R0.w, R0.w;
MUL R2.xyz, R0.w, R0;
DP3 R0.x, R2, R3;
ADD R0.z, -R0.x, c[0].x;
DP3 R0.x, R2, c[3];
DP3 R0.y, R3, -c[3];
MAD R0.y, R0.z, R0, R0.x;
MIN R0.y, R0, c[0].x;
MAX R0.y, R0, c[0];
ADD R0.w, R0.x, R0.y;
MAX R0.w, R0, c[0].z;
DP3 R0.x, R2, c[12];
DP3 R0.y, R2, c[13];
DP3 R0.z, R2, c[14];
DP3 R0.z, R0, R0;
RSQ R0.z, R0.z;
MUL R0.xy, R0.z, R0;
MAD result.texcoord[4].xy, R0, c[16].x, c[16].x;
DP4 R0.x, R1, c[10];
MIN result.texcoord[1].x, R0.w, c[0].w;
MOV result.texcoord[2].xyz, R3;
DP4 result.position.w, R1, c[11];
MOV result.position.z, R0.x;
DP4 result.position.y, R1, c[9];
DP4 result.position.x, R1, c[8];
MOV result.texcoord[3].xyz, R2;
MOV result.fogcoord.x, R0;
MAD result.texcoord[0].xy, vertex.texcoord[0], c[2], c[2].zwzw;
END
# 47 instructions, 4 R-regs
"
}

SubProgram "d3d9 " {
Keywords { }
Bind "vertex", Vertex
Bind "normal", Normal
Bind "texcoord", TexCoord0
Local 12, ([_FatFactor],0,0,0)
Local 13, [_MainTex_ST]
Local 14, [glstate_light0_position]
Matrix 0, [glstate_matrix_modelview0]
Matrix 4, [glstate_matrix_mvp]
Matrix 8, [glstate_matrix_invtrans_modelview0]
"vs_1_1
; 47 ALU
def c15, 1.00000000, 0.00000000, 0.01000000, 0.99000001
def c16, 0.50000000, 0, 0, 0
dcl_position v0
dcl_normal v1
dcl_texcoord0 v2
mul r0.xyz, v1, c12.x
add r1.xyz, v0, r0
mov r1.w, v0
dp4 r0.w, r1, c3
dp4 r0.z, r1, c2
dp4 r0.y, r1, c1
dp4 r0.x, r1, c0
mov r2.w, c11
mov r2.z, c10.w
mov r2.y, c9.w
mov r2.x, c8.w
add r2, r2, -r0
dp4 r2.w, r2, r2
rsq r2.w, r2.w
mul r3.xyz, r2.w, r2
dp3 r0.z, v1, c2
dp3 r0.x, v1, c0
dp3 r0.y, v1, c1
dp3 r0.w, r0, r0
rsq r0.w, r0.w
mul r2.xyz, r0.w, r0
dp3 r0.x, r2, r3
add r0.z, -r0.x, c15.x
dp3 r0.x, r2, c14
dp3 r0.y, r3, -c14
mad r0.y, r0.z, r0, r0.x
min r0.y, r0, c15.x
max r0.y, r0, c15
add r0.w, r0.x, r0.y
max r0.w, r0, c15.z
dp3 r0.x, r2, c8
dp3 r0.y, r2, c9
dp3 r0.z, r2, c10
dp3 r0.z, r0, r0
rsq r0.z, r0.z
mul r0.xy, r0.z, r0
mad oT4.xy, r0, c16.x, c16.x
dp4 r0.x, r1, c6
min oT1.x, r0.w, c15.w
mov oT2.xyz, r3
dp4 oPos.w, r1, c7
mov oPos.z, r0.x
dp4 oPos.y, r1, c5
dp4 oPos.x, r1, c4
mov oT3.xyz, r2
mov oFog, r0.x
mad oT0.xy, v2, c13, c13.zwzw
"
}

SubProgram "opengl " {
Keywords { }
Local 0, [_Color]
Local 1, ([_EmissionStrength],0,0,0)
Local 2, ([_RimPower],0,0,0)
Local 3, ([_RimIntensity],0,0,0)
Local 4, [_RimColor]
Local 5, ([_EmissionIntensity],0,0,0)
Local 6, ([_MatCapStrength],0,0,0)
Local 7, ([_MatCapBlendMode],0,0,0)
Local 8, ([_Transparency],0,0,0)
SetTexture [_MainTex] {2D}
SetTexture [_ShaderMap] {2D}
SetTexture [_MaskTex] {2D}
"!!ARBfp1.0
# 39 ALU, 4 TEX
PARAM c[10] = { program.local[0..8],
		{ 0.5, 2, 0, 1 } };
TEMP R0;
TEMP R1;
TEMP R2;
TEMP R3;
TEX R2.xyz, fragment.texcoord[0], texture[2], 2D;
TEX R2.w, fragment.texcoord[4], texture[2], 2D;
MOV R0.y, c[9].x;
MOV R0.x, fragment.texcoord[1];
TEX R1, R0, texture[1], 2D;
TEX R0, fragment.texcoord[0], texture[0], 2D;
MUL R3.xyz, R0, R2.y;
MUL R0, R0, c[0];
MUL R0, R0, R1;
MUL R0, R0, c[9].y;
MUL R1.xyz, R3, c[5].x;
MAD R0.xyz, R0, c[1].x, R1;
MOV R1.w, c[9].x;
MUL R1.x, R2.z, R2.w;
MAD R1.xyz, R1.x, c[6].x, R0;
ADD R2.y, -R1.w, c[7].x;
CMP R3.xyz, R2.y, R1, R0;
DP3 R0.y, fragment.texcoord[2], fragment.texcoord[2];
RSQ R0.y, R0.y;
DP3 R0.x, fragment.texcoord[3], fragment.texcoord[3];
MUL R1.xyz, R0.y, fragment.texcoord[2];
RSQ R0.x, R0.x;
MUL R0.xyz, R0.x, fragment.texcoord[3];
DP3 R0.x, R0, R1;
MAX R0.x, R0, c[9].z;
ADD R0.x, -R0, c[9].w;
MUL R0.z, R2, c[6].x;
ADD R0.y, R2.w, -c[9].w;
MAD R0.y, R0.z, R0, c[9].w;
MUL R1.xyz, R3, R0.y;
SLT R0.y, c[7].x, R1.w;
ABS R1.w, R0.y;
POW R0.x, R0.x, c[2].x;
MUL R0.xyz, R0.x, c[4];
CMP R1.w, -R1, c[9].z, c[9];
CMP R1.xyz, -R1.w, R1, R3;
MUL R0.xyz, R0, c[3].x;
MAD result.color.xyz, R0, R2.x, R1;
MUL result.color.w, R0, c[8].x;
END
# 39 instructions, 4 R-regs
"
}

SubProgram "d3d9 " {
Keywords { }
Local 0, [_Color]
Local 1, ([_EmissionStrength],0,0,0)
Local 2, ([_RimPower],0,0,0)
Local 3, ([_RimIntensity],0,0,0)
Local 4, [_RimColor]
Local 5, ([_EmissionIntensity],0,0,0)
Local 6, ([_MatCapStrength],0,0,0)
Local 7, ([_MatCapBlendMode],0,0,0)
Local 8, ([_Transparency],0,0,0)
SetTexture [_MainTex] {2D}
SetTexture [_ShaderMap] {2D}
SetTexture [_MaskTex] {2D}
"ps_2_0
; 39 ALU, 4 TEX
dcl_2d s0
dcl_2d s1
dcl_2d s2
def c9, -0.50000000, 0.00000000, 1.00000000, 0.50000000
def c10, 2.00000000, -1.00000000, 0, 0
dcl t0.xy
dcl t1.x
dcl t2.xyz
dcl t3.xyz
dcl t4.xy
texld r4, t4, s2
texld r2, t0, s2
texld r1, t0, s0
mul r3, r1, c0
mul r1.xyz, r1, r2.y
mul r1.xyz, r1, c5.x
mov r0.x, t1
mov r0.y, c9.w
texld r0, r0, s1
mul r0, r3, r0
mul r0, r0, c10.x
mad r3.xyz, r0, c1.x, r1
mul r0.x, r2.z, r4.w
mad r4.xyz, r0.x, c6.x, r3
dp3 r1.x, t3, t3
dp3 r0.x, t2, t2
rsq r0.x, r0.x
mul r5.xyz, r0.x, t2
rsq r1.x, r1.x
mul r1.xyz, r1.x, t3
mov r0.x, c7
add r0.x, c9, r0
cmp r3.xyz, r0.x, r3, r4
dp3 r1.x, r1, r5
max r0.x, r1, c9.y
mul r4.x, r2.z, c6
add r1.x, r4.w, c10.y
mad r1.x, r4, r1, c9.z
add r0.x, -r0, c9.z
mul r4.xyz, r3, r1.x
pow r1, r0.x, c2.x
mov r0.x, c7
add r0.x, c9, r0
cmp r0.x, r0, c9.y, c9.z
mul r1.xyz, r1.x, c4
abs_pp r0.x, r0
cmp r0.xyz, -r0.x, r4, r3
mul r1.xyz, r1, c3.x
mad r0.xyz, r1, r2.x, r0
mul r0.w, r0, c8.x
mov oC0, r0
"
}

}
#LINE 199

            }
           
            // OUTLINE PASS
            Pass {
                Name "OUTLINE"
                Tags { "LightMode" = "Always" }
                Cull Front
                ZWrite On
                ColorMask RGBA
                Blend SrcAlpha OneMinusSrcAlpha
               
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
MOV R0.x, c[2];
ADD R0.x, R0, c[3];
MUL R0.xyz, vertex.normal, R0.x;
MOV R0.w, vertex.position;
ADD R0.xyz, R0, vertex.position;
DP4 R1.x, R0, c[6];
DP4 result.position.w, R0, c[7];
MOV result.position.z, R1.x;
DP4 result.position.y, R0, c[5];
DP4 result.position.x, R0, c[4];
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
mov r0.x, c6
add r0.x, c5, r0
mul r0.xyz, v1, r0.x
mov r0.w, v0
add r0.xyz, r0, v0
dp4 r1.x, r0, c2
dp4 oPos.w, r0, c3
mov oPos.z, r1.x
dp4 oPos.y, r0, c1
dp4 oPos.x, r0, c0
mov oFog, r1.x
mov oD0, c4
"
}

}
#LINE 249

                SetTexture [_MainTex] { combine primary }
            }
        }
       
        // SubShader with ONLY base pass - LOD 0 (no outline)
        SubShader {
            LOD 0
           
            Pass {
                Name "BASE"
                Cull Back
                ZWrite On
                ZTest LEqual
               
                BindChannels {
                    Bind "vertex", Vertex
                    Bind "normal", Normal
                    Bind "texcoord", TexCoord0
                }
                Lighting On
                Program "" {
// Vertex combos: 1
//   opengl - ALU: 47 to 47
//   d3d9 - ALU: 47 to 47
// Fragment combos: 1
//   opengl - ALU: 39 to 39, TEX: 4 to 4
//   d3d9 - ALU: 39 to 39, TEX: 4 to 4
SubProgram "opengl " {
Keywords { }
Bind "vertex", Vertex
Bind "normal", Normal
Bind "texcoord", TexCoord0
Local 1, ([_FatFactor],0,0,0)
Local 2, [_MainTex_ST]
"!!ARBvp1.0
# 47 ALU
PARAM c[17] = { { 1, 0, 0.0099999998, 0.99000001 },
		program.local[1..2],
		state.light[0].position,
		state.matrix.modelview[0],
		state.matrix.mvp,
		state.matrix.modelview[0].invtrans,
		{ 0.5 } };
TEMP R0;
TEMP R1;
TEMP R2;
TEMP R3;
MUL R0.xyz, vertex.normal, c[1].x;
ADD R1.xyz, vertex.position, R0;
MOV R1.w, vertex.position;
DP4 R0.w, R1, c[7];
DP4 R0.z, R1, c[6];
DP4 R0.y, R1, c[5];
DP4 R0.x, R1, c[4];
MOV R2.w, c[15];
MOV R2.z, c[14].w;
MOV R2.y, c[13].w;
MOV R2.x, c[12].w;
ADD R2, R2, -R0;
DP4 R2.w, R2, R2;
RSQ R2.w, R2.w;
MUL R3.xyz, R2.w, R2;
DP3 R0.z, vertex.normal, c[6];
DP3 R0.x, vertex.normal, c[4];
DP3 R0.y, vertex.normal, c[5];
DP3 R0.w, R0, R0;
RSQ R0.w, R0.w;
MUL R2.xyz, R0.w, R0;
DP3 R0.x, R2, R3;
ADD R0.z, -R0.x, c[0].x;
DP3 R0.x, R2, c[3];
DP3 R0.y, R3, -c[3];
MAD R0.y, R0.z, R0, R0.x;
MIN R0.y, R0, c[0].x;
MAX R0.y, R0, c[0];
ADD R0.w, R0.x, R0.y;
MAX R0.w, R0, c[0].z;
DP3 R0.x, R2, c[12];
DP3 R0.y, R2, c[13];
DP3 R0.z, R2, c[14];
DP3 R0.z, R0, R0;
RSQ R0.z, R0.z;
MUL R0.xy, R0.z, R0;
MAD result.texcoord[4].xy, R0, c[16].x, c[16].x;
DP4 R0.x, R1, c[10];
MIN result.texcoord[1].x, R0.w, c[0].w;
MOV result.texcoord[2].xyz, R3;
DP4 result.position.w, R1, c[11];
MOV result.position.z, R0.x;
DP4 result.position.y, R1, c[9];
DP4 result.position.x, R1, c[8];
MOV result.texcoord[3].xyz, R2;
MOV result.fogcoord.x, R0;
MAD result.texcoord[0].xy, vertex.texcoord[0], c[2], c[2].zwzw;
END
# 47 instructions, 4 R-regs
"
}

SubProgram "d3d9 " {
Keywords { }
Bind "vertex", Vertex
Bind "normal", Normal
Bind "texcoord", TexCoord0
Local 12, ([_FatFactor],0,0,0)
Local 13, [_MainTex_ST]
Local 14, [glstate_light0_position]
Matrix 0, [glstate_matrix_modelview0]
Matrix 4, [glstate_matrix_mvp]
Matrix 8, [glstate_matrix_invtrans_modelview0]
"vs_1_1
; 47 ALU
def c15, 1.00000000, 0.00000000, 0.01000000, 0.99000001
def c16, 0.50000000, 0, 0, 0
dcl_position v0
dcl_normal v1
dcl_texcoord0 v2
mul r0.xyz, v1, c12.x
add r1.xyz, v0, r0
mov r1.w, v0
dp4 r0.w, r1, c3
dp4 r0.z, r1, c2
dp4 r0.y, r1, c1
dp4 r0.x, r1, c0
mov r2.w, c11
mov r2.z, c10.w
mov r2.y, c9.w
mov r2.x, c8.w
add r2, r2, -r0
dp4 r2.w, r2, r2
rsq r2.w, r2.w
mul r3.xyz, r2.w, r2
dp3 r0.z, v1, c2
dp3 r0.x, v1, c0
dp3 r0.y, v1, c1
dp3 r0.w, r0, r0
rsq r0.w, r0.w
mul r2.xyz, r0.w, r0
dp3 r0.x, r2, r3
add r0.z, -r0.x, c15.x
dp3 r0.x, r2, c14
dp3 r0.y, r3, -c14
mad r0.y, r0.z, r0, r0.x
min r0.y, r0, c15.x
max r0.y, r0, c15
add r0.w, r0.x, r0.y
max r0.w, r0, c15.z
dp3 r0.x, r2, c8
dp3 r0.y, r2, c9
dp3 r0.z, r2, c10
dp3 r0.z, r0, r0
rsq r0.z, r0.z
mul r0.xy, r0.z, r0
mad oT4.xy, r0, c16.x, c16.x
dp4 r0.x, r1, c6
min oT1.x, r0.w, c15.w
mov oT2.xyz, r3
dp4 oPos.w, r1, c7
mov oPos.z, r0.x
dp4 oPos.y, r1, c5
dp4 oPos.x, r1, c4
mov oT3.xyz, r2
mov oFog, r0.x
mad oT0.xy, v2, c13, c13.zwzw
"
}

SubProgram "opengl " {
Keywords { }
Local 0, [_Color]
Local 1, ([_EmissionStrength],0,0,0)
Local 2, ([_RimPower],0,0,0)
Local 3, ([_RimIntensity],0,0,0)
Local 4, [_RimColor]
Local 5, ([_EmissionIntensity],0,0,0)
Local 6, ([_MatCapStrength],0,0,0)
Local 7, ([_MatCapBlendMode],0,0,0)
Local 8, ([_Transparency],0,0,0)
SetTexture [_MainTex] {2D}
SetTexture [_ShaderMap] {2D}
SetTexture [_MaskTex] {2D}
"!!ARBfp1.0
# 39 ALU, 4 TEX
PARAM c[10] = { program.local[0..8],
		{ 0.5, 2, 0, 1 } };
TEMP R0;
TEMP R1;
TEMP R2;
TEMP R3;
TEX R2.xyz, fragment.texcoord[0], texture[2], 2D;
TEX R2.w, fragment.texcoord[4], texture[2], 2D;
MOV R0.y, c[9].x;
MOV R0.x, fragment.texcoord[1];
TEX R1, R0, texture[1], 2D;
TEX R0, fragment.texcoord[0], texture[0], 2D;
MUL R3.xyz, R0, R2.y;
MUL R0, R0, c[0];
MUL R0, R0, R1;
MUL R0, R0, c[9].y;
MUL R1.xyz, R3, c[5].x;
MAD R0.xyz, R0, c[1].x, R1;
MOV R1.w, c[9].x;
MUL R1.x, R2.z, R2.w;
MAD R1.xyz, R1.x, c[6].x, R0;
ADD R2.y, -R1.w, c[7].x;
CMP R3.xyz, R2.y, R1, R0;
DP3 R0.y, fragment.texcoord[2], fragment.texcoord[2];
RSQ R0.y, R0.y;
DP3 R0.x, fragment.texcoord[3], fragment.texcoord[3];
MUL R1.xyz, R0.y, fragment.texcoord[2];
RSQ R0.x, R0.x;
MUL R0.xyz, R0.x, fragment.texcoord[3];
DP3 R0.x, R0, R1;
MAX R0.x, R0, c[9].z;
ADD R0.x, -R0, c[9].w;
MUL R0.z, R2, c[6].x;
ADD R0.y, R2.w, -c[9].w;
MAD R0.y, R0.z, R0, c[9].w;
MUL R1.xyz, R3, R0.y;
SLT R0.y, c[7].x, R1.w;
ABS R1.w, R0.y;
POW R0.x, R0.x, c[2].x;
MUL R0.xyz, R0.x, c[4];
CMP R1.w, -R1, c[9].z, c[9];
CMP R1.xyz, -R1.w, R1, R3;
MUL R0.xyz, R0, c[3].x;
MAD result.color.xyz, R0, R2.x, R1;
MUL result.color.w, R0, c[8].x;
END
# 39 instructions, 4 R-regs
"
}

SubProgram "d3d9 " {
Keywords { }
Local 0, [_Color]
Local 1, ([_EmissionStrength],0,0,0)
Local 2, ([_RimPower],0,0,0)
Local 3, ([_RimIntensity],0,0,0)
Local 4, [_RimColor]
Local 5, ([_EmissionIntensity],0,0,0)
Local 6, ([_MatCapStrength],0,0,0)
Local 7, ([_MatCapBlendMode],0,0,0)
Local 8, ([_Transparency],0,0,0)
SetTexture [_MainTex] {2D}
SetTexture [_ShaderMap] {2D}
SetTexture [_MaskTex] {2D}
"ps_2_0
; 39 ALU, 4 TEX
dcl_2d s0
dcl_2d s1
dcl_2d s2
def c9, -0.50000000, 0.00000000, 1.00000000, 0.50000000
def c10, 2.00000000, -1.00000000, 0, 0
dcl t0.xy
dcl t1.x
dcl t2.xyz
dcl t3.xyz
dcl t4.xy
texld r4, t4, s2
texld r2, t0, s2
texld r1, t0, s0
mul r3, r1, c0
mul r1.xyz, r1, r2.y
mul r1.xyz, r1, c5.x
mov r0.x, t1
mov r0.y, c9.w
texld r0, r0, s1
mul r0, r3, r0
mul r0, r0, c10.x
mad r3.xyz, r0, c1.x, r1
mul r0.x, r2.z, r4.w
mad r4.xyz, r0.x, c6.x, r3
dp3 r1.x, t3, t3
dp3 r0.x, t2, t2
rsq r0.x, r0.x
mul r5.xyz, r0.x, t2
rsq r1.x, r1.x
mul r1.xyz, r1.x, t3
mov r0.x, c7
add r0.x, c9, r0
cmp r3.xyz, r0.x, r3, r4
dp3 r1.x, r1, r5
max r0.x, r1, c9.y
mul r4.x, r2.z, c6
add r1.x, r4.w, c10.y
mad r1.x, r4, r1, c9.z
add r0.x, -r0, c9.z
mul r4.xyz, r3, r1.x
pow r1, r0.x, c2.x
mov r0.x, c7
add r0.x, c9, r0
cmp r0.x, r0, c9.y, c9.z
mul r1.xyz, r1.x, c4
abs_pp r0.x, r0
cmp r0.xyz, -r0.x, r4, r3
mul r1.xyz, r1, c3.x
mad r0.xyz, r1, r2.x, r0
mul r0.w, r0, c8.x
mov oC0, r0
"
}

}
#LINE 428

            }
        }
    }
   
    FallBack "Skin_DirLight_AmbLight_blendSrcalphaInvsrcalpha", 1
}