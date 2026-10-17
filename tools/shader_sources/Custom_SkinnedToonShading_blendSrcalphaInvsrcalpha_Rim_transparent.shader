Shader "Custom/SkinnedToonShading_blendSrcalphaInvsrcalpha_Rim_transparent" {
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
        _RimPower ("Rim Power", Float) = 10
        _RimIntensity ("Rim Intensity", Float) = 0.5
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
        Tags {"Queue"="Transparent+3"}
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
//   opengl - ALU: 43 to 43
//   d3d9 - ALU: 44 to 44
// Fragment combos: 1
//   opengl - ALU: 33 to 33, TEX: 4 to 4
//   d3d9 - ALU: 33 to 33, TEX: 4 to 4
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
# 43 ALU
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
ADD R1.xyz, vertex.position, R0;
MOV R1.w, vertex.position;
MOV R2.w, c[18];
MOV R2.z, c[17].w;
MOV R2.y, c[16].w;
MOV R2.x, c[15].w;
DP4 R0.w, R1, c[10];
DP4 R0.z, R1, c[9];
DP4 R0.y, R1, c[8];
DP4 R0.x, R1, c[7];
ADD R0, R2, -R0;
DP4 R2.w, R0, R0;
RSQ R2.w, R2.w;
MUL R0.xyz, R2.w, R0;
DP3 R2.z, vertex.normal, c[9];
DP3 R2.x, vertex.normal, c[7];
DP3 R2.y, vertex.normal, c[8];
DP3 R0.w, R2, R2;
RSQ R0.w, R0.w;
MUL R2.xyz, R0.w, R2;
DP3 R0.w, R2, R0;
ADD R3.x, -R0.w, c[0];
DP3 R0.w, R2, c[6];
DP3 R2.w, R0, -c[6];
MAD R2.w, R3.x, R2, R0;
MIN R2.w, R2, c[0].x;
MAX R2.w, R2, c[0].y;
ADD R0.w, R0, R2;
MAX R0.w, R0, c[0].z;
MOV result.texcoord[2].xyz, R0;
DP4 R0.x, R1, c[13];
MOV result.position.z, R0.x;
MOV result.fogcoord.x, R0;
MOV R0.x, c[1].y;
MIN result.texcoord[1].x, R0.w, c[0].w;
DP4 result.position.w, R1, c[14];
DP4 result.position.y, R1, c[12];
DP4 result.position.x, R1, c[11];
MOV result.texcoord[3].xyz, R2;
MAD result.texcoord[0].xy, vertex.texcoord[0], c[5], c[5].zwzw;
MAD result.texcoord[4].xy, R0.x, c[2], vertex.texcoord[0];
MAD result.texcoord[5].xy, R0.x, c[3], vertex.texcoord[0];
END
# 43 instructions, 4 R-regs
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
; 44 ALU
def c18, 1.00000000, 0.00000000, 0.01000000, 0.99000001
dcl_position v0
dcl_normal v1
dcl_texcoord0 v2
mul r0.xyz, v1, c15.x
add r1.xyz, v0, r0
mov r1.w, v0
mov r2.w, c11
mov r2.z, c10.w
mov r2.y, c9.w
mov r2.x, c8.w
dp4 r0.w, r1, c3
dp4 r0.z, r1, c2
dp4 r0.y, r1, c1
dp4 r0.x, r1, c0
add r0, r2, -r0
dp4 r2.w, r0, r0
rsq r2.w, r2.w
mul r0.xyz, r2.w, r0
dp3 r2.w, r0, -c17
mov oT2.xyz, r0
dp3 r2.z, v1, c2
dp3 r2.x, v1, c0
dp3 r2.y, v1, c1
dp3 r0.w, r2, r2
rsq r0.w, r0.w
mul r2.xyz, r0.w, r2
dp3 r0.w, r2, r0
dp4 r0.x, r1, c6
add r3.x, -r0.w, c18
dp3 r0.w, r2, c17
mad r2.w, r3.x, r2, r0
min r2.w, r2, c18.x
max r2.w, r2, c18.y
add r0.w, r0, r2
max r0.w, r0, c18.z
min oT1.x, r0.w, c18.w
mov r0.zw, c14.xyxy
mov oPos.z, r0.x
mov oFog, r0.x
mov r0.xy, c13
dp4 oPos.w, r1, c7
dp4 oPos.y, r1, c5
dp4 oPos.x, r1, c4
mov oT3.xyz, r2
mad oT0.xy, v2, c16, c16.zwzw
mad oT4.xy, c12.y, r0, v2
mad oT5.xy, c12.y, r0.zwzw, v2
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
# 33 ALU, 4 TEX
PARAM c[10] = { program.local[0..8],
		{ 0.5, 2, 0, 1 } };
TEMP R0;
TEMP R1;
TEMP R2;
TEMP R3;
TEMP R4;
TEMP R5;
TEX R3, fragment.texcoord[4], texture[2], 2D;
TEX R2, fragment.texcoord[5], texture[3], 2D;
DP3 R4.y, fragment.texcoord[2], fragment.texcoord[2];
RSQ R4.y, R4.y;
DP3 R4.x, fragment.texcoord[3], fragment.texcoord[3];
MOV R0.x, fragment.texcoord[1];
MOV R0.y, c[9].x;
MUL R5.xyz, R4.y, fragment.texcoord[2];
RSQ R4.x, R4.x;
MUL R4.xyz, R4.x, fragment.texcoord[3];
DP3 R4.x, R4, R5;
MAX R4.x, R4, c[9].z;
ADD R4.x, -R4, c[9].w;
POW R4.x, R4.x, c[2].x;
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
# 33 instructions, 6 R-regs
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
; 33 ALU, 4 TEX
dcl_2d s0
dcl_2d s1
dcl_2d s2
dcl_2d s3
def c9, 0.50000000, 2.00000000, -0.50000000, 0.00000000
def c10, 1.00000000, 0, 0, 0
dcl t0.xy
dcl t1.x
dcl t2.xyz
dcl t3.xyz
dcl t4.xy
dcl t5.xy
texld r1, t5, s3
texld r2, t4, s2
texld r4, t0, s0
dp3 r5.x, t2, t2
rsq r5.x, r5.x
mul r1, r1, c6
mov r0.x, t1
mov r0.y, c9.x
mul r5.xyz, r5.x, t2
mul r2, r2, c5
texld r3, r0, s1
dp3 r0.x, t3, t3
rsq r0.x, r0.x
mul r0.xyz, r0.x, t3
dp3 r0.x, r0, r5
max r0.x, r0, c9.w
add r0.x, -r0, c10
pow r5, r0.x, c2.x
mul r0, r4, c0
mul r3, r0, r3
mul r3, r3, c9.y
mov r0.x, r5
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
#LINE 195

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
#LINE 245

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
//   opengl - ALU: 43 to 43
//   d3d9 - ALU: 44 to 44
// Fragment combos: 1
//   opengl - ALU: 33 to 33, TEX: 4 to 4
//   d3d9 - ALU: 33 to 33, TEX: 4 to 4
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
# 43 ALU
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
ADD R1.xyz, vertex.position, R0;
MOV R1.w, vertex.position;
MOV R2.w, c[18];
MOV R2.z, c[17].w;
MOV R2.y, c[16].w;
MOV R2.x, c[15].w;
DP4 R0.w, R1, c[10];
DP4 R0.z, R1, c[9];
DP4 R0.y, R1, c[8];
DP4 R0.x, R1, c[7];
ADD R0, R2, -R0;
DP4 R2.w, R0, R0;
RSQ R2.w, R2.w;
MUL R0.xyz, R2.w, R0;
DP3 R2.z, vertex.normal, c[9];
DP3 R2.x, vertex.normal, c[7];
DP3 R2.y, vertex.normal, c[8];
DP3 R0.w, R2, R2;
RSQ R0.w, R0.w;
MUL R2.xyz, R0.w, R2;
DP3 R0.w, R2, R0;
ADD R3.x, -R0.w, c[0];
DP3 R0.w, R2, c[6];
DP3 R2.w, R0, -c[6];
MAD R2.w, R3.x, R2, R0;
MIN R2.w, R2, c[0].x;
MAX R2.w, R2, c[0].y;
ADD R0.w, R0, R2;
MAX R0.w, R0, c[0].z;
MOV result.texcoord[2].xyz, R0;
DP4 R0.x, R1, c[13];
MOV result.position.z, R0.x;
MOV result.fogcoord.x, R0;
MOV R0.x, c[1].y;
MIN result.texcoord[1].x, R0.w, c[0].w;
DP4 result.position.w, R1, c[14];
DP4 result.position.y, R1, c[12];
DP4 result.position.x, R1, c[11];
MOV result.texcoord[3].xyz, R2;
MAD result.texcoord[0].xy, vertex.texcoord[0], c[5], c[5].zwzw;
MAD result.texcoord[4].xy, R0.x, c[2], vertex.texcoord[0];
MAD result.texcoord[5].xy, R0.x, c[3], vertex.texcoord[0];
END
# 43 instructions, 4 R-regs
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
; 44 ALU
def c18, 1.00000000, 0.00000000, 0.01000000, 0.99000001
dcl_position v0
dcl_normal v1
dcl_texcoord0 v2
mul r0.xyz, v1, c15.x
add r1.xyz, v0, r0
mov r1.w, v0
mov r2.w, c11
mov r2.z, c10.w
mov r2.y, c9.w
mov r2.x, c8.w
dp4 r0.w, r1, c3
dp4 r0.z, r1, c2
dp4 r0.y, r1, c1
dp4 r0.x, r1, c0
add r0, r2, -r0
dp4 r2.w, r0, r0
rsq r2.w, r2.w
mul r0.xyz, r2.w, r0
dp3 r2.w, r0, -c17
mov oT2.xyz, r0
dp3 r2.z, v1, c2
dp3 r2.x, v1, c0
dp3 r2.y, v1, c1
dp3 r0.w, r2, r2
rsq r0.w, r0.w
mul r2.xyz, r0.w, r2
dp3 r0.w, r2, r0
dp4 r0.x, r1, c6
add r3.x, -r0.w, c18
dp3 r0.w, r2, c17
mad r2.w, r3.x, r2, r0
min r2.w, r2, c18.x
max r2.w, r2, c18.y
add r0.w, r0, r2
max r0.w, r0, c18.z
min oT1.x, r0.w, c18.w
mov r0.zw, c14.xyxy
mov oPos.z, r0.x
mov oFog, r0.x
mov r0.xy, c13
dp4 oPos.w, r1, c7
dp4 oPos.y, r1, c5
dp4 oPos.x, r1, c4
mov oT3.xyz, r2
mad oT0.xy, v2, c16, c16.zwzw
mad oT4.xy, c12.y, r0, v2
mad oT5.xy, c12.y, r0.zwzw, v2
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
# 33 ALU, 4 TEX
PARAM c[10] = { program.local[0..8],
		{ 0.5, 2, 0, 1 } };
TEMP R0;
TEMP R1;
TEMP R2;
TEMP R3;
TEMP R4;
TEMP R5;
TEX R3, fragment.texcoord[4], texture[2], 2D;
TEX R2, fragment.texcoord[5], texture[3], 2D;
DP3 R4.y, fragment.texcoord[2], fragment.texcoord[2];
RSQ R4.y, R4.y;
DP3 R4.x, fragment.texcoord[3], fragment.texcoord[3];
MOV R0.x, fragment.texcoord[1];
MOV R0.y, c[9].x;
MUL R5.xyz, R4.y, fragment.texcoord[2];
RSQ R4.x, R4.x;
MUL R4.xyz, R4.x, fragment.texcoord[3];
DP3 R4.x, R4, R5;
MAX R4.x, R4, c[9].z;
ADD R4.x, -R4, c[9].w;
POW R4.x, R4.x, c[2].x;
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
# 33 instructions, 6 R-regs
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
; 33 ALU, 4 TEX
dcl_2d s0
dcl_2d s1
dcl_2d s2
dcl_2d s3
def c9, 0.50000000, 2.00000000, -0.50000000, 0.00000000
def c10, 1.00000000, 0, 0, 0
dcl t0.xy
dcl t1.x
dcl t2.xyz
dcl t3.xyz
dcl t4.xy
dcl t5.xy
texld r1, t5, s3
texld r2, t4, s2
texld r4, t0, s0
dp3 r5.x, t2, t2
rsq r5.x, r5.x
mul r1, r1, c6
mov r0.x, t1
mov r0.y, c9.x
mul r5.xyz, r5.x, t2
mul r2, r2, c5
texld r3, r0, s1
dp3 r0.x, t3, t3
rsq r0.x, r0.x
mul r0.xyz, r0.x, t3
dp3 r0.x, r0, r5
max r0.x, r0, c9.w
add r0.x, -r0, c10
pow r5, r0.x, c2.x
mul r0, r4, c0
mul r3, r0, r3
mul r3, r3, c9.y
mov r0.x, r5
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
#LINE 414

            }
        }
    }
    
    FallBack "Skin_DirLight_AmbLight_blendSrcalphaInvsrcalpha", 1
}