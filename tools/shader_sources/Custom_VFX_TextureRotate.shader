Shader "Custom/VFX/TextureRotate" {
    Properties {
        _MainTex ("Base Texture", 2D) = "white" {}
        _Color ("Main Color", Color) = (1,1,1,1)
        _RotationSpeed ("Rotation Speed", Float) = 1.0
        _CenterX ("Center X", Float) = 0.5
        _CenterY ("Center Y", Float) = 0.5
        _VignetteIntensity ("Vignette Intensity", Float) = 0.0
        _VignettePower ("Vignette Power", Float) = 2.0
    }
    
    Category {
        Tags {"Queue"="Transparent+3" "RenderType"="Transparent+3"}
        Blend SrcAlpha OneMinusSrcAlpha
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
//   opengl - ALU: 6 to 6
//   d3d9 - ALU: 6 to 6
// Fragment combos: 1
//   opengl - ALU: 27 to 27, TEX: 1 to 1
//   d3d9 - ALU: 39 to 39, TEX: 1 to 1
SubProgram "opengl " {
Keywords { }
Bind "vertex", Vertex
Bind "texcoord", TexCoord0
Bind "color", Color
Local 1, [_MainTex_ST]
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
Local 4, [_MainTex_ST]
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
Local 1, [_Color]
Local 2, ([_RotationSpeed],0,0,0)
Local 3, ([_CenterX],0,0,0)
Local 4, ([_CenterY],0,0,0)
Local 5, ([_VignetteIntensity],0,0,0)
Local 6, ([_VignettePower],0,0,0)
SetTexture [_MainTex] {2D}
"!!ARBfp1.0
# 27 ALU, 1 TEX
PARAM c[8] = { program.local[0..6],
		{ 1, 0.5, 2 } };
TEMP R0;
TEMP R1;
MOV R0.x, c[2];
MUL R0.x, R0, c[0].y;
SIN R0.y, R0.x;
MOV R0.w, c[4].x;
MOV R0.z, c[3].x;
ADD R1.xy, fragment.texcoord[0], -R0.zwzw;
MUL R1.z, R1.y, R0.y;
COS R0.x, R0.x;
MUL R1.y, R1, R0.x;
MAD R0.y, R1.x, R0, R1;
MAD R0.x, R1, R0, -R1.z;
ADD R0.xy, R0, R0.zwzw;
ADD R1.xy, fragment.texcoord[0], -c[7].y;
MUL R1.xy, R1, R1;
ADD R1.x, R1, R1.y;
RSQ R1.x, R1.x;
RCP R1.x, R1.x;
MUL R1.x, R1, c[7].z;
POW R1.x, R1.x, c[6].x;
ADD_SAT R1.x, -R1, c[7];
ADD R1.x, R1, -c[7];
MUL R1.x, R1, c[5];
ADD R1.x, R1, c[7];
TEX R0, R0, texture[0], 2D;
MUL R0, R0, c[1];
MUL R0, R0, fragment.color.primary;
MUL result.color, R0, R1.x;
END
# 27 instructions, 2 R-regs
"
}

SubProgram "d3d9 " {
Keywords { }
Local 0, [_Time]
Local 1, [_Color]
Local 2, ([_RotationSpeed],0,0,0)
Local 3, ([_CenterX],0,0,0)
Local 4, ([_CenterY],0,0,0)
Local 5, ([_VignetteIntensity],0,0,0)
Local 6, ([_VignettePower],0,0,0)
SetTexture [_MainTex] {2D}
"ps_2_0
; 39 ALU, 1 TEX
dcl_2d s0
def c7, 0.15915494, 0.50000000, 6.28318548, -3.14159274
def c8, -0.00000155, -0.00002170, 0.00260417, 0.00026042
def c9, -0.02083333, -0.12500000, 1.00000000, 0.50000000
def c10, -0.50000000, 2.00000000, -1.00000000, 0
dcl t0.xy
dcl v0
mov r0.y, c0
mul r0.x, c2, r0.y
mad r0.x, r0, c7, c7.y
frc r0.x, r0
mad r0.x, r0, c7.z, c7.w
sincos r2.xy, r0.x, c8.xyzw, c9.xyzw
mov r3.y, c4.x
mov r3.x, c3
add r4.xy, t0, -r3
mul r0.x, r4.y, r2
mul r1.x, r4.y, r2.y
mad r1.y, r4.x, r2, r0.x
mad r1.x, r4, r2, -r1
add r0.xy, r1, r3
texld r1, r0, s0
add r0.xy, t0, c10.x
mul r0.xy, r0, r0
add r0.x, r0, r0.y
rsq r0.x, r0.x
rcp r0.x, r0.x
mul r0.x, r0, c10.y
pow r2, r0.x, c6.x
mov r0.x, r2
add_sat r0.x, -r0, c9.z
add r0.x, r0, c10.z
mul r0.x, r0, c5
mul r1, r1, c1
add r0.x, r0, c9.z
mul r1, r1, v0
mul r0, r1, r0.x
mov oC0, r0
"
}

}
#LINE 86

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