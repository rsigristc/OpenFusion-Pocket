Shader "normal_blendSrcalphaInvsrcalphaTest_cullOff" {
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
	Cull Off
	Material {
		Diffuse [_Color]
		Ambient [_AmbColor]
		Shininess [_Shininess]
		//	Specular [_SpecColor]
		Emission [_Emission]
	} 
	Lighting On


	SubShader {
		LOD 300
		Pass {
			AlphaTest GEqual 0.9
			ColorMask RGBA
			SetTexture [_MainTex] {
				Combine texture * primary DOUBLE, texture
			} 
		}
		Pass {
			Blend SrcAlpha OneMinusSrcAlpha 
			ColorMask RGB
			ZWrite Off
			SetTexture [_MainTex] {
				Combine texture * primary DOUBLE, texture
			} 
		}
	}

	SubShader {
		Pass {
			AlphaTest Greater [_Cutoff]
			ColorMask RGBA
			SetTexture [_MainTex] {
				Combine texture * primary DOUBLE, texture
			} 
		}
	}
}
}