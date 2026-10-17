Shader "normal_blendSrcalphaOne_zwriteOff" {
Properties {
	_Color ("Main Color", Color) = (1,1,1,1)
	_SpecColor ("Spec Color", Color) = (1,1,1,1)
	_AmbColor ("Ambient Color", Color) = (1,1,1,1)
	_Emission ("Emmisive Color", Color) = (0,0,0,0)
	_Shininess ("Shininess", Range (0.01, 1)) = 0.7
	_MainTex ("Base (RGB)", 2D) = "white" {}
}

Category {
	Tags {"Queue"="Transparent+10"}

	Blend SrcAlpha One 
	ColorMask RGB
	ZWrite Off
	

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