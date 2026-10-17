Shader "fusionStar" {
Properties {
	_MainTex ("Base (RGB)", 2D) = "white" {}
}

Category {
	Tags {"Queue"="Geometry-1000"}
	Blend SrcAlpha OneMinusSrcAlpha 
	ColorMask RGBA
	ZWrite Off
	

	SubShader {
		Pass {
			SetTexture [_MainTex] {
				Combine texture
			} 
		}
	}
}
}