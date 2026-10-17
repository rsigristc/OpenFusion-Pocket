Shader "RetroLit" {
Properties {
	_Color ("Main Color", Color) = (1,1,1,1)
	_SpecColor ("Spec Color", Color) = (1,1,1,1)
	_Emission ("Emissive Color", Color) = (0,0,0,0)
	_Shininess ("Shininess", Range (0.01, 1)) = 0.7
	_MainTex ("Base (RGB)", 2D) = "white" {}
}

SubShader {
	Tags { "RenderType"="Opaque" }
	LOD 100
	
	// Normal rendering pass
	Pass {
		Material {
			Diffuse [_Color]
			Ambient [_Color]
			Shininess [_Shininess]
			Specular [_SpecColor]
			Emission [_Emission]
		} 
		Lighting On
		SeparateSpecular On
		SetTexture [_MainTex] {
			Combine texture * primary DOUBLE, texture * primary
		} 
	}
	
	// Pass to render object as a shadow caster
	Pass {
		Name "ShadowCaster"
		Tags { "LightMode" = "ShadowCaster" }
		
		Fog {Mode Off}
		ZWrite On ZTest Less Cull Off
		Offset [_ShadowBias], [_ShadowBiasSlope]

Program "" {
// Vertex combos: 2
//   opengl - ALU: 8 to 10
//   d3d9 - ALU: 8 to 13
// Fragment combos: 2
//   opengl - ALU: 1 to 7, TEX: 0 to 0
//   d3d9 - ALU: 4 to 8
SubProgram "opengl " {
Keywords { "SHADOWS_NATIVE" }
Bind "vertex", Vertex
Local 1, [_LightDirectionBias]
"!!ARBvp1.0
# 10 ALU
PARAM c[6] = { program.local[0..1],
		state.matrix.mvp };
TEMP R0;
TEMP R1;
MOV R0.w, vertex.position;
MAD R0.xyz, c[1], c[1].w, vertex.position;
DP4 R1.x, R0, c[5];
DP4 R1.y, R0, c[4];
SLT R1.z, R1.y, -R1.x;
ADD R1.w, -R1.x, -R1.y;
MAD result.position.z, R1.w, R1, R1.y;
MOV result.position.w, R1.x;
DP4 result.position.y, R0, c[3];
DP4 result.position.x, R0, c[2];
END
# 10 instructions, 2 R-regs
"
}

SubProgram "d3d9 " {
Keywords { "SHADOWS_NATIVE" }
Bind "vertex", Vertex
Local 4, [_LightDirectionBias]
Matrix 0, [glstate_matrix_mvp]
"vs_1_1
; 13 ALU
dcl_position v0
def c5, 0.00000000, 1.00000000, 0, 0
mov r1.w, v0
mad r1.xyz, c4, c4.w, v0
dp4 r0.x, r1, c2
slt r0.y, r0.x, c5.x
max r0.y, -r0, r0
slt r0.y, c5.x, r0
add r0.y, -r0, c5
mul r0.z, r0.y, r0.x
dp4 r0.w, r1, c3
dp4 r0.x, r1, c0
dp4 r0.y, r1, c1
mov oPos, r0
mov oT0, r0
"
}

SubProgram "opengl " {
Keywords { "SHADOWS_CUBE" }
Bind "vertex", Vertex
Matrix 1, [_Object2World]
Local 5, [_LightPositionRange]
"!!ARBvp1.0
# 8 ALU
PARAM c[10] = { program.local[0..5],
		state.matrix.mvp };
TEMP R0;
DP4 R0.z, vertex.position, c[3];
DP4 R0.x, vertex.position, c[1];
DP4 R0.y, vertex.position, c[2];
ADD result.texcoord[0].xyz, R0, -c[5];
DP4 result.position.w, vertex.position, c[9];
DP4 result.position.z, vertex.position, c[8];
DP4 result.position.y, vertex.position, c[7];
DP4 result.position.x, vertex.position, c[6];
END
# 8 instructions, 1 R-regs
"
}

SubProgram "d3d9 " {
Keywords { "SHADOWS_CUBE" }
Bind "vertex", Vertex
Matrix 0, [_Object2World]
Local 8, [_LightPositionRange]
Matrix 4, [glstate_matrix_mvp]
"vs_1_1
; 8 ALU
dcl_position v0
dp4 r0.z, v0, c2
dp4 r0.x, v0, c0
dp4 r0.y, v0, c1
add oT0.xyz, r0, -c8
dp4 oPos.w, v0, c7
dp4 oPos.z, v0, c6
dp4 oPos.y, v0, c5
dp4 oPos.x, v0, c4
"
}

SubProgram "opengl " {
Keywords { "SHADOWS_NATIVE" }
"!!ARBfp1.0
OPTION ARB_precision_hint_fastest;
# 1 ALU, 0 TEX
PARAM c[1] = { { 0 } };
MOV result.color, c[0].x;
END
# 1 instructions, 0 R-regs
"
}

SubProgram "d3d9 " {
Keywords { "SHADOWS_NATIVE" }
"ps_2_0
; 4 ALU
dcl t0.xyzw
rcp r0.x, t0.w
mul r0.x, t0.z, r0
mov r0, r0.x
mov oC0, r0
"
}

SubProgram "opengl " {
Keywords { "SHADOWS_CUBE" }
Local 0, [_LightPositionRange]
Local 1, [_RGBAEncodeDot]
Local 2, [_RGBAEncodeBias]
"!!ARBfp1.0
OPTION ARB_precision_hint_fastest;
# 7 ALU, 0 TEX
PARAM c[3] = { program.local[0..2] };
TEMP R0;
DP3 R0.x, fragment.texcoord[0], fragment.texcoord[0];
RSQ R0.x, R0.x;
RCP R0.x, R0.x;
MUL R0.x, R0, c[0].w;
MUL R0, R0.x, c[1];
FRC R0, R0;
ADD result.color, R0, c[2];
END
# 7 instructions, 1 R-regs
"
}

SubProgram "d3d9 " {
Keywords { "SHADOWS_CUBE" }
Local 0, [_LightPositionRange]
Local 1, [_RGBAEncodeDot]
Local 2, [_RGBAEncodeBias]
"ps_2_0
; 8 ALU
dcl t0.xyz
dp3 r0.x, t0, t0
rsq r0.x, r0.x
rcp r0.x, r0.x
mul r0.x, r0, c0.w
mul r0, r0.x, c1
frc r0, r0
add r0, r0, c2
mov oC0, r0
"
}

}
#LINE 61


	}
	
	// Pass to render object as a shadow collector
	Pass {
		Name "ShadowCollector"
		Tags { "LightMode" = "ShadowCollector" }
		
		Fog {Mode Off}
		ZWrite On ZTest Less

Program "" {
// Vertex combos: 1
//   opengl - ALU: 27 to 27
//   d3d9 - ALU: 27 to 27
// Fragment combos: 1
//   opengl - ALU: 20 to 20, TEX: 1 to 1
//   d3d9 - ALU: 22 to 22, TEX: 1 to 1
SubProgram "opengl " {
Keywords { }
Bind "vertex", ATTR0
Matrix 1, [_Object2World]
Matrix 5, [_World2Shadow]
Matrix 9, [_World2Shadow1]
Matrix 13, [_World2Shadow2]
Matrix 17, [_World2Shadow3]
Local 21, [_LightShadowData]
"!!ARBvp1.0
# 27 ALU
PARAM c[30] = { program.local[0..21],
		state.matrix.modelview[0],
		state.matrix.mvp };
TEMP R0;
DP4 R0.x, vertex.attrib[0], c[1];
DP4 R0.w, vertex.attrib[0], c[4];
DP4 R0.z, vertex.attrib[0], c[3];
DP4 R0.y, vertex.attrib[0], c[2];
DP4 result.texcoord[0].w, R0, c[8];
DP4 result.texcoord[0].z, R0, c[7];
DP4 result.texcoord[0].y, R0, c[6];
DP4 result.texcoord[0].x, R0, c[5];
DP4 result.texcoord[1].w, R0, c[12];
DP4 result.texcoord[1].z, R0, c[11];
DP4 result.texcoord[1].y, R0, c[10];
DP4 result.texcoord[1].x, R0, c[9];
DP4 result.texcoord[2].w, R0, c[16];
DP4 result.texcoord[2].z, R0, c[15];
DP4 result.texcoord[2].y, R0, c[14];
DP4 result.texcoord[2].x, R0, c[13];
DP4 result.texcoord[3].w, R0, c[20];
DP4 result.texcoord[3].z, R0, c[19];
DP4 result.texcoord[3].y, R0, c[18];
DP4 result.texcoord[3].x, R0, c[17];
DP4 R0.x, vertex.attrib[0], c[24];
DP4 result.position.w, vertex.attrib[0], c[29];
DP4 result.position.z, vertex.attrib[0], c[28];
DP4 result.position.y, vertex.attrib[0], c[27];
DP4 result.position.x, vertex.attrib[0], c[26];
MAD result.texcoord[4].y, -R0.x, c[21].z, c[21].w;
MOV result.texcoord[4].x, -R0;
END
# 27 instructions, 1 R-regs
"
}

SubProgram "d3d9 " {
Keywords { }
Bind "vertex", Vertex
Matrix 0, [_Object2World]
Matrix 4, [_World2Shadow]
Matrix 8, [_World2Shadow1]
Matrix 12, [_World2Shadow2]
Matrix 16, [_World2Shadow3]
Local 28, [_LightShadowData]
Matrix 20, [glstate_matrix_modelview0]
Matrix 24, [glstate_matrix_mvp]
"vs_1_1
; 27 ALU
dcl_position v0
dp4 r0.x, v0, c0
dp4 r0.w, v0, c3
dp4 r0.z, v0, c2
dp4 r0.y, v0, c1
dp4 oT0.w, r0, c7
dp4 oT0.z, r0, c6
dp4 oT0.y, r0, c5
dp4 oT0.x, r0, c4
dp4 oT1.w, r0, c11
dp4 oT1.z, r0, c10
dp4 oT1.y, r0, c9
dp4 oT1.x, r0, c8
dp4 oT2.w, r0, c15
dp4 oT2.z, r0, c14
dp4 oT2.y, r0, c13
dp4 oT2.x, r0, c12
dp4 oT3.w, r0, c19
dp4 oT3.z, r0, c18
dp4 oT3.y, r0, c17
dp4 oT3.x, r0, c16
dp4 r0.x, v0, c22
dp4 oPos.w, v0, c27
dp4 oPos.z, v0, c26
dp4 oPos.y, v0, c25
dp4 oPos.x, v0, c24
mad oT4.y, -r0.x, c28.z, c28.w
mov oT4.x, -r0
"
}

SubProgram "opengl " {
Keywords { }
Local 0, [_LightShadowData]
Local 1, [_LightSplitsNear]
Local 2, [_LightSplitsFar]
SetTexture [_ShadowMapTexture] {2D}
"!!ARBfp1.0
OPTION ARB_precision_hint_fastest;
# 20 ALU, 1 TEX
PARAM c[4] = { program.local[0..2],
		{ 1, 255 } };
TEMP R0;
TEMP R1;
SLT R1, fragment.texcoord[4].x, c[2];
SGE R0, fragment.texcoord[4].x, c[1];
MUL R0, R0, R1;
MUL R1, R0.y, fragment.texcoord[1];
MAD R1, R0.x, fragment.texcoord[0], R1;
MAD R1, R0.z, fragment.texcoord[2], R1;
MAD R0, fragment.texcoord[3], R0.w, R1;
MOV result.color.y, c[3].x;
TXP R0.x, R0, texture[0], 2D;
RCP R0.y, R0.w;
MAD R0.x, -R0.z, R0.y, R0;
MOV R0.w, c[3].x;
CMP R0.x, R0, c[0], R0.w;
MOV_SAT R0.y, fragment.texcoord[4];
ADD_SAT result.color.x, R0, R0.y;
MUL R0.z, -fragment.texcoord[4].x, c[0].y;
ADD_SAT R0.x, R0.z, c[3];
MUL R0.y, R0.x, c[3];
MOV result.color.z, R0.x;
FRC result.color.w, R0.y;
END
# 20 instructions, 2 R-regs
"
}

SubProgram "d3d9 " {
Keywords { }
Local 0, [_LightShadowData]
Local 1, [_LightSplitsNear]
Local 2, [_LightSplitsFar]
SetTexture [_ShadowMapTexture] {2D}
"ps_2_0
; 22 ALU, 1 TEX
dcl_2d s0
def c3, 1.00000000, 0.00000000, 255.00000000, 0
dcl t0
dcl t1
dcl t2
dcl t3
dcl t4.xy
add r1, t4.x, -c2
add r0, t4.x, -c1
cmp r1, r1, c3.y, c3.x
cmp r0, r0, c3.x, c3.y
mul r0, r0, r1
mul r1, r0.y, t1
mad r1, r0.x, t0, r1
mad r1, r0.z, t2, r1
mad r0, t3, r0.w, r1
rcp r2.x, r0.w
mov_pp r2.y, c3.x
texldp r1, r0, s0
mad r1.x, -r0.z, r2, r1
mov r0.x, c0
cmp r1.x, r1, c3, r0
mul r0.x, -t4, c0.y
add_sat r0.x, r0, c3
mov_sat r2.x, t4.y
add_sat r2.x, r1, r2
mul_pp r1.x, r0, c3.z
frc_pp r2.w, r1.x
mov_pp r2.z, r0.x
mov_pp oC0, r2
"
}

}
#LINE 100


	}
}
}
