Shader "Custom/VFX/DualScrollingAdditive" {
    Properties {
        _MainTex ("Texture 1", 2D) = "white" {}
        _Color1 ("Color 1", Color) = (1,1,1,1)
        _ScrollX1 ("Scroll X 1", Float) = 0.0
        _ScrollY1 ("Scroll Y 1", Float) = 0.0
       
        _MainTex2 ("Texture 2", 2D) = "white" {}
        _Color2 ("Color 2", Color) = (1,1,1,1)
        _ScrollX2 ("Scroll X 2", Float) = 0.0
        _ScrollY2 ("Scroll Y 2", Float) = 0.0
       
        _EmissionStrength ("Emission Strength", Float) = 1.0   // ← New control
        
        _SpecMap ("Spec Map", 2D) = "white" {} // Kept for script compatibility
       
        _MasterOpacity ("Master Opacity", Float) = 1.0
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
               
                Program "" {
// Vertex combos: 1
//   opengl - ALU: 6 to 6
//   d3d9 - ALU: 6 to 6
// Fragment combos: 1
//   opengl - ALU: 13 to 13, TEX: 2 to 2
//   d3d9 - ALU: 12 to 12, TEX: 2 to 2
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
Local 1, [_Color1]
Local 2, ([_ScrollX1],0,0,0)
Local 3, ([_ScrollY1],0,0,0)
Local 4, [_Color2]
Local 5, ([_ScrollX2],0,0,0)
Local 6, ([_ScrollY2],0,0,0)
Local 7, ([_EmissionStrength],0,0,0)
Local 8, ([_MasterOpacity],0,0,0)
SetTexture [_MainTex] {2D}
SetTexture [_MainTex2] {2D}
"!!ARBfp1.0
# 13 ALU, 2 TEX
PARAM c[9] = { program.local[0..8] };
TEMP R0;
TEMP R1;
TEMP R2;
MOV R0.y, c[6].x;
MOV R0.x, c[5];
MAD R0.zw, R0.xyxy, c[0].y, fragment.texcoord[0].xyxy;
MOV R0.y, c[3].x;
MOV R0.x, c[2];
MAD R0.xy, R0, c[0].y, fragment.texcoord[0];
MUL R2.x, fragment.color.primary.w, c[8];
TEX R1, R0.zwzw, texture[1], 2D;
TEX R0, R0, texture[0], 2D;
MUL R1, R1, c[4];
MAD R0, R0, c[1], R1;
MUL result.color.w, R0, R2.x;
MUL result.color.xyz, R0, c[7].x;
END
# 13 instructions, 3 R-regs
"
}

SubProgram "d3d9 " {
Keywords { }
Local 0, [_Time]
Local 1, [_Color1]
Local 2, ([_ScrollX1],0,0,0)
Local 3, ([_ScrollY1],0,0,0)
Local 4, [_Color2]
Local 5, ([_ScrollX2],0,0,0)
Local 6, ([_ScrollY2],0,0,0)
Local 7, ([_EmissionStrength],0,0,0)
Local 8, ([_MasterOpacity],0,0,0)
SetTexture [_MainTex] {2D}
SetTexture [_MainTex2] {2D}
"ps_2_0
; 12 ALU, 2 TEX
dcl_2d s0
dcl_2d s1
dcl t0.xy
dcl v0.xyzw
mov r0.y, c6.x
mov r0.x, c5
mad r0.xy, r0, c0.y, t0
mov r1.y, c3.x
mov r1.x, c2
mad r1.xy, r1, c0.y, t0
texld r0, r0, s1
texld r1, r1, s0
mul r0, r0, c4
mad r0, r1, c1, r0
mul r1.x, v0.w, c8
mul r0.xyz, r0, c7.x
mul r0.w, r0, r1.x
mov oC0, r0
"
}

}
#LINE 94

            }
        }
    }
   
    FallBack "Diffuse"
}