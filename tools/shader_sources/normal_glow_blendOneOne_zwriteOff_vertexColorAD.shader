Shader "normal_glow_blendOneOne_zwriteOff_vertexColorAD" {
Properties {
	_Color ("Main Color", Color) = (1,1,1,1)
	_SpecColor ("Spec Color", Color) = (1,1,1,1)
	_AmbColor ("Ambient Color", Color) = (1,1,1,1)
	_Emission ("Emmisive Color", Color) = (0,0,0,0)
	_Shininess ("Shininess", Range (0.01, 1)) = 0.7
	_MainTex ("Base (RGB)", 2D) = "white" {}
	_BumpMap ("Glow (RGB)", 2D) = "black" {}
}

SubShader {
	Tags {"Queue"="Transparent+1"}	
	Blend One One
	ZWrite Off
	ColorMaterial AmbientAndDiffuse				

	Pass {
		Cull Off
		Material {
			Diffuse [_Color]
			Ambient [_AmbColor]
			Shininess [_Shininess]
			//	Specular [_SpecColor]
			Emission [_Emission]
		} 
		Lighting On
		SetTexture [_BumpMap] {
			constantColor (.5,.5,.5)
			combine constant lerp (texture) previous
		}
		SetTexture [_MainTex] {
			Combine texture * previous , texture*primary
		}
	}
}
}