Shader "normal_blendSrcalphaOne_vertexColorAD" {
Properties {
	_Color ("Main Color", Color) = (1,1,1,1)
	_SpecColor ("Spec Color", Color) = (1,1,1,1)
	_AmbColor ("Ambient Color", Color) = (1,1,1,1)
	_Emission ("Emmisive Color", Color) = (0,0,0,0)
	_Shininess ("Shininess", Range (0.01, 1)) = 0.7
	_MainTex ("Base (RGB)", 2D) = "white" {}
}

Category {
	Tags {"Queue"="Transparent"}
	Blend SrcAlpha One 
	ColorMask RGB
	ColorMaterial AmbientAndDiffuse	
	
	SubShader {
		Pass {
			Material {
				Diffuse [_Color]
				Ambient [_AmbColor]
				Shininess [_Shininess]
				//	Specular [_SpecColor]
				Emission [_Emission]
			} 
			Lighting On
			SetTexture [_MainTex] {
				Combine texture * primary DOUBLE , texture
			} 
		}
	}
}
}