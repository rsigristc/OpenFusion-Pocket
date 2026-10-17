Shader "SkinnedToonShading_blendSrcalphaInvsrcalphaflipped" {
	Properties {
		_Color ("Main Color", Color) = (0.5,0.5,0.5,1)
		_AmbColor ("Ambient Color", Color) = (0,0,0,1)
		_Emission ("Emmisive Color", Color) = (0.235,0.235,0.235,0)
		_OutlineColor ("Outline Color", Color) = (0,0,0,1)
		_Outline ("Outline width", Range (.002, 0.03)) = .005
		_MainTex ("Base (RGB)", 2D) = "white" {}
		_SpecMap ("SpecRamp (RGB)", 2D) = "" {}		
		_ShaderMap ("ToonRamp (RGB)", 2D) = "white" {}

	}
		
Category {
	Tags {"Queue"="Transparent-100"}
	Blend SrcAlpha OneMinusSrcAlpha 
	ColorMask RGBA
	
	SubShader {
		LOD 300
		Pass {
			Name "BASE"

			BindChannels {
				Bind "vertex", Vertex
				Bind "normal", Normal
				Bind "texcoord", TexCoord0
			}
			Lighting On
			Program "" {
// Vertex combos: 1
//   opengl - ALU: 40 to 40
//   d3d9 - ALU: 40 to 40
SubProgram "opengl " {
Keywords { }
Bind "vertex", Vertex
Bind "normal", Normal
Bind "texcoord", TexCoord0
Local 1, ([_FatFactor],0,0,0)
Local 2, [_MainTex_ST]
"!!ARBvp1.0
# 40 ALU
PARAM c[16] = { { 1, 0, 0.0099999998, 0.99000001 },
		program.local[1..2],
		state.light[0].position,
		state.matrix.modelview[0],
		state.matrix.mvp,
		state.matrix.modelview[0].invtrans };
TEMP R0;
TEMP R1;
TEMP R2;
MUL R0.xyz, vertex.normal, c[1].x;
ADD R2.xyz, vertex.position, R0;
MOV R2.w, vertex.position;
MOV R1.x, c[12].w;
MOV R1.y, c[13].w;
MOV R1.z, c[14].w;
MOV R1.w, c[15];
DP4 R0.x, R2, c[4];
DP4 R0.y, R2, c[5];
DP4 R0.z, R2, c[6];
DP4 R0.w, R2, c[7];
ADD R0, R1, -R0;
DP4 R1.w, R0, R0;
RSQ R1.w, R1.w;
DP3 R1.y, vertex.normal, c[5];
DP3 R1.z, vertex.normal, c[6];
DP3 R1.x, vertex.normal, c[4];
DP3 R0.w, R1, R1;
RSQ R0.w, R0.w;
MUL R1.xyz, R0.w, R1;
MUL R0.xyz, R1.w, R0;
DP3 R0.w, R1, R0;
DP3 R1.x, R1, c[3];
ADD R0.w, -R0, c[0].x;
DP3 R0.x, R0, -c[3];
MAD R0.x, R0.w, R0, R1;
MIN R0.x, R0, c[0];
MAX R0.x, R0, c[0].y;
ADD R0.x, R1, R0;
MAX R0.x, R0, c[0].z;
MIN result.texcoord[1].x, R0, c[0].w;
DP4 R0.x, R2, c[10];
DP4 result.position.x, R2, c[8];
DP4 result.position.y, R2, c[9];
MOV result.position.z, R0.x;
DP4 result.position.w, R2, c[11];
MOV result.fogcoord.x, R0;
MOV result.texcoord[1].yzw, c[0].x;
MAD result.texcoord[0].xy, vertex.texcoord[0], c[2], c[2].zwzw;
MOV result.texcoord[0].zw, c[0].x;
END
# 40 instructions, 3 R-regs
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
; 40 ALU
def c15, 1.00000000, 0.00000000, 0.01000000, 0.99000001
dcl_position v0
dcl_normal v1
dcl_texcoord0 v2
mul r0.xyz, v1, c12.x
add r2.xyz, v0, r0
mov r2.w, v0
mov r1.x, c8.w
mov r1.y, c9.w
mov r1.z, c10.w
mov r1.w, c11
dp4 r0.x, r2, c0
dp4 r0.y, r2, c1
dp4 r0.z, r2, c2
dp4 r0.w, r2, c3
add r0, r1, -r0
dp4 r1.w, r0, r0
rsq r1.w, r1.w
dp3 r1.y, v1, c1
dp3 r1.z, v1, c2
dp3 r1.x, v1, c0
dp3 r0.w, r1, r1
rsq r0.w, r0.w
mul r1.xyz, r0.w, r1
mul r0.xyz, r1.w, r0
dp3 r0.w, r1, r0
dp3 r1.x, r1, c14
add r0.w, -r0, c15.x
dp3 r0.x, r0, -c14
mad r0.x, r0.w, r0, r1
min r0.x, r0, c15
max r0.x, r0, c15.y
add r0.x, r1, r0
max r0.x, r0, c15.z
min oT1.x, r0, c15.w
dp4 r0.x, r2, c6
dp4 oPos.x, r2, c4
dp4 oPos.y, r2, c5
mov oPos.z, r0.x
dp4 oPos.w, r2, c7
mov oFog, r0.x
mov oT1.yzw, c15.x
mad oT0.xy, v2, c13, c13.zwzw
mov oT0.zw, c15.x
"
}

}
#LINE 208

		SetTexture [_MainTex] {
			constantColor [_Color]
			Combine texture * constant DOUBLE
		} 
		SetTexture [_ShaderMap] {
			combine texture * previous
	 	}
	 	//SetTexture [_SpecMap] {
	 	//	combine texture * previous + previous
	 	//}
	}
		

		Pass {
			Name "OUTLINE"
			Tags { "LightMode" = "Always" }
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
MOV R0.x, c[3];
ADD R0.x, R0, c[2];
MUL R0.xyz, -vertex.normal, R0.x;
MOV R0.w, vertex.position;
ADD R0.xyz, R0, vertex.position;
DP4 R1.x, R0, c[6];
DP4 result.position.x, R0, c[4];
DP4 result.position.y, R0, c[5];
MOV result.position.z, R1.x;
DP4 result.position.w, R0, c[7];
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
mov r0.x, c5
add r0.x, c6, r0
mul r0.xyz, -v1, r0.x
mov r0.w, v0
add r0.xyz, r0, v0
dp4 r1.x, r0, c2
dp4 oPos.x, r0, c0
dp4 oPos.y, r0, c1
mov oPos.z, r1.x
dp4 oPos.w, r0, c3
mov oFog, r1.x
mov oD0, c4
"
}

}
#LINE 311

		//Color (0,0,0,0)
			Cull Back
			ZWrite On
			ColorMask RGBA
			Blend SrcAlpha OneMinusSrcAlpha
			SetTexture [_MainTex] { combine primary }
		}
	} 

 	FallBack "Skin_DirLight_AmbLight_blendSrcalphaInvsrcalpha", 1
}}
