Shader "retro_diffuseFade" {
	Properties {
		_Color ("Main Color", Color) = (1,1,1,1)
		_SpecColor ("Spec Color", Color) = (1,1,1,1)
		_AmbColor ("Ambient Color", Color) = (1,1,1,1)
		_Emission ("Emmisive Color", Color) = (0,0,0,0)
		_Shininess ("Shininess", Range (0.01, 1)) = 0.7
		_MainTex ("Tex0", 2D) = "black" {}
		_BumpMap ("Tex1", 2D) = "black" {}
		_ShaderMap ("Tex2", 2D) = "black" {}	
		_SpecMap ("SpecRamp (RGB)", 2D) = "black" {}		
		_Speed ("Speed", Range (-10,10)) = 0
	}
Category
{
	Tags {"Queue"="Transparent-100"}
	Blend SrcAlpha OneMinusSrcAlpha
	ColorMask RGB

	SubShader {
		Pass {
			Name "BASE"
Program "" {
// Vertex combos: 1, instructions: 29 to 29
SubProgram "opengl " {
Keywords { }
Bind "vertex", Vertex
Bind "normal", Normal
Bind "texcoord", TexCoord0
Local 1, [_Time]
Local 2, [_MainTex_ST]
Local 3, ([_Speed],0,0,0)
Local 4, [_Color]
Local 5, [_Emission]
"!!ARBvp1.0
# 29 instructions
PARAM c[15] = { { 1, 0 },
		program.local[1..5],
		state.light[0].position,
		state.matrix.modelview[0],
		state.matrix.mvp };
TEMP R0;
TEMP R1;
TEMP R2;
MAD R2.xy, vertex.texcoord[0], c[2], c[2].zwzw;
DP3 R0.z, vertex.normal, c[9];
DP3 R0.y, vertex.normal, c[8];
DP3 R0.x, vertex.normal, c[7];
DP3 R0.x, R0, c[6];
MAX R1.x, R0, c[0].y;
MOV R0.xyz, c[5];
MOV R0.w, c[4];
MAD result.color, R1.x, c[4], R0;
MOV R1.x, c[3];
MUL R2.z, R1.x, c[1].x;
MOV R0.xyz, vertex.normal;
MOV R0.w, c[0].x;
DP4 R2.w, R0, c[11];
DP4 result.texcoord[2].y, R0, c[12];
DP4 R0.x, vertex.position, c[13];
MOV R1.x, R2;
MOV R1.zw, c[0].x;
ADD R1.y, R2, -R2.z;
MOV result.texcoord[1], R1;
ADD result.texcoord[2].x, R2.z, R2.w;
MOV result.texcoord[0].xzw, R1;
ADD result.texcoord[0].y, R2, R2.z;
MOV result.texcoord[2].zw, c[0].x;
DP4 result.position.w, vertex.position, c[14];
MOV result.position.z, R0.x;
DP4 result.position.y, vertex.position, c[12];
DP4 result.position.x, vertex.position, c[11];
MOV result.fogcoord.x, R0;
END
# 29 instructions, 3 R-regs
"
}

SubProgram "d3d9 " {
Keywords { }
Bind "vertex", Vertex
Bind "normal", Normal
Bind "texcoord", TexCoord0
Local 8, [_Time]
Local 9, [_MainTex_ST]
Local 10, ([_Speed],0,0,0)
Local 11, [_Color]
Local 12, [_Emission]
Local 13, [glstate_light0_position]
Matrix 0, [glstate_matrix_modelview0]
Matrix 4, [glstate_matrix_mvp]
"vs_1_1
def c14, 1.00000000, 0.00000000, 0, 0
dcl_position v0
dcl_normal v1
dcl_texcoord0 v2
mad r2.xy, v2, c9, c9.zwzw
dp3 r0.z, v1, c2
dp3 r0.y, v1, c1
dp3 r0.x, v1, c0
dp3 r0.x, r0, c13
max r1.x, r0, c14.y
mov r0.xyz, c12
mov r0.w, c11
mad oD0, r1.x, c11, r0
mov r1.x, c8
mul r2.z, c10.x, r1.x
mov r0.xyz, v1
mov r0.w, c14.x
dp4 r2.w, r0, c4
dp4 oT2.y, r0, c5
dp4 r0.x, v0, c6
mov r1.x, r2
mov r1.zw, c14.x
add r1.y, r2, -r2.z
mov oT1, r1
add oT2.x, r2.z, r2.w
mov oT0.xzw, r1
add oT0.y, r2, r2.z
mov oT2.zw, c14.x
dp4 oPos.w, v0, c7
mov oPos.z, r0.x
dp4 oPos.y, v0, c5
dp4 oPos.x, v0, c4
mov oFog, r0.x
"
}

}
#LINE 67

			SetTexture [_MainTex] {
				//constantColor [_Color]
				//Combine texture * constant DOUBLE				
				Combine texture * primary DOUBLE
			} 
			SetTexture [_BumpMap] {
				combine texture + previous, previous
			}
			SetTexture [_ShaderMap] {
				combine texture + previous, previous
			}
		}
	} 
	FallBack "Diffuse", 1
}
}
