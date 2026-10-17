Shader "Skin_DirLight_AmbLight_blendSrcalphaInvsrcalpha_cullOff" {
Properties {
	_Color ("Main Color", Color) = (1,1,1,1)
	_SpecColor ("Spec Color", Color) = (1,1,1,1)
	_AmbColor ("Ambient Color", Color) = (1,1,1,1)
	_Emission ("Emmisive Color", Color) = (0,0,0,0)
	_Shininess ("Shininess", Range (0.01, 1)) = 0.7
	_MainTex ("Base (RGB)", 2D) = "white" {}
	_FatFactor ("Fat Factor", Range (-0.01,0.01)) = 0
	_Cutoff ("Cutoff", Range (0, 1)) = 0.0
	_SpecMap ("SpecRamp (RGB)", 2D) = "black" {}
}

Category {
	Tags {"Queue"="Transparent-100"}
	Blend SrcAlpha OneMinusSrcAlpha 
	ColorMask RGBA
	Cull Off
	
	SubShader {
		Pass {
			Name "BASE"

Program "" {
// Vertex combos: 1, instructions: 22 to 22
SubProgram "opengl " {
Keywords { }
Bind "vertex", Vertex
Bind "normal", Normal
Bind "texcoord", TexCoord0
Local 1, [_MainTex_ST]
Local 2, [_Color]
Local 3, [_AmbColor]
Local 4, ([_FatFactor],0,0,0)
"!!ARBvp1.0
# 22 instructions
PARAM c[14] = { { 0, 1, 0.1 },
		program.local[1..4],
		state.light[0].position,
		state.matrix.modelview[0],
		state.matrix.mvp };
TEMP R0;
TEMP R1;
MOV R1.xyz, vertex.normal;
MOV R1.w, c[0].x;
DP4 R0.z, R1, c[8];
DP4 R0.y, R1, c[7];
DP4 R0.x, R1, c[6];
DP3 R0.x, R0, c[5];
MIN R0.x, R0, c[0].y;
MAX R0.x, R0, c[0];
MUL R0, R0.x, c[2];
ADD R0, R0, c[3];
ADD result.color, R0, c[0].zzzx;
MUL R0.xyz, vertex.normal, c[4].x;
MOV R0.w, vertex.position;
ADD R0.xyz, R0, vertex.position;
DP4 R1.x, R0, c[12];
DP4 result.position.w, R0, c[13];
MOV result.position.z, R1.x;
DP4 result.position.y, R0, c[11];
DP4 result.position.x, R0, c[10];
MOV result.fogcoord.x, R1;
MOV result.texcoord[0].zw, c[0].y;
MAD result.texcoord[0].xy, vertex.texcoord[0], c[1], c[1].zwzw;
END
# 22 instructions, 2 R-regs
"
}

SubProgram "d3d9 " {
Keywords { }
Bind "vertex", Vertex
Bind "normal", Normal
Bind "texcoord", TexCoord0
Local 8, [_MainTex_ST]
Local 9, [_Color]
Local 10, [_AmbColor]
Local 11, ([_FatFactor],0,0,0)
Local 12, [glstate_light0_position]
Matrix 0, [glstate_matrix_modelview0]
Matrix 4, [glstate_matrix_mvp]
"vs_1_1
dcl_position v0
dcl_normal v1
dcl_texcoord0 v2
def c13, 0.00000000, 1.00000000, 0.10000000, 0
mov r1.xyz, v1
mov r1.w, c13.x
dp4 r0.z, r1, c2
dp4 r0.y, r1, c1
dp4 r0.x, r1, c0
dp3 r0.x, r0, c12
min r0.x, r0, c13.y
max r0.x, r0, c13
mul r0, r0.x, c9
add r0, r0, c10
add oD0, r0, c13.zzzx
mul r0.xyz, v1, c11.x
mov r0.w, v0
add r0.xyz, r0, v0
dp4 r1.x, r0, c6
dp4 oPos.w, r0, c7
mov oPos.z, r1.x
dp4 oPos.y, r0, c5
dp4 oPos.x, r0, c4
mov oFog, r1.x
mov oT0.zw, c13.y
mad oT0.xy, v2, c8, c8.zwzw
"
}

}
#LINE 63


				SetTexture [_MainTex] {
					Combine texture * primary DOUBLE, texture
				} 
			}
	}
}
}