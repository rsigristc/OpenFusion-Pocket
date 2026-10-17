Shader "Custom/VFX/Unique/HologramSolid_Additive" {
    Properties {
        _MaskTex ("Packed Mask (RGB+A)", 2D) = "white" {}
        _ColorA ("Color A (Alpha)", Color) = (1.0, 0.859, 0.067, 1)
        _ColorB ("Color B (Inverted Alpha)", Color) = (0.871, 0.588, 0.063, 1)
        _MasterOpacity ("Master Opacity", Float) = 1.0
        _BaseOpacity ("Base Opacity", Float) = 0.85
        _MaskAlphaOpacity ("Mask Alpha Opacity", Float) = 0.65

        _OverlayRColor ("Overlay R Color", Color) = (1, 0.235, 0, 1)
        _OverlayROpacity ("Overlay R Opacity", Float) = 1.0

        _OverlayGColor ("Overlay G Color", Color) = (0.647, 0.529, 0.529, 1)
        _OverlayGOpacity ("Overlay G Opacity", Float) = 0.25

        _OverlayBColor ("Overlay B Color", Color) = (0.569, 0.353, 0.055, 1)
        _OverlayBOpacity ("Overlay B Opacity", Float) = 0.6
    }

    Category {
        Tags {"Queue"="Transparent-100"}
        Cull Off
        ColorMask RGBA

        SubShader {
            LOD 200

            // Pass 1 - Base transparent
            Pass {
                Name "BASE"
                Blend SrcAlpha OneMinusSrcAlpha

                BindChannels {
                    Bind "vertex", Vertex
                    Bind "texcoord", TexCoord0
                }

                Program "" {
// Vertex combos: 1
//   opengl - ALU: 6 to 6
//   d3d9 - ALU: 6 to 6
// Fragment combos: 1
//   opengl - ALU: 7 to 7, TEX: 1 to 1
//   d3d9 - ALU: 7 to 7, TEX: 1 to 1
SubProgram "opengl " {
Keywords { }
Bind "vertex", Vertex
Bind "texcoord", TexCoord0
Bind "color", Color
Local 1, [_MaskTex_ST]
"!!ARBvp1.0
# 6 ALU
PARAM c[6] = { program.local[0..1],
		state.matrix.mvp };
MOV result.color, vertex.color;
MAD result.texcoord[0].xy, vertex.texcoord[0], c[1], c[1].zwzw;
DP4 result.position.w, vertex.position, c[5];
DP4 result.position.z, vertex.position, c[4];
DP4 result.position.y, vertex.position, c[3];
DP4 result.position.x, vertex.position, c[2];
END
# 6 instructions, 0 R-regs
"
}

SubProgram "d3d9 " {
Keywords { }
Bind "vertex", Vertex
Bind "texcoord", TexCoord0
Bind "color", Color
Local 4, [_MaskTex_ST]
Matrix 0, [glstate_matrix_mvp]
"vs_1_1
; 6 ALU
dcl_position v0
dcl_texcoord0 v1
dcl_color v2
mov oD0, v2
mad oT0.xy, v1, c4, c4.zwzw
dp4 oPos.w, v0, c3
dp4 oPos.z, v0, c2
dp4 oPos.y, v0, c1
dp4 oPos.x, v0, c0
"
}

SubProgram "opengl " {
Keywords { }
Local 0, [_ColorA]
Local 1, [_ColorB]
Local 2, ([_MasterOpacity],0,0,0)
Local 3, ([_BaseOpacity],0,0,0)
SetTexture [_MaskTex] {2D}
"!!ARBfp1.0
# 7 ALU, 1 TEX
PARAM c[5] = { program.local[0..3],
		{ 1 } };
TEMP R0;
TEMP R1;
TEX R0.w, fragment.texcoord[0], texture[0], 2D;
ADD R0.x, -R0.w, c[4];
MOV R1.x, c[2];
MUL R0.xyz, R0.x, c[1];
MUL R1.x, R1, c[3];
MAD result.color.xyz, R0.w, c[0], R0;
MUL result.color.w, R1.x, fragment.color.primary;
END
# 7 instructions, 2 R-regs
"
}

SubProgram "d3d9 " {
Keywords { }
Local 0, [_ColorA]
Local 1, [_ColorB]
Local 2, ([_MasterOpacity],0,0,0)
Local 3, ([_BaseOpacity],0,0,0)
SetTexture [_MaskTex] {2D}
"ps_2_0
; 7 ALU, 1 TEX
dcl_2d s0
def c4, 1.00000000, 0, 0, 0
dcl t0.xy
dcl v0.xyzw
texld r0, t0, s0
add r1.x, -r0.w, c4
mov r0.x, c3
mul r1.xyz, r1.x, c1
mul r0.x, c2, r0
mad r1.xyz, r0.w, c0, r1
mul r1.w, r0.x, v0
mov oC0, r1
"
}

}
#LINE 90

            }

            // Pass 2 - Additive overlays
            Pass {
                Name "OVERLAYS"
                Blend SrcAlpha One

                BindChannels {
                    Bind "vertex", Vertex
                    Bind "texcoord", TexCoord0
                }

                Program "" {
// Vertex combos: 1
//   opengl - ALU: 6 to 6
//   d3d9 - ALU: 6 to 6
// Fragment combos: 1
//   opengl - ALU: 31 to 31, TEX: 4 to 4
//   d3d9 - ALU: 28 to 28, TEX: 4 to 4
SubProgram "opengl " {
Keywords { }
Bind "vertex", Vertex
Bind "texcoord", TexCoord0
Bind "color", Color
Local 1, [_MaskTex_ST]
"!!ARBvp1.0
# 6 ALU
PARAM c[6] = { program.local[0..1],
		state.matrix.mvp };
MOV result.color, vertex.color;
MAD result.texcoord[0].xy, vertex.texcoord[0], c[1], c[1].zwzw;
DP4 result.position.w, vertex.position, c[5];
DP4 result.position.z, vertex.position, c[4];
DP4 result.position.y, vertex.position, c[3];
DP4 result.position.x, vertex.position, c[2];
END
# 6 instructions, 0 R-regs
"
}

SubProgram "d3d9 " {
Keywords { }
Bind "vertex", Vertex
Bind "texcoord", TexCoord0
Bind "color", Color
Local 4, [_MaskTex_ST]
Matrix 0, [glstate_matrix_mvp]
"vs_1_1
; 6 ALU
dcl_position v0
dcl_texcoord0 v1
dcl_color v2
mov oD0, v2
mad oT0.xy, v1, c4, c4.zwzw
dp4 oPos.w, v0, c3
dp4 oPos.z, v0, c2
dp4 oPos.y, v0, c1
dp4 oPos.x, v0, c0
"
}

SubProgram "opengl " {
Keywords { }
Local 0, [_Time]
Local 1, [_ColorA]
Local 2, ([_MasterOpacity],0,0,0)
Local 3, ([_MaskAlphaOpacity],0,0,0)
Local 4, [_OverlayRColor]
Local 5, ([_OverlayROpacity],0,0,0)
Local 6, [_OverlayGColor]
Local 7, ([_OverlayGOpacity],0,0,0)
Local 8, [_OverlayBColor]
Local 9, ([_OverlayBOpacity],0,0,0)
SetTexture [_MaskTex] {2D}
"!!ARBfp1.0
# 31 ALU, 4 TEX
PARAM c[12] = { program.local[0..9],
		{ 1, 0.64999998, 0.1, 1.25 },
		{ 0.2, 0.34999999, 0.69999999 } };
TEMP R0;
TEMP R1;
TEMP R2;
TEMP R3;
MOV R1.xyz, c[11];
MUL R0.y, R1.x, c[0];
MOV R0.x, c[10].z;
MUL R0.w, R0.x, c[0].y;
MAD R0.z, fragment.texcoord[0].x, c[10].w, R0.y;
MAD R0.xy, R1.yzzw, c[0].y, fragment.texcoord[0];
MAD R1.x, fragment.texcoord[0], c[10].y, R0.w;
MOV R1.y, fragment.texcoord[0];
MOV R0.w, fragment.texcoord[0].y;
TEX R0.y, R0, texture[0], 2D;
TEX R0.x, R0.zwzw, texture[0], 2D;
TEX R0.w, fragment.texcoord[0], texture[0], 2D;
TEX R0.z, R1, texture[0], 2D;
MAD R1, R0.w, c[1], -R0.w;
ADD R1, R1, c[10].x;
MUL R2, R1, c[4];
MUL R3, R1, c[6];
MUL R2, R2, R0.x;
MUL R3, R3, R0.y;
ADD R0.x, R0.w, -c[10];
MUL R3, R3, c[7].x;
MUL R0.x, R0, c[3];
MAD R2, R2, c[5].x, R3;
ADD R0.x, R0, c[10];
MUL R1, R1, c[8];
MUL R3.x, R0, c[2];
MUL R0, R1, R0.z;
MAD R0, R0, c[9].x, R2;
MUL R1.x, R3, fragment.color.primary.w;
MUL result.color.w, R0, R1.x;
MOV result.color.xyz, R0;
END
# 31 instructions, 4 R-regs
"
}

SubProgram "d3d9 " {
Keywords { }
Local 0, [_Time]
Local 1, [_ColorA]
Local 2, ([_MasterOpacity],0,0,0)
Local 3, ([_MaskAlphaOpacity],0,0,0)
Local 4, [_OverlayRColor]
Local 5, ([_OverlayROpacity],0,0,0)
Local 6, [_OverlayGColor]
Local 7, ([_OverlayGOpacity],0,0,0)
Local 8, [_OverlayBColor]
Local 9, ([_OverlayBOpacity],0,0,0)
SetTexture [_MaskTex] {2D}
"ps_2_0
; 28 ALU, 4 TEX
dcl_2d s0
def c10, 1.00000000, 0.10000000, 0.64999998, 0.20000000
def c11, 1.25000000, 0.34999999, 0.69999999, -1.00000000
dcl t0.xy
dcl v0.xyzw
texld r5, t0, s0
mov r1.y, c0
mul r0.x, c10.w, r1.y
mov r0.y, c0
mad r2.xy, c11.yzxw, r0.y, t0
mov r1.y, c0
mul r1.x, c10.y, r1.y
mad r0.x, t0, c11, r0
mov r0.y, t0
mov r1.y, t0
mad r1.x, t0, c10.z, r1
texld r3, r2, s0
texld r2, r0, s0
texld r4, r1, s0
mad r0, r5.w, c1, -r5.w
add r1, r0, c10.x
mul r0, r1, c4
mul r2, r0, r2.x
mul r0, r1, c6
mul r3, r0, r3.y
add r0.x, r5.w, c11.w
mul r3, r3, c7.x
mul r0.x, r0, c3
add r0.x, r0, c10
mul r0.x, r0, c2
mad r2, r2, c5.x, r3
mul r1, r1, c8
mul r1, r1, r4.z
mad r1, r1, c9.x, r2
mul r0.x, r0, v0.w
mul r1.w, r1, r0.x
mov oC0, r1
"
}

}
#LINE 171

            }
        }

        SubShader {
            LOD 100
            Pass {
                SetTexture [_MaskTex] {
                    Combine texture
                }
            }
        }
    }

    FallBack "Diffuse"
}