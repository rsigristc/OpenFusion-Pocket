Shader "Custom/RimEmissiveColoredTransparentTextureShaderNoScroll" {
    Properties {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Transparency ("Transparency", Range(0, 1)) = 0.5
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _Outline ("Outline width", Range (.002, 0.03)) = .005
        _Emission ("Emission", Range(0, 10)) = 1
        _RimPower ("Rim Power", Range(0.1, 10)) = 3
        _RimIntensity ("Rim Intensity", Range(0.1, 10)) = 3
        _RimColor ("Rim Color", Color) = (1, 1, 1, 1)
    }
    SubShader {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Pass {
            Cull Back
            Program "" {
// Vertex combos: 1
//   opengl - ALU: 21 to 21
//   d3d9 - ALU: 21 to 21
// Fragment combos: 1
//   opengl - ALU: 10 to 10, TEX: 1 to 1
//   d3d9 - ALU: 13 to 13, TEX: 1 to 1
SubProgram "opengl " {
Keywords { }
Bind "vertex", Vertex
Bind "texcoord", TexCoord0
Bind "normal", Normal
Matrix 1, [_Object2World]
"!!ARBvp1.0
# 21 ALU
PARAM c[13] = { program.local[0..4],
		state.matrix.modelview[0],
		state.matrix.mvp };
TEMP R0;
TEMP R1;
MOV R0.z, c[7].w;
MOV R0.y, c[6].w;
MOV R0.x, c[5].w;
DP4 R1.z, vertex.position, c[3];
DP4 R1.x, vertex.position, c[1];
DP4 R1.y, vertex.position, c[2];
ADD R1.xyz, -R0, -R1;
DP3 R1.w, R1, R1;
RSQ R1.w, R1.w;
DP3 R0.z, vertex.normal, c[7];
DP3 R0.x, vertex.normal, c[5];
DP3 R0.y, vertex.normal, c[6];
DP3 R0.w, R0, R0;
RSQ R0.w, R0.w;
MUL result.texcoord[1].xyz, R1.w, R1;
MUL result.texcoord[2].xyz, R0.w, R0;
MOV result.texcoord[0].xy, vertex.texcoord[0];
DP4 result.position.w, vertex.position, c[12];
DP4 result.position.z, vertex.position, c[11];
DP4 result.position.y, vertex.position, c[10];
DP4 result.position.x, vertex.position, c[9];
END
# 21 instructions, 2 R-regs
"
}

SubProgram "d3d9 " {
Keywords { }
Bind "vertex", Vertex
Bind "texcoord", TexCoord0
Bind "normal", Normal
Matrix 0, [_Object2World]
Matrix 4, [glstate_matrix_modelview0]
Matrix 8, [glstate_matrix_mvp]
"vs_1_1
; 21 ALU
dcl_position v0
dcl_texcoord0 v1
dcl_normal v2
mov r0.z, c6.w
mov r0.y, c5.w
mov r0.x, c4.w
dp4 r1.z, v0, c2
dp4 r1.x, v0, c0
dp4 r1.y, v0, c1
add r1.xyz, -r0, -r1
dp3 r1.w, r1, r1
rsq r1.w, r1.w
dp3 r0.z, v2, c6
dp3 r0.x, v2, c4
dp3 r0.y, v2, c5
dp3 r0.w, r0, r0
rsq r0.w, r0.w
mul oT1.xyz, r1.w, r1
mul oT2.xyz, r0.w, r0
mov oT0.xy, v1
dp4 oPos.w, v0, c11
dp4 oPos.z, v0, c10
dp4 oPos.y, v0, c9
dp4 oPos.x, v0, c8
"
}

SubProgram "opengl " {
Keywords { }
Local 0, [_Color]
Local 1, ([_Transparency],0,0,0)
Local 2, ([_Emission],0,0,0)
Local 3, ([_RimPower],0,0,0)
Local 4, ([_RimIntensity],0,0,0)
Local 5, [_RimColor]
SetTexture [_MainTex] {2D}
"!!ARBfp1.0
# 10 ALU, 1 TEX
PARAM c[7] = { program.local[0..5],
		{ 1 } };
TEMP R0;
TEMP R1;
TEX R0.xyz, fragment.texcoord[0], texture[0], 2D;
MOV R1.xyz, fragment.texcoord[2];
DP3_SAT R0.w, fragment.texcoord[1], R1;
ADD R0.w, -R0, c[6].x;
POW R0.w, R0.w, c[3].x;
MUL R1.xyz, R0.w, c[5];
MUL R1.xyz, R1, c[4].x;
MUL R0.xyz, R0, c[0];
MAD result.color.xyz, R0, c[2].x, R1;
MOV result.color.w, c[1].x;
END
# 10 instructions, 2 R-regs
"
}

SubProgram "d3d9 " {
Keywords { }
Local 0, [_Color]
Local 1, ([_Transparency],0,0,0)
Local 2, ([_Emission],0,0,0)
Local 3, ([_RimPower],0,0,0)
Local 4, ([_RimIntensity],0,0,0)
Local 5, [_RimColor]
SetTexture [_MainTex] {2D}
"ps_2_0
; 13 ALU, 1 TEX
dcl_2d s0
def c6, 1.00000000, 0, 0, 0
dcl t0.xy
dcl t1.xyz
dcl t2.xyz
texld r2, t0, s0
mov r0.xyz, t2
dp3_sat r0.x, t1, r0
add r0.x, -r0, c6
pow r1, r0.x, c3.x
mov r0.x, r1
mul r0.xyz, r0.x, c5
mul r0.xyz, r0, c4.x
mul r1.xyz, r2, c0
mad r0.xyz, r1, c2.x, r0
mov r0.w, c1.x
mov oC0, r0
"
}

}
#LINE 65

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
MUL R0.xyz, vertex.normal, R0.x;
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
mul r0.xyz, v1, r0.x
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
#LINE 157

        //Color (0,0,0,0)
            Cull Front
            ZWrite On
            ColorMask RGBA
            Blend SrcAlpha OneMinusSrcAlpha
            SetTexture [_MainTex] { combine primary }
        }
    }
}