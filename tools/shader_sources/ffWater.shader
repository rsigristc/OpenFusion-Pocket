Shader "ffWater" { 
Properties {
	_WaveScale ("Wave scale", Range (0.001,0.15)) = 0.063
	_ReflDistort ("Reflection distort", Range (0,1.5)) = 0.44
	_RefrDistort ("Refraction distort", Range (0,1.5)) = 0.40
	_RefrColor ("Refraction color", COLOR)  = ( .34, .85, .92, 1)
	_Fresnel ("Fresnel (A) ", 2D) = "" {}
	_BumpMap ("Bumpmap (RGB) ", 2D) = "" {}
	WaveSpeed ("Wave speed (map1 x,y; map2 x,y)", Vector) = (19,9,-16,-7)
	_ReflectiveColor ("Reflective color (RGB) fresnel (A) ", 2D) = "" {}
	_ReflectiveColorCube ("Reflective color cube (RGB) fresnel (A)", Cube) = "" { TexGen CubeReflect }
	_HorizonColor ("Simple water horizon color", COLOR)  = ( .172, .463, .435, 1)
	_FoamColor ("foam color", COLOR)  = ( 1, 1, 1, 1)
	_MainTex ("Fallback texture", 2D) = "" {}
	_ReflectionTex ("Internal Reflection", 2D) = "" {}
	_RefractionTex ("Internal Refraction", 2D) = "" {}
}


// -----------------------------------------------------------
// Fragment program cards

Subshader {
	Tags {
		"WaterMode" = "Reflective"
		"Queue" = "Transparent+2" // foam uses 110
	}
	Pass {
		//ZWrite Off
		ColorMask RGB
		Blend SrcAlpha OneMinusSrcAlpha
		
Program "" {
// Vertex combos: 1, instructions: 14 to 14
// Fragment combos: 1, instructions: 15 to 15, texreads: 3 to 3
SubProgram "opengl " {
Keywords { }
Bind "vertex", Vertex
Bind "normal", Normal
Local 1, [_Time]
Local 2, [_ObjectSpaceCameraPos]
Local 3, [WaveSpeed]
Local 4, ([_WaveScale],0,0,0)
"!!ARBvp1.0
# 14 instructions
PARAM c[9] = { { 0.40000001, 0.44999999, 0 },
		program.local[1..4],
		state.matrix.mvp };
TEMP R0;
MOV R0, c[3];
MAD R0, R0, c[1].x, vertex.position.xzxz;
MUL R0, R0, c[4].x;
MUL result.texcoord[0].xy, R0, c[0];
DP4 R0.x, vertex.position, c[7];
MOV result.texcoord[1].xy, R0.wzzw;
ADD result.texcoord[2].xyz, -vertex.position.xzyw, c[2].xzyw;
DP4 result.position.w, vertex.position, c[8];
MOV result.position.z, R0.x;
DP4 result.position.y, vertex.position, c[6];
DP4 result.position.x, vertex.position, c[5];
MOV result.fogcoord.x, R0;
MOV result.texcoord[2].w, c[0].z;
MOV result.texcoord[3].x, vertex.normal.y;
END
# 14 instructions, 1 R-regs
"
}

SubProgram "d3d9 " {
Keywords { }
Bind "vertex", Vertex
Bind "normal", Normal
Local 4, [_Time]
Local 5, [_ObjectSpaceCameraPos]
Local 6, [WaveSpeed]
Local 7, ([_WaveScale],0,0,0)
Matrix 0, [glstate_matrix_mvp]
"vs_1_1
def c8, 0.40000001, 0.44999999, 0.00000000, 0
dcl_position v0
dcl_normal v1
mov r0.x, c4
mad r0, c6, r0.x, v0.xzxz
mul r0, r0, c7.x
mul oT0.xy, r0, c8
dp4 r0.x, v0, c2
mov oT1.xy, r0.wzzw
add oT2.xyz, -v0.xzyw, c5.xzyw
dp4 oPos.w, v0, c3
mov oPos.z, r0.x
dp4 oPos.y, v0, c1
dp4 oPos.x, v0, c0
mov oFog, r0.x
mov oT2.w, c8.z
mov oT3.x, v1.y
"
}

SubProgram "opengl " {
Keywords { }
Local 0, [_HorizonColor]
Local 1, [_FoamColor]
SetTexture [_BumpMap] {2D}
SetTexture [_ReflectiveColor] {2D}
"!!ARBfp1.0
OPTION ARB_precision_hint_fastest;
OPTION ARB_fog_exp2;
# 15 instructions, 3 texture reads
PARAM c[3] = { program.local[0..1],
		{ 1, 2 } };
TEMP R0;
TEMP R1;
TEX R0.xyz, fragment.texcoord[0], texture[0], 2D;
TEX R1.xyz, fragment.texcoord[1], texture[0], 2D;
ADD R1.xyz, R0, R1;
DP3 R0.w, fragment.texcoord[2], fragment.texcoord[2];
RSQ R0.x, R0.w;
MUL R0.xyz, R0.x, fragment.texcoord[2];
ADD R1.xyz, R1, -c[2].x;
DP3 R0.x, R0, R1;
TEX R0, R0.x, texture[1], 2D;
ADD R1.xyz, -R0, c[0];
MAD R0.xyz, R0.w, R1, R0;
ADD R0.xyz, R0, -c[1];
MUL R0.w, R0, c[0];
MAD result.color.xyz, fragment.texcoord[3].x, R0, c[1];
MUL result.color.w, R0, c[2].y;
END
# 15 instructions, 2 R-regs
"
}

SubProgram "d3d9 " {
Keywords { }
Local 0, [_HorizonColor]
Local 1, [_FoamColor]
SetTexture [_BumpMap] {2D}
SetTexture [_ReflectiveColor] {2D}
"ps_2_0
dcl_2d s0
dcl_2d s1
def c2, -1.00000000, 2.00000000, 0, 0
dcl t0.xy
dcl t1.xy
dcl t2.xyz
dcl t3.x
texld r1, t1, s0
texld r2, t0, s0
dp3 r0.x, t2, t2
rsq r0.x, r0.x
mul r0.xyz, r0.x, t2
add_pp r1.xyz, r2, r1
add_pp r1.xyz, r1, c2.x
dp3 r0.x, r0, r1
mov r0.xy, r0.x
texld r0, r0, s1
add_pp r1.xyz, -r0, c0
mad_pp r0.xyz, r0.w, r1, r0
add r1.xyz, r0, -c1
mul r0.x, r0.w, c0.w
mad r1.xyz, t3.x, r1, c1
mul r1.w, r0.x, c2.y
mov_pp oC0, r1
"
}

}
#LINE 104


	}
}

// -----------------------------------------------------------
// Radeon 9000 cards

Subshader {
	Tags {
		"WaterMode" = "Reflective"
		"Queue" = "Transparent+2" // foam uses -110
	}
	Pass {
		//ZWrite Off
		ColorMask RGB
		Blend SrcAlpha OneMinusSrcAlpha
	
Program "" {
// Vertex combos: 1, instructions: 24 to 24
SubProgram "opengl " {
Keywords { }
Bind "vertex", Vertex
Local 1, [_Time]
Local 2, [_ProjectionParams]
Local 3, [_ObjectSpaceCameraPos]
Local 4, [WaveSpeed]
Local 5, ([_WaveScale],0,0,0)
"!!ARBvp1.0
# 24 instructions
PARAM c[10] = { { 0, 0.5, 0.40000001, 0.44999999 },
		program.local[1..5],
		state.matrix.mvp };
TEMP R0;
TEMP R1;
TEMP R2;
TEMP R3;
DP4 R2.w, vertex.position, c[9];
DP4 R3.x, vertex.position, c[8];
ADD R2.xyz, -vertex.position, c[3];
MOV R1.y, c[0];
MOV R0.w, R2;
MOV R0.z, R3.x;
DP4 R0.y, vertex.position, c[7];
DP4 R0.x, vertex.position, c[6];
MOV R1.xzw, c[0].xyxy;
MUL R1.y, R1, c[2].x;
DP4 result.texcoord[3].y, R0, R1;
MOV R1, c[4];
MAD R1, R1, c[1].x, vertex.position.xzxz;
MUL R1, R1, c[5].x;
DP3 R3.y, R2, R2;
MUL result.texcoord[0].xy, R1, c[0].zwzw;
RSQ R1.x, R3.y;
MOV result.position, R0;
MOV result.texcoord[1].xy, R1.wzzw;
MUL result.texcoord[2].xyz, R1.x, R2.xzyw;
DP4 result.texcoord[3].z, R0, c[0].xxyy;
DP4 result.texcoord[3].x, R0, c[0].yxxy;
MOV result.fogcoord.x, R3;
MOV result.texcoord[3].w, R2;
END
# 24 instructions, 4 R-regs
"
}

SubProgram "d3d9 " {
Keywords { }
Bind "vertex", Vertex
Local 4, [_Time]
Local 5, [_ProjectionParams]
Local 6, [_ObjectSpaceCameraPos]
Local 7, [WaveSpeed]
Local 8, ([_WaveScale],0,0,0)
Matrix 0, [glstate_matrix_mvp]
"vs_1_1
def c9, 0.40000001, 0.44999999, 0.50000000, 0.00000000
dcl_position v0
dp4 r2.w, v0, c3
dp4 r3.x, v0, c2
add r2.xyz, -v0, c6
mov r1.y, c5.x
mov r0.w, r2
mov r0.z, r3.x
dp4 r0.y, v0, c1
dp4 r0.x, v0, c0
mov r1.xzw, c9.wywz
mul r1.y, c9.z, r1
dp4 oT3.y, r0, r1
mov r1.x, c4
mad r1, c7, r1.x, v0.xzxz
mul r1, r1, c8.x
dp3 r3.y, r2, r2
mul oT0.xy, r1, c9
rsq r1.x, r3.y
mov oPos, r0
mov oT1.xy, r1.wzzw
mul oT2.xyz, r1.x, r2.xzyw
dp4 oT3.z, r0, c9.wwzz
dp4 oT3.x, r0, c9.zwwz
mov oFog, r3.x
mov oT3.w, r2
"
}

}
#LINE 168


Program "" {
SubProgram {
	Keywords { "WATER_SIMPLE" }
	SetTexture [_BumpMap] { 2D }
	SetTexture [_BumpMap] { 2D }
	SetTexture [_ReflectiveColor] { 2D }
	Local 0, [_HorizonColor]

"!!ATIfs1.0
StartConstants;
	CONSTANT c0 = program.local[0];
EndConstants;

StartPrelimPass;
	SampleMap r0, t0.str;
	SampleMap r1, t1.str;
	PassTexCoord r2, t2.str;
	
	ADD r1, r0.bias, r1.bias;	# bump = bump1 + bump2 - 1
	DOT3 r2, r1, r2;			# fresnel: dot (bump, viewer-pos)
EndPass;

StartOutputPass;
 	SampleMap r2, r2.str;

	#MOV r0.rgb, r2;
	LERP r0.rgb, r2.a, c0, r2;	# fade in reflection
	MOV r0.a, c0.a;
EndPass;
"
#LINE 199
 
}
}
	}
}

// -----------------------------------------------------------
//  Old cards

// three texture, cubemaps
Subshader {
	Tags {
		"WaterMode" = "Reflective"
		"Queue" = "Transparent+2" // foam uses 110
	}
	Pass {
		//ZWrite Off
		ColorMask RGB
		Blend SrcAlpha OneMinusSrcAlpha
		Color (0.5,0.5,0.5,0.3)
		SetTexture [_MainTex] {
			Matrix [_WaveMatrix]
			combine texture * primary
		}
		SetTexture [_MainTex] {
			Matrix [_WaveMatrix2]
			combine texture * primary + previous
		}
		SetTexture [_ReflectiveColorCube] {
			combine texture * previous, primary
			Matrix [_Reflection]
		}
	}
}

// single texture, cubemaps
Subshader {
	Tags {
		"WaterMode" = "Reflective"
		"Queue" = "Transparent+2" // foam uses 110
	}
	Pass {
		//ZWrite Off
		ColorMask RGB
		Blend SrcAlpha OneMinusSrcAlpha
		Color [_HorizonColor]
		SetTexture [_ReflectiveColorCube] {
			combine texture, primary
			Matrix [_Reflection]
		}
	}
}

// single texture
Subshader {
	Tags {
		"WaterMode" = "Reflective"
		"Queue" = "Transparent+2" // foam uses 110
	}
	Pass {
		//ZWrite Off
		ColorMask RGB
		Blend SrcAlpha OneMinusSrcAlpha
		Color [_HorizonColor]
		SetTexture [_MainTex] {
			Matrix [_WaveMatrix]
			combine texture, primary
		}
	}
}


}
