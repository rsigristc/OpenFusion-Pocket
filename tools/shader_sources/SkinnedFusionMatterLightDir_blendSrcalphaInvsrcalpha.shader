Shader "SkinnedFusionMatterLightDir_blendSrcalphaInvsrcalpha"
{
    Properties
    {
        _Color ("Main Color", Color) = (0.6,0.6,0.6,1)
        _MainTex ("Combined (RGB: Fusion/Bubbles/Electricity)", 2D) = "black" {}
        _FusionTint ("Fusion Tint", Color) = (0,1,0,1)
        _BubblesTint ("Bubbles Tint", Color) = (1, 0.976, 0.208,1)
        _ElectricityTint ("Electricity Tint", Color) = (0.529,0.576,1,1)
        _RimColor ("Rim Color", Color) = (1, 0.976, 0.208, 1.0)
        _ShadowStrength ("Shadow Strength (Default: 0.65 - Unlit: 0.35)", Float) = 0.65
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _Outline ("Outline width", Float) = 0.005
    }

    Category
    {
        Tags {"Queue"="Transparent-100"}
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask RGBA

        SubShader
        {
            LOD 300

            Pass
            {
                Name "BASE"
                Cull Back
                ZWrite On
                ZTest LEqual

                BindChannels
                {
                    Bind "vertex", Vertex
                    Bind "normal", Normal
                    Bind "texcoord", TexCoord0
                }
                Lighting On

                Program "" {
// Vertex combos: 1
//   opengl - ALU: 31 to 31
//   d3d9 - ALU: 31 to 31
// Fragment combos: 1
//   opengl - ALU: 38 to 38, TEX: 3 to 3
//   d3d9 - ALU: 36 to 36, TEX: 3 to 3
SubProgram "opengl " {
Keywords { }
Bind "vertex", Vertex
Bind "normal", Normal
Bind "texcoord", TexCoord0
Local 1, [_Color]
Local 2, ([_ShadowStrength],0,0,0)
"!!ARBvp1.0
# 31 ALU
PARAM c[16] = { { 0, 1, 0.30000001 },
		program.local[1..2],
		state.light[0].position,
		state.matrix.modelview[0],
		state.matrix.mvp,
		state.matrix.modelview[0].invtrans };
TEMP R0;
TEMP R1;
TEMP R2;
DP3 R0.x, c[3], c[3];
DP3 R1.z, vertex.normal, c[14];
DP3 R1.x, vertex.normal, c[12];
DP3 R1.y, vertex.normal, c[13];
DP3 R0.y, R1, R1;
RSQ R0.w, R0.y;
MUL R1.xyz, R0.w, R1;
RSQ R0.x, R0.x;
MUL R0.xyz, R0.x, c[3];
DP3 R0.x, R1, R0;
MAX R0.x, R0, c[0];
ADD R0.x, R0, -c[0].y;
MUL R0.x, R0, c[2];
ADD R1.w, R0.x, c[0].y;
MUL R2.xyz, R1.w, c[1];
DP4 R0.x, vertex.position, c[4];
DP4 R0.z, vertex.position, c[6];
DP4 R0.y, vertex.position, c[5];
DP3 R0.w, -R0, -R0;
RSQ R0.w, R0.w;
MUL result.texcoord[1].xyz, R0.w, -R0;
DP4 R0.x, vertex.position, c[10];
ADD result.color.xyz, R2, c[0].z;
MOV result.texcoord[2].xyz, R1;
MOV result.texcoord[0].xy, vertex.texcoord[0];
DP4 result.position.w, vertex.position, c[11];
MOV result.position.z, R0.x;
DP4 result.position.y, vertex.position, c[9];
DP4 result.position.x, vertex.position, c[8];
MOV result.color.w, c[0].y;
MOV result.fogcoord.x, R0;
END
# 31 instructions, 3 R-regs
"
}

SubProgram "d3d9 " {
Keywords { }
Bind "vertex", Vertex
Bind "normal", Normal
Bind "texcoord", TexCoord0
Local 12, [_Color]
Local 13, ([_ShadowStrength],0,0,0)
Local 14, [glstate_light0_position]
Matrix 0, [glstate_matrix_modelview0]
Matrix 4, [glstate_matrix_mvp]
Matrix 8, [glstate_matrix_invtrans_modelview0]
"vs_1_1
; 31 ALU
def c15, 0.00000000, -1.00000000, 1.00000000, 0.30000001
dcl_position v0
dcl_normal v1
dcl_texcoord0 v2
dp3 r0.x, c14, c14
dp3 r1.z, v1, c10
dp3 r1.x, v1, c8
dp3 r1.y, v1, c9
dp3 r0.y, r1, r1
rsq r0.w, r0.y
mul r1.xyz, r0.w, r1
rsq r0.x, r0.x
mul r0.xyz, r0.x, c14
dp3 r0.x, r1, r0
max r0.x, r0, c15
add r0.x, r0, c15.y
mul r0.x, r0, c13
add r1.w, r0.x, c15.z
mul r2.xyz, r1.w, c12
dp4 r0.x, v0, c0
dp4 r0.z, v0, c2
dp4 r0.y, v0, c1
dp3 r0.w, -r0, -r0
rsq r0.w, r0.w
mul oT1.xyz, r0.w, -r0
dp4 r0.x, v0, c6
add oD0.xyz, r2, c15.w
mov oT2.xyz, r1
mov oT0.xy, v2
dp4 oPos.w, v0, c7
mov oPos.z, r0.x
dp4 oPos.y, v0, c5
dp4 oPos.x, v0, c4
mov oD0.w, c15.z
mov oFog, r0.x
"
}

SubProgram "opengl " {
Keywords { }
Local 0, [_Time]
Local 1, [_Color]
Local 2, [_FusionTint]
Local 3, [_BubblesTint]
Local 4, [_ElectricityTint]
Local 5, [_RimColor]
SetTexture [_MainTex] {2D}
"!!ARBfp1.0
# 38 ALU, 3 TEX
PARAM c[13] = { program.local[0..5],
		state.matrix.modelview[0].invtrans,
		{ 0.60000002, 0.45000002, 0.24000001, 0 },
		{ 0.25, -0.15000001, 0.5, 2 },
		{ 1.25, 1, -0.34999999, 0.64999998 } };
TEMP R0;
TEMP R1;
TEMP R2;
MOV R1.xy, c[11];
MUL R0.y, R1.x, c[0];
MOV R0.z, c[12];
MUL R0.w, R0.z, c[0].y;
MOV R0.x, c[10].w;
ADD R0.xy, fragment.texcoord[0], R0;
MOV R0.z, c[10].w;
DP3 R2.z, fragment.texcoord[2], c[8];
DP3 R2.x, fragment.texcoord[2], c[6];
ADD R0.zw, fragment.texcoord[0].xyxy, R0;
MUL R1.y, R1, c[0];
MOV R1.x, c[10].w;
ADD R1.xy, fragment.texcoord[0], R1;
MOV result.color.w, c[1];
TEX R0.xy, R0, texture[0], 2D;
MAD R0.zw, R0.xyxy, c[12].w, R0;
TEX R0.y, R1, texture[0], 2D;
TEX R0.z, R0.zwzw, texture[0], 2D;
MUL R1.xyz, R0.y, c[3];
DP3 R0.y, fragment.texcoord[2], c[7];
MOV R2.y, R0;
DP3 R0.w, R2, R2;
MUL R1.xyz, R1, c[11].z;
MAD R1.xyz, R0.x, c[2], R1;
RSQ R0.x, R0.w;
MUL R0.x, R0, R0.y;
MOV R2.xyz, fragment.texcoord[1];
MUL R0.w, R0.x, c[11];
DP3_SAT R0.y, fragment.texcoord[2], R2;
ADD R0.x, -R0.y, c[12].y;
ADD_SAT R0.y, R0.w, -c[10].x;
MAD R1.xyz, R0.y, c[10], R1;
MUL R0.w, R0.x, R0.x;
MUL R0.xyz, R0.z, c[4];
MUL R0.w, R0, c[5];
MAD R0.xyz, R0.w, c[5], R0;
MUL R1.xyz, R1, fragment.color.primary;
MAD result.color.xyz, R1, c[12].x, R0;
END
# 38 instructions, 3 R-regs
"
}

SubProgram "d3d9 " {
Keywords { }
Local 4, [_Time]
Local 5, [_Color]
Local 6, [_FusionTint]
Local 7, [_BubblesTint]
Local 8, [_ElectricityTint]
Local 9, [_RimColor]
Matrix 0, [glstate_matrix_invtrans_modelview0]
SetTexture [_MainTex] {2D}
"ps_2_0
; 36 ALU, 3 TEX
dcl_2d s0
def c10, 0.25000000, 0.00000000, -0.15000001, -0.34999999
def c11, 0.64999998, 0.50000000, 2.00000000, -0.60000002
def c12, 0.60000002, 0.45000002, 0.24000001, 1.00000000
def c13, 1.25000000, 0, 0, 0
dcl t0.xy
dcl t1.xyz
dcl t2.xyz
dcl v0.xyz
mov r0.y, c4
mov r1.y, c4
mul r1.y, c10.w, r1
mov r1.x, c10.y
add r2.xy, t0, r1
mul r0.y, c10.x, r0
mov r0.x, c10.y
add r0.xy, t0, r0
texld r3, r0, s0
mov r0.y, c4
mul r0.y, c10.z, r0
mov r0.x, c10.y
add r1.xy, t0, r0
mad r0.xy, r3, c11.x, r2
texld r0, r0, s0
texld r1, r1, s0
mul r2.xyz, r1.y, c7
dp3 r0.x, t2, c1
mul r2.xyz, r2, c11.y
mov r1.y, r0.x
dp3 r1.z, t2, c2
dp3 r1.x, t2, c0
dp3 r1.x, r1, r1
rsq r1.x, r1.x
mul r0.y, r1.x, r0.x
mad_sat r0.x, r0.y, c11.z, c11.w
mad r2.xyz, r3.x, c6, r2
mad r2.xyz, r0.x, c12, r2
mov r1.xyz, t1
dp3_sat r0.x, t2, r1
add r0.x, -r0, c12.w
mul r1.xyz, r2, v0
mul r0.x, r0, r0
mul r2.xyz, r0.z, c8
mul r0.x, r0, c9.w
mad r0.xyz, r0.x, c9, r2
mov r0.w, c5
mad r0.xyz, r1, c13.x, r0
mov oC0, r0
"
}

}
#LINE 156

            }

            // ==================== OUTLINE PASS ====================
            Pass
            {
                Name "OUTLINE"
                Tags { "LightMode" = "Always" }
                Cull Front
                ZWrite On
                ColorMask RGBA
                Blend SrcAlpha OneMinusSrcAlpha

                BindChannels
                {
                    Bind "vertex", Vertex
                    Bind "normal", Normal
                }

                Program "" {
// Vertex combos: 1
//   opengl - ALU: 8 to 8
//   d3d9 - ALU: 8 to 8
// Fragment combos: 1
//   opengl - ALU: 1 to 1, TEX: 0 to 0
//   d3d9 - ALU: 1 to 1
SubProgram "opengl " {
Keywords { }
Bind "vertex", Vertex
Bind "normal", Normal
Local 1, [_OutlineColor]
Local 2, ([_Outline],0,0,0)
"!!ARBvp1.0
# 8 ALU
PARAM c[7] = { program.local[0..2],
		state.matrix.mvp };
TEMP R0;
MUL R0.xyz, vertex.normal, c[2].x;
MOV R0.w, vertex.position;
ADD R0.xyz, R0, vertex.position;
DP4 result.position.w, R0, c[6];
DP4 result.position.z, R0, c[5];
DP4 result.position.y, R0, c[4];
DP4 result.position.x, R0, c[3];
MOV result.color, c[1];
END
# 8 instructions, 1 R-regs
"
}

SubProgram "d3d9 " {
Keywords { }
Bind "vertex", Vertex
Bind "normal", Normal
Local 4, [_OutlineColor]
Local 5, ([_Outline],0,0,0)
Matrix 0, [glstate_matrix_mvp]
"vs_1_1
; 8 ALU
dcl_position v0
dcl_normal v1
mul r0.xyz, v1, c5.x
mov r0.w, v0
add r0.xyz, r0, v0
dp4 oPos.w, r0, c3
dp4 oPos.z, r0, c2
dp4 oPos.y, r0, c1
dp4 oPos.x, r0, c0
mov oD0, c4
"
}

SubProgram "opengl " {
Keywords { }
"!!ARBfp1.0
# 1 ALU, 0 TEX
MOV result.color, fragment.color.primary;
END
# 1 instructions, 0 R-regs
"
}

SubProgram "d3d9 " {
Keywords { }
"ps_2_0
; 1 ALU
dcl v0
mov oC0, v0
"
}

}
#LINE 209

            }
        }

        // Fallback (same logic, LOD 0)
        SubShader
        {
            LOD 0
            Pass
            {
                Name "BASE"
                Cull Back
                ZWrite On
                ZTest LEqual
                BindChannels
                {
                    Bind "vertex", Vertex
                    Bind "normal", Normal
                    Bind "texcoord", TexCoord0
                }
                Lighting On

                Program "" {
// Vertex combos: 1
//   opengl - ALU: 31 to 31
//   d3d9 - ALU: 31 to 31
// Fragment combos: 1
//   opengl - ALU: 38 to 38, TEX: 3 to 3
//   d3d9 - ALU: 36 to 36, TEX: 3 to 3
SubProgram "opengl " {
Keywords { }
Bind "vertex", Vertex
Bind "normal", Normal
Bind "texcoord", TexCoord0
Local 1, [_Color]
Local 2, ([_ShadowStrength],0,0,0)
"!!ARBvp1.0
# 31 ALU
PARAM c[16] = { { 0, 1, 0.30000001 },
		program.local[1..2],
		state.light[0].position,
		state.matrix.modelview[0],
		state.matrix.mvp,
		state.matrix.modelview[0].invtrans };
TEMP R0;
TEMP R1;
TEMP R2;
DP3 R0.x, c[3], c[3];
DP3 R1.z, vertex.normal, c[14];
DP3 R1.x, vertex.normal, c[12];
DP3 R1.y, vertex.normal, c[13];
DP3 R0.y, R1, R1;
RSQ R0.w, R0.y;
MUL R1.xyz, R0.w, R1;
RSQ R0.x, R0.x;
MUL R0.xyz, R0.x, c[3];
DP3 R0.x, R1, R0;
MAX R0.x, R0, c[0];
ADD R0.x, R0, -c[0].y;
MUL R0.x, R0, c[2];
ADD R1.w, R0.x, c[0].y;
MUL R2.xyz, R1.w, c[1];
DP4 R0.x, vertex.position, c[4];
DP4 R0.z, vertex.position, c[6];
DP4 R0.y, vertex.position, c[5];
DP3 R0.w, -R0, -R0;
RSQ R0.w, R0.w;
MUL result.texcoord[1].xyz, R0.w, -R0;
DP4 R0.x, vertex.position, c[10];
ADD result.color.xyz, R2, c[0].z;
MOV result.texcoord[2].xyz, R1;
MOV result.texcoord[0].xy, vertex.texcoord[0];
DP4 result.position.w, vertex.position, c[11];
MOV result.position.z, R0.x;
DP4 result.position.y, vertex.position, c[9];
DP4 result.position.x, vertex.position, c[8];
MOV result.color.w, c[0].y;
MOV result.fogcoord.x, R0;
END
# 31 instructions, 3 R-regs
"
}

SubProgram "d3d9 " {
Keywords { }
Bind "vertex", Vertex
Bind "normal", Normal
Bind "texcoord", TexCoord0
Local 12, [_Color]
Local 13, ([_ShadowStrength],0,0,0)
Local 14, [glstate_light0_position]
Matrix 0, [glstate_matrix_modelview0]
Matrix 4, [glstate_matrix_mvp]
Matrix 8, [glstate_matrix_invtrans_modelview0]
"vs_1_1
; 31 ALU
def c15, 0.00000000, -1.00000000, 1.00000000, 0.30000001
dcl_position v0
dcl_normal v1
dcl_texcoord0 v2
dp3 r0.x, c14, c14
dp3 r1.z, v1, c10
dp3 r1.x, v1, c8
dp3 r1.y, v1, c9
dp3 r0.y, r1, r1
rsq r0.w, r0.y
mul r1.xyz, r0.w, r1
rsq r0.x, r0.x
mul r0.xyz, r0.x, c14
dp3 r0.x, r1, r0
max r0.x, r0, c15
add r0.x, r0, c15.y
mul r0.x, r0, c13
add r1.w, r0.x, c15.z
mul r2.xyz, r1.w, c12
dp4 r0.x, v0, c0
dp4 r0.z, v0, c2
dp4 r0.y, v0, c1
dp3 r0.w, -r0, -r0
rsq r0.w, r0.w
mul oT1.xyz, r0.w, -r0
dp4 r0.x, v0, c6
add oD0.xyz, r2, c15.w
mov oT2.xyz, r1
mov oT0.xy, v2
dp4 oPos.w, v0, c7
mov oPos.z, r0.x
dp4 oPos.y, v0, c5
dp4 oPos.x, v0, c4
mov oD0.w, c15.z
mov oFog, r0.x
"
}

SubProgram "opengl " {
Keywords { }
Local 0, [_Time]
Local 1, [_FusionTint]
Local 2, [_BubblesTint]
Local 3, [_ElectricityTint]
Local 4, [_RimColor]
SetTexture [_MainTex] {2D}
"!!ARBfp1.0
# 38 ALU, 3 TEX
PARAM c[12] = { program.local[0..4],
		state.matrix.modelview[0].invtrans,
		{ 0.60000002, 0.45000002, 0.24000001, 0 },
		{ 0.25, -0.15000001, 0.5, 2 },
		{ 1.25, 1, -0.34999999, 0.64999998 } };
TEMP R0;
TEMP R1;
TEMP R2;
MOV R1.xy, c[10];
MUL R0.y, R1.x, c[0];
MOV R0.z, c[11];
MUL R0.w, R0.z, c[0].y;
MOV R0.x, c[9].w;
ADD R0.xy, fragment.texcoord[0], R0;
MOV R0.z, c[9].w;
DP3 R2.z, fragment.texcoord[2], c[7];
DP3 R2.x, fragment.texcoord[2], c[5];
ADD R0.zw, fragment.texcoord[0].xyxy, R0;
MUL R1.y, R1, c[0];
MOV R1.x, c[9].w;
ADD R1.xy, fragment.texcoord[0], R1;
MOV result.color.w, c[11].y;
TEX R0.xy, R0, texture[0], 2D;
MAD R0.zw, R0.xyxy, c[11].w, R0;
TEX R0.y, R1, texture[0], 2D;
TEX R0.z, R0.zwzw, texture[0], 2D;
MUL R1.xyz, R0.y, c[2];
DP3 R0.y, fragment.texcoord[2], c[6];
MOV R2.y, R0;
DP3 R0.w, R2, R2;
MUL R1.xyz, R1, c[10].z;
MAD R1.xyz, R0.x, c[1], R1;
RSQ R0.x, R0.w;
MUL R0.x, R0, R0.y;
MOV R2.xyz, fragment.texcoord[1];
MUL R0.w, R0.x, c[10];
DP3_SAT R0.y, fragment.texcoord[2], R2;
ADD R0.x, -R0.y, c[11].y;
ADD_SAT R0.y, R0.w, -c[9].x;
MAD R1.xyz, R0.y, c[9], R1;
MUL R0.w, R0.x, R0.x;
MUL R0.xyz, R0.z, c[3];
MUL R0.w, R0, c[4];
MAD R0.xyz, R0.w, c[4], R0;
MUL R1.xyz, R1, fragment.color.primary;
MAD result.color.xyz, R1, c[11].x, R0;
END
# 38 instructions, 3 R-regs
"
}

SubProgram "d3d9 " {
Keywords { }
Local 4, [_Time]
Local 5, [_FusionTint]
Local 6, [_BubblesTint]
Local 7, [_ElectricityTint]
Local 8, [_RimColor]
Matrix 0, [glstate_matrix_invtrans_modelview0]
SetTexture [_MainTex] {2D}
"ps_2_0
; 36 ALU, 3 TEX
dcl_2d s0
def c9, 0.25000000, 0.00000000, -0.15000001, -0.34999999
def c10, 0.64999998, 0.50000000, 2.00000000, -0.60000002
def c11, 0.60000002, 0.45000002, 0.24000001, 1.00000000
def c12, 1.25000000, 0, 0, 0
dcl t0.xy
dcl t1.xyz
dcl t2.xyz
dcl v0.xyz
mov r0.y, c4
mov r1.y, c4
mul r1.y, c9.w, r1
mov r1.x, c9.y
add r2.xy, t0, r1
mul r0.y, c9.x, r0
mov r0.x, c9.y
add r0.xy, t0, r0
texld r3, r0, s0
mov r0.y, c4
mul r0.y, c9.z, r0
mov r0.x, c9.y
add r1.xy, t0, r0
mad r0.xy, r3, c10.x, r2
texld r0, r0, s0
texld r1, r1, s0
mul r2.xyz, r1.y, c6
dp3 r0.x, t2, c1
mul r2.xyz, r2, c10.y
mov r1.y, r0.x
dp3 r1.z, t2, c2
dp3 r1.x, t2, c0
dp3 r1.x, r1, r1
rsq r1.x, r1.x
mul r0.y, r1.x, r0.x
mad_sat r0.x, r0.y, c10.z, c10.w
mad r2.xyz, r3.x, c5, r2
mad r2.xyz, r0.x, c11, r2
mov r1.xyz, t1
dp3_sat r0.x, t2, r1
add r0.x, -r0, c11.w
mul r1.xyz, r2, v0
mul r0.x, r0, r0
mul r2.xyz, r0.z, c7
mul r0.x, r0, c8.w
mad r0.xyz, r0.x, c8, r2
mov r0.w, c11
mad r0.xyz, r1, c12.x, r0
mov oC0, r0
"
}

}
#LINE 322

            }
        }
    }
    FallBack "Diffuse"
}