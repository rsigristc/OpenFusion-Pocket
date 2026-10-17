Shader "Custom/VFX/RotatingFlipbook" {
	Properties {
		_MainTex ("Flipbook Texture", 2D) = "white" {}
		_Color ("Main Color", Color) = (1,1,1,1)
		_Columns ("Columns", Float) = 4.0
		_Rows ("Rows", Float) = 4.0
		_AnimationSpeed ("Animation Speed", Float) = 10.0
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
//   opengl - ALU: 50 to 50, TEX: 1 to 1
//   d3d9 - ALU: 64 to 64, TEX: 1 to 1
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
Local 2, ([_Columns],0,0,0)
Local 3, ([_Rows],0,0,0)
Local 4, ([_AnimationSpeed],0,0,0)
Local 5, ([_RotationSpeed],0,0,0)
Local 6, ([_CenterX],0,0,0)
Local 7, ([_CenterY],0,0,0)
Local 8, ([_VignetteIntensity],0,0,0)
Local 9, ([_VignettePower],0,0,0)
SetTexture [_MainTex] {2D}
"!!ARBfp1.0
# 50 ALU, 1 TEX
PARAM c[11] = { program.local[0..9],
		{ 1, 0.5, 2 } };
TEMP R0;
TEMP R1;
TEMP R2;
MOV R0.x, c[4];
MUL R0.y, R0.x, c[0];
FLR R1.z, R0.y;
RCP R0.x, c[2].x;
MUL R1.w, R1.z, R0.x;
MOV R0.y, c[5].x;
MUL R2.z, R0.y, c[0].y;
COS R0.y, R2.z;
RCP R1.xy, c[3].x;
FLR R2.x, R1.w;
MUL R2.y, R2.x, R1.x;
SIN R2.z, R2.z;
MOV R0.w, c[7].x;
MOV R0.z, c[6].x;
ADD R0.zw, fragment.texcoord[0].xyxy, -R0;
MUL R2.w, R0, R0.y;
MAD R2.w, R0.z, R2.z, R2;
MUL R2.z, R0.w, R2;
MAD R0.z, R0, R0.y, -R2;
MOV R0.y, R1;
ABS R1.y, c[3].x;
ADD R0.w, R2, c[7].x;
ADD R0.z, R0, c[6].x;
MUL R0.zw, R0, R0.xyxy;
ABS R0.y, R2;
FRC R0.y, R0;
MUL R0.y, R0, R1;
ABS R1.y, R1.w;
CMP R0.y, R2.x, -R0, R0;
MAD R0.y, R0, R1.x, R0.w;
ABS R1.w, c[2].x;
FRC R1.y, R1;
MUL R1.y, R1, R1.w;
CMP R1.y, R1.z, -R1, R1;
MAD R0.x, R0, R1.y, R0.z;
ADD R1.xy, fragment.texcoord[0], -c[10].y;
MUL R1.xy, R1, R1;
ADD R1.x, R1, R1.y;
RSQ R1.x, R1.x;
RCP R1.x, R1.x;
MUL R1.x, R1, c[10].z;
POW R1.x, R1.x, c[9].x;
ADD_SAT R1.x, -R1, c[10];
ADD R1.x, R1, -c[10];
MUL R1.x, R1, c[8];
ADD R1.x, R1, c[10];
TEX R0, R0, texture[0], 2D;
MUL R0, R0, c[1];
MUL R0, R0, fragment.color.primary;
MUL result.color, R0, R1.x;
END
# 50 instructions, 3 R-regs
"
}

SubProgram "d3d9 " {
Keywords { }
Local 0, [_Time]
Local 1, [_Color]
Local 2, ([_Columns],0,0,0)
Local 3, ([_Rows],0,0,0)
Local 4, ([_AnimationSpeed],0,0,0)
Local 5, ([_RotationSpeed],0,0,0)
Local 6, ([_CenterX],0,0,0)
Local 7, ([_CenterY],0,0,0)
Local 8, ([_VignetteIntensity],0,0,0)
Local 9, ([_VignettePower],0,0,0)
SetTexture [_MainTex] {2D}
"ps_2_0
; 64 ALU, 1 TEX
dcl_2d s0
def c10, 0.15915494, 0.50000000, 6.28318548, -3.14159274
def c11, -0.00000155, -0.00002170, 0.00260417, 0.00026042
def c12, -0.02083333, -0.12500000, 1.00000000, 0.50000000
def c13, -0.50000000, 2.00000000, -1.00000000, 0
dcl t0.xy
dcl v0
rcp r7.xy, c3.x
mov r0.y, c0
mul r0.x, c5, r0.y
mad r0.x, r0, c10, c10.y
frc r0.x, r0
mad r0.x, r0, c10.z, c10.w
sincos r2.xy, r0.x, c11.xyzw, c12.xyzw
rcp r6.x, c2.x
mov r0.y, c7.x
mov r0.x, c6
add r3.xy, t0, -r0
mul r0.x, r3.y, r2
mad r0.x, r3, r2.y, r0
mul r1.x, r3.y, r2.y
mad r1.x, r3, r2, -r1
add r4.x, r1, c6
add r4.y, r0.x, c7.x
mov r0.y, c0
mul r0.x, c4, r0.y
frc r2.x, r0
add r0.x, r0, -r2
mul r2.x, r0, r6
frc r1.x, r2
add r1.x, -r1, r2
mov r6.y, r7
mul r3.x, r1, r7
mul r5.xy, r4, r6
abs r4.x, r3
frc r4.x, r4
abs r3.x, c3
mul r3.x, r4, r3
abs r4.x, r2
cmp r1.x, r1, r3, -r3
mad r0.y, r1.x, r7.x, r5
abs r2.x, c2
frc r4.x, r4
mul r2.x, r4, r2
cmp r0.x, r0, r2, -r2
mad r0.x, r0, r6, r5
texld r1, r0, s0
add r0.xy, t0, c13.x
mul r0.xy, r0, r0
add r0.x, r0, r0.y
rsq r0.x, r0.x
rcp r0.x, r0.x
mul r0.x, r0, c13.y
pow r2, r0.x, c9.x
mov r0.x, r2
add_sat r0.x, -r0, c12.z
add r0.x, r0, c13.z
mul r0.x, r0, c8
mul r1, r1, c1
add r0.x, r0, c12.z
mul r1, r1, v0
mul r0, r1, r0.x
mov oC0, r0
"
}

}
#LINE 101

			}
		}
		
		// Fallback for older hardware
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