Shader "normal_blendSrcalphaInvsrcalphaTest_zwriteOff_cullOff" {
Properties {
	_Color ("Main Color", Color) = (1,1,1,1)
	_SpecColor ("Spec Color", Color) = (1,1,1,1)
	_AmbColor ("Ambient Color", Color) = (1,1,1,1)
	_Emission ("Emmisive Color", Color) = (0,0,0,0)
	_Shininess ("Shininess", Range (0.01, 1)) = 0.7
	_MainTex ("Base (RGB)", 2D) = "white" {}
	_Cutoff ("Cutoff", Range (0, 1)) = 0.0
}

Category {
	Tags {"Queue"="Transparent-100"}

	//Blend SrcAlpha OneMinusSrcAlpha 
	ColorMask RGBA
	Alphatest Greater [_Cutoff]
	Cull Off
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
				Combine texture * primary DOUBLE, texture
			} 
		}
	}
}
}