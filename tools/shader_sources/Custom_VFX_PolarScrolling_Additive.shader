Shader "Custom/VFX/PolarScrolling_Additive" {
    Properties {
        _MainTex ("Base Texture", 2D) = "white" {}
        _Color ("Main Color", Color) = (1,1,1,1)
        _ScrollSpeedU ("Scroll Speed U", Float) = 1.0
        _ScrollSpeedV ("Scroll Speed V", Float) = 1.0
        _CenterX ("Center X", Float) = 0.0
        _CenterY ("Center Y", Float) = 0.0
        _RadialScale ("Radial Scale", Float) = 1.0
        _AngularScale ("Angular Scale", Float) = 1.0
        _VignetteIntensity ("Vignette Intensity", Float) = 0.0
        _VignettePower ("Vignette Power", Float) = 2.0
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
//   opengl - ALU: 6 to 6
//   d3d9 - ALU: 6 to 6
// Fragment combos: 1
//   opengl - ALU: 49 to 49, TEX: 1 to 1
//   d3d9 - ALU: 53 to 53, TEX: 1 to 1
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
Local 2, ([_ScrollSpeedU],0,0,0)
Local 3, ([_ScrollSpeedV],0,0,0)
Local 4, ([_CenterX],0,0,0)
Local 5, ([_CenterY],0,0,0)
Local 6, ([_RadialScale],0,0,0)
Local 7, ([_AngularScale],0,0,0)
Local 8, ([_VignetteIntensity],0,0,0)
Local 9, ([_VignettePower],0,0,0)
SetTexture [_MainTex] {2D}
"!!ARBfp1.0
# 49 ALU, 1 TEX
PARAM c[13] = { program.local[0..9],
		{ 0.15915494, 0.5, 3.1415927, 1.5707964 },
		{ -0.01348047, 0.057477314, 0.12123907, 0.19563593 },
		{ 0.33299461, 0.99999565, 1, 2 } };
TEMP R0;
TEMP R1;
TEMP R2;
ADD R1.xy, fragment.texcoord[0], -c[10].y;
ADD R0.x, R1, c[4];
ADD R0.y, R1, c[5].x;
MUL R1.xy, R1, R1;
ADD R1.x, R1, R1.y;
RSQ R1.x, R1.x;
RCP R1.x, R1.x;
MUL R1.x, R1, c[12].w;
POW R1.x, R1.x, c[9].x;
ADD_SAT R1.x, -R1, c[12].z;
ADD R1.x, R1, -c[12].z;
MUL R1.x, R1, c[8];
ABS R0.w, R0.x;
ABS R0.z, R0.y;
MAX R1.z, R0, R0.w;
RCP R1.w, R1.z;
MIN R1.z, R0, R0.w;
MUL R1.z, R1, R1.w;
MUL R1.w, R1.z, R1.z;
MAD R2.x, R1.w, c[11], c[11].y;
MAD R2.x, R2, R1.w, -c[11].z;
MAD R2.x, R2, R1.w, c[11].w;
MAD R2.x, R2, R1.w, -c[12];
MAD R1.w, R2.x, R1, c[12].y;
MUL R1.z, R1.w, R1;
ADD R0.z, R0, -R0.w;
ADD R1.w, -R1.z, c[10];
CMP R0.z, -R0, R1.w, R1;
ADD R0.w, -R0.z, c[10].z;
CMP R0.z, R0.x, R0.w, R0;
CMP R0.z, R0.y, -R0, R0;
MUL R0.xy, R0, R0;
ADD R0.x, R0, R0.y;
MUL R0.z, R0, c[7].x;
MOV R0.w, c[2].x;
MOV R0.y, c[3].x;
RSQ R0.x, R0.x;
MUL R0.w, R0, c[0].y;
ADD R0.z, R0, c[10];
MAD R0.z, R0, c[10].x, R0.w;
RCP R0.x, R0.x;
MUL R0.y, R0, c[0];
MAD R0.y, R0.x, c[6].x, R0;
FRC R0.x, R0.z;
ADD R1.x, R1, c[12].z;
TEX R0, R0, texture[0], 2D;
MUL R0, R0, c[1];
MUL R0, R0, fragment.color.primary;
MUL result.color, R0, R1.x;
END
# 49 instructions, 3 R-regs
"
}

SubProgram "d3d9 " {
Keywords { }
Local 0, [_Time]
Local 1, [_Color]
Local 2, ([_ScrollSpeedU],0,0,0)
Local 3, ([_ScrollSpeedV],0,0,0)
Local 4, ([_CenterX],0,0,0)
Local 5, ([_CenterY],0,0,0)
Local 6, ([_RadialScale],0,0,0)
Local 7, ([_AngularScale],0,0,0)
Local 8, ([_VignetteIntensity],0,0,0)
Local 9, ([_VignettePower],0,0,0)
SetTexture [_MainTex] {2D}
"ps_2_0
; 53 ALU, 1 TEX
dcl_2d s0
def c10, -0.50000000, -0.01348047, 0.05747731, -0.12123907
def c11, 0.19563593, -0.33299461, 0.99999565, 1.57079637
def c12, 3.14159274, 0.15915494, 2.00000000, 1.00000000
def c13, -1.00000000, 0, 0, 0
dcl t0.xy
dcl v0
add r6.xy, t0, c10.x
add r0.x, r6.y, c5
add r7.x, r6, c4
abs r2.x, r7
abs r1.x, r0
max r3.x, r1, r2
rcp r4.x, r3.x
min r3.x, r1, r2
mul r3.x, r3, r4
mul r4.x, r3, r3
mad r5.x, r4, c10.y, c10.z
mad r5.x, r5, r4, c10.w
mad r5.x, r5, r4, c11
mad r5.x, r5, r4, c11.y
mad r4.x, r5, r4, c11.z
mul r3.x, r4, r3
add r1.x, r1, -r2
add r4.x, -r3, c11.w
cmp r1.x, -r1, r3, r4
add r2.x, -r1, c12
cmp r1.x, r7, r1, r2
cmp r1.x, r0, r1, -r1
mov r7.y, r0.x
mov r0.y, c0
mul r0.x, c2, r0.y
mul r1.x, r1, c7
add r1.x, r1, c12
mad r0.x, r1, c12.y, r0
mul r2.xy, r7, r7
add r1.x, r2, r2.y
mov r0.y, c0
rsq r1.x, r1.x
mul r2.x, c3, r0.y
rcp r1.x, r1.x
mad r0.y, r1.x, c6.x, r2.x
frc r0.x, r0
texld r1, r0, s0
mul r0.xy, r6, r6
add r0.x, r0, r0.y
rsq r0.x, r0.x
rcp r0.x, r0.x
mul r0.x, r0, c12.z
pow r2, r0.x, c9.x
mov r0.x, r2
add_sat r0.x, -r0, c12.w
add r0.x, r0, c13
mul r0.x, r0, c8
mul r1, r1, c1
add r0.x, r0, c12.w
mul r1, r1, v0
mul r0, r1, r0.x
mov oC0, r0
"
}

}
#LINE 89

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