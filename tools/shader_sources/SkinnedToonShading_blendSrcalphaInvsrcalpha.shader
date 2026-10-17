Shader "SkinnedToonShading_blendSrcalphaInvsrcalpha" {
    Properties {
        _Color ("Main Color", Color) = (1,1,1,1)
        _AmbColor ("Ambient Color", Color) = (1,1,1,1)
        _Emission ("Emmisive Color", Color) = (0,0,0,0)
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _Outline ("Outline width", Range (0, 0.03)) = .005
        _MainTex ("Base (RGB)", 2D) = "white" {}
        _SpecMap ("SpecRamp (RGB)", 2D) = "" {}
        _ShaderMap ("ToonRamp (RGB)", 2D) = "white" {}
        _RimPower ("Rim Power", Float) = 4
        _RimIntensity ("Rim Intensity", Float) = 2
    }

    Category {
        Tags { "Queue" = "Transparent-100" }
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask RGBA

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
//   opengl - ALU: 39 to 39
//   d3d9 - ALU: 39 to 39
// Fragment combos: 1
//   opengl - ALU: 22 to 22, TEX: 2 to 2
//   d3d9 - ALU: 24 to 24, TEX: 2 to 2
SubProgram "opengl " {
Keywords { }
Bind "vertex", Vertex
Bind "normal", Normal
Bind "texcoord", TexCoord0
Local 1, ([_FatFactor],0,0,0)
Local 2, [_MainTex_ST]
"!!ARBvp1.0
# 39 ALU
PARAM c[16] = { { 1, 0, 0.0099999998, 0.99000001 },
		program.local[1..2],
		state.light[0].position,
		state.matrix.modelview[0],
		state.matrix.mvp,
		state.matrix.modelview[0].invtrans };
TEMP R0;
TEMP R1;
TEMP R2;
TEMP R3;
MUL R0.xyz, vertex.normal, c[1].x;
ADD R1.xyz, R0, vertex.position;
MOV R1.w, vertex.position;
DP4 R0.z, R1, c[6];
DP4 R0.y, R1, c[5];
DP4 R0.x, R1, c[4];
MOV R2.z, c[14].w;
MOV R2.y, c[13].w;
MOV R2.x, c[12].w;
ADD R2.xyz, R2, -R0;
DP3 R2.w, R2, R2;
RSQ R2.w, R2.w;
MUL R2.xyz, R2.w, R2;
DP3 R0.z, vertex.normal, c[6];
DP3 R0.x, vertex.normal, c[4];
DP3 R0.y, vertex.normal, c[5];
DP3 R0.w, R0, R0;
RSQ R0.w, R0.w;
MUL R0.xyz, R0.w, R0;
DP3 R0.w, R0, R2;
ADD R2.w, -R0, c[0].x;
DP3 R0.w, R0, c[3];
DP3 R3.x, R2, -c[3];
MAD R2.w, R2, R3.x, R0;
MIN R2.w, R2, c[0].x;
MAX R2.w, R2, c[0].y;
ADD R2.w, R0, R2;
MIN R2.w, R2, c[0];
MOV result.texcoord[2].xyz, R2;
DP4 R2.x, R1, c[10];
MAX result.texcoord[1].x, R2.w, c[0].z;
DP4 result.position.w, R1, c[11];
MOV result.position.z, R2.x;
DP4 result.position.y, R1, c[9];
DP4 result.position.x, R1, c[8];
MOV result.texcoord[1].y, R0.w;
MOV result.texcoord[3].xyz, R0;
MOV result.fogcoord.x, R2;
MAD result.texcoord[0].xy, vertex.texcoord[0], c[2], c[2].zwzw;
END
# 39 instructions, 4 R-regs
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
; 39 ALU
def c15, 1.00000000, 0.00000000, 0.99000001, 0.01000000
dcl_position v0
dcl_normal v1
dcl_texcoord0 v2
mul r0.xyz, v1, c12.x
add r1.xyz, r0, v0
mov r1.w, v0
dp4 r0.z, r1, c2
dp4 r0.y, r1, c1
dp4 r0.x, r1, c0
mov r2.z, c10.w
mov r2.y, c9.w
mov r2.x, c8.w
add r2.xyz, r2, -r0
dp3 r2.w, r2, r2
rsq r2.w, r2.w
mul r2.xyz, r2.w, r2
dp3 r0.z, v1, c2
dp3 r0.x, v1, c0
dp3 r0.y, v1, c1
dp3 r0.w, r0, r0
rsq r0.w, r0.w
mul r0.xyz, r0.w, r0
dp3 r0.w, r0, r2
add r2.w, -r0, c15.x
dp3 r0.w, r0, c14
dp3 r3.x, r2, -c14
mad r2.w, r2, r3.x, r0
min r2.w, r2, c15.x
max r2.w, r2, c15.y
add r2.w, r0, r2
min r2.w, r2, c15.z
mov oT2.xyz, r2
dp4 r2.x, r1, c6
max oT1.x, r2.w, c15.w
dp4 oPos.w, r1, c7
mov oPos.z, r2.x
dp4 oPos.y, r1, c5
dp4 oPos.x, r1, c4
mov oT1.y, r0.w
mov oT3.xyz, r0
mov oFog, r2.x
mad oT0.xy, v2, c13, c13.zwzw
"
}

SubProgram "opengl " {
Keywords { }
Local 0, [_Color]
Local 1, ([_RimPower],0,0,0)
Local 2, ([_RimIntensity],0,0,0)
Local 3, [_RimColor]
SetTexture [_MainTex] {2D}
SetTexture [_ShaderMap] {2D}
"!!ARBfp1.0
# 22 ALU, 2 TEX
PARAM c[6] = { program.local[0..3],
		{ 0.5, 2, 0, 1 },
		{ 0.25 } };
TEMP R0;
TEMP R1;
TEMP R2;
TEMP R3;
DP3 R2.y, fragment.texcoord[2], fragment.texcoord[2];
RSQ R2.y, R2.y;
DP3 R2.x, fragment.texcoord[3], fragment.texcoord[3];
MOV R0.y, c[4].x;
MOV R0.x, fragment.texcoord[1];
MUL R3.xyz, R2.y, fragment.texcoord[2];
RSQ R2.x, R2.x;
MUL R2.xyz, R2.x, fragment.texcoord[3];
DP3 R2.x, R2, R3;
MAX R2.x, R2, c[4].z;
ADD R2.x, -R2, c[4].w;
POW R2.x, R2.x, c[1].x;
TEX R1, R0, texture[1], 2D;
TEX R0, fragment.texcoord[0], texture[0], 2D;
MUL R0, R0, c[0];
MUL R0, R0, R1;
MUL R0, R0, c[4].y;
MUL R1.xyz, R2.x, c[3];
MUL R1.xyz, R1, c[2].x;
SGE R1.w, fragment.texcoord[1].y, c[5].x;
MAD result.color.xyz, R1, R1.w, R0;
MOV result.color.w, R0;
END
# 22 instructions, 4 R-regs
"
}

SubProgram "d3d9 " {
Keywords { }
Local 0, [_Color]
Local 1, ([_RimPower],0,0,0)
Local 2, ([_RimIntensity],0,0,0)
Local 3, [_RimColor]
SetTexture [_MainTex] {2D}
SetTexture [_ShaderMap] {2D}
"ps_2_0
; 24 ALU, 2 TEX
dcl_2d s0
dcl_2d s1
def c4, 0.50000000, 2.00000000, 0.00000000, 1.00000000
def c5, -0.25000000, 0, 0, 0
dcl t0.xy
dcl t1.xy
dcl t2.xyz
dcl t3.xyz
texld r3, t0, s0
dp3 r1.x, t2, t2
rsq r1.x, r1.x
mov r0.y, c4.x
mov r0.x, t1
mul r1.xyz, r1.x, t2
texld r2, r0, s1
dp3 r0.x, t3, t3
rsq r0.x, r0.x
mul r0.xyz, r0.x, t3
dp3 r0.x, r0, r1
max r0.x, r0, c4.z
add r0.x, -r0, c4.w
mul r1, r3, c0
mul r2, r1, r2
pow r1, r0.x, c1.x
mul r2, r2, c4.y
add r0.x, t1.y, c5
mul r1.xyz, r1.x, c3
mov r0.w, r2
cmp r0.x, r0, c4.w, c4.z
mul r1.xyz, r1, c2.x
mad r0.xyz, r1, r0.x, r2
mov oC0, r0
"
}

}
#LINE 120

                SetTexture [_MainTex] { combine primary }
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
#LINE 168

                SetTexture [_MainTex] { combine primary }
            }
        }

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
//   opengl - ALU: 39 to 39
//   d3d9 - ALU: 39 to 39
// Fragment combos: 1
//   opengl - ALU: 22 to 22, TEX: 2 to 2
//   d3d9 - ALU: 24 to 24, TEX: 2 to 2
SubProgram "opengl " {
Keywords { }
Bind "vertex", Vertex
Bind "normal", Normal
Bind "texcoord", TexCoord0
Local 1, ([_FatFactor],0,0,0)
Local 2, [_MainTex_ST]
"!!ARBvp1.0
# 39 ALU
PARAM c[16] = { { 1, 0, 0.0099999998, 0.99000001 },
		program.local[1..2],
		state.light[0].position,
		state.matrix.modelview[0],
		state.matrix.mvp,
		state.matrix.modelview[0].invtrans };
TEMP R0;
TEMP R1;
TEMP R2;
TEMP R3;
MUL R0.xyz, vertex.normal, c[1].x;
ADD R1.xyz, R0, vertex.position;
MOV R1.w, vertex.position;
DP4 R0.z, R1, c[6];
DP4 R0.y, R1, c[5];
DP4 R0.x, R1, c[4];
MOV R2.z, c[14].w;
MOV R2.y, c[13].w;
MOV R2.x, c[12].w;
ADD R2.xyz, R2, -R0;
DP3 R2.w, R2, R2;
RSQ R2.w, R2.w;
MUL R2.xyz, R2.w, R2;
DP3 R0.z, vertex.normal, c[6];
DP3 R0.x, vertex.normal, c[4];
DP3 R0.y, vertex.normal, c[5];
DP3 R0.w, R0, R0;
RSQ R0.w, R0.w;
MUL R0.xyz, R0.w, R0;
DP3 R0.w, R0, R2;
ADD R2.w, -R0, c[0].x;
DP3 R0.w, R0, c[3];
DP3 R3.x, R2, -c[3];
MAD R2.w, R2, R3.x, R0;
MIN R2.w, R2, c[0].x;
MAX R2.w, R2, c[0].y;
ADD R2.w, R0, R2;
MIN R2.w, R2, c[0];
MOV result.texcoord[2].xyz, R2;
DP4 R2.x, R1, c[10];
MAX result.texcoord[1].x, R2.w, c[0].z;
DP4 result.position.w, R1, c[11];
MOV result.position.z, R2.x;
DP4 result.position.y, R1, c[9];
DP4 result.position.x, R1, c[8];
MOV result.texcoord[1].y, R0.w;
MOV result.texcoord[3].xyz, R0;
MOV result.fogcoord.x, R2;
MAD result.texcoord[0].xy, vertex.texcoord[0], c[2], c[2].zwzw;
END
# 39 instructions, 4 R-regs
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
; 39 ALU
def c15, 1.00000000, 0.00000000, 0.99000001, 0.01000000
dcl_position v0
dcl_normal v1
dcl_texcoord0 v2
mul r0.xyz, v1, c12.x
add r1.xyz, r0, v0
mov r1.w, v0
dp4 r0.z, r1, c2
dp4 r0.y, r1, c1
dp4 r0.x, r1, c0
mov r2.z, c10.w
mov r2.y, c9.w
mov r2.x, c8.w
add r2.xyz, r2, -r0
dp3 r2.w, r2, r2
rsq r2.w, r2.w
mul r2.xyz, r2.w, r2
dp3 r0.z, v1, c2
dp3 r0.x, v1, c0
dp3 r0.y, v1, c1
dp3 r0.w, r0, r0
rsq r0.w, r0.w
mul r0.xyz, r0.w, r0
dp3 r0.w, r0, r2
add r2.w, -r0, c15.x
dp3 r0.w, r0, c14
dp3 r3.x, r2, -c14
mad r2.w, r2, r3.x, r0
min r2.w, r2, c15.x
max r2.w, r2, c15.y
add r2.w, r0, r2
min r2.w, r2, c15.z
mov oT2.xyz, r2
dp4 r2.x, r1, c6
max oT1.x, r2.w, c15.w
dp4 oPos.w, r1, c7
mov oPos.z, r2.x
dp4 oPos.y, r1, c5
dp4 oPos.x, r1, c4
mov oT1.y, r0.w
mov oT3.xyz, r0
mov oFog, r2.x
mad oT0.xy, v2, c13, c13.zwzw
"
}

SubProgram "opengl " {
Keywords { }
Local 0, [_Color]
Local 1, ([_RimPower],0,0,0)
Local 2, ([_RimIntensity],0,0,0)
Local 3, [_RimColor]
SetTexture [_MainTex] {2D}
SetTexture [_ShaderMap] {2D}
"!!ARBfp1.0
# 22 ALU, 2 TEX
PARAM c[5] = { program.local[0..3],
		{ 0.5, 2, 0, 1 } };
TEMP R0;
TEMP R1;
TEMP R2;
TEMP R3;
DP3 R2.y, fragment.texcoord[2], fragment.texcoord[2];
RSQ R2.y, R2.y;
DP3 R2.x, fragment.texcoord[3], fragment.texcoord[3];
MOV R0.y, c[4].x;
MOV R0.x, fragment.texcoord[1];
MUL R3.xyz, R2.y, fragment.texcoord[2];
RSQ R2.x, R2.x;
MUL R2.xyz, R2.x, fragment.texcoord[3];
DP3 R2.x, R2, R3;
MAX R2.x, R2, c[4].z;
ADD R2.x, -R2, c[4].w;
POW R2.x, R2.x, c[1].x;
TEX R1, R0, texture[1], 2D;
TEX R0, fragment.texcoord[0], texture[0], 2D;
MUL R0, R0, c[0];
MUL R0, R0, R1;
MUL R0, R0, c[4].y;
MUL R1.xyz, R2.x, c[3];
MUL R1.xyz, R1, c[2].x;
SGE R1.w, fragment.texcoord[1].y, c[4].z;
MAD result.color.xyz, R1, R1.w, R0;
MOV result.color.w, R0;
END
# 22 instructions, 4 R-regs
"
}

SubProgram "d3d9 " {
Keywords { }
Local 0, [_Color]
Local 1, ([_RimPower],0,0,0)
Local 2, ([_RimIntensity],0,0,0)
Local 3, [_RimColor]
SetTexture [_MainTex] {2D}
SetTexture [_ShaderMap] {2D}
"ps_2_0
; 24 ALU, 2 TEX
dcl_2d s0
dcl_2d s1
def c4, 0.50000000, 2.00000000, 0.00000000, 1.00000000
dcl t0.xy
dcl t1.xy
dcl t2.xyz
dcl t3.xyz
texld r2, t0, s0
dp3 r3.x, t2, t2
rsq r3.x, r3.x
mov r0.y, c4.x
mov r0.x, t1
mul r3.xyz, r3.x, t2
texld r1, r0, s1
dp3 r0.x, t3, t3
rsq r0.x, r0.x
mul r0.xyz, r0.x, t3
dp3 r0.x, r0, r3
max r0.x, r0, c4.z
add r0.x, -r0, c4.w
pow r3, r0.x, c1.x
mul r0, r2, c0
mul r0, r0, r1
mul r1, r0, c4.y
mov r0.x, r3
mul r2.xyz, r0.x, c3
mov r0.w, r1
cmp r0.x, t1.y, c4.w, c4.z
mul r2.xyz, r2, c2.x
mad r0.xyz, r2, r0.x, r1
mov oC0, r0
"
}

}
#LINE 271

            }
        }
    }

    FallBack "Skin_DirLight_AmbLight_blendSrcalphaInvsrcalpha", 1
}
