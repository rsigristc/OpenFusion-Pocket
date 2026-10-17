Shader "MiniMapRender" {
Properties {
	//_Color ("Main Color", Color) = (1,1,1,1)
	//_SpecColor ("Spec Color", Color) = (1,1,1,1)
	//_Emission ("Emmisive Color", Color) = (0,0,0,0)
	//_Shininess ("Shininess", Range (0.01, 1)) = 0.7
	_MainTex ("Base (RGB)", 2D) = "white" {}
	_DecalTex ("Decal (RGBA)", 2D) = "black" {}
}
Category {
	//Tags {"Queue"="Transparent"}
	Blend SrcAlpha OneMinusSrcAlpha 
	ColorMask RGBA
	ZWrite Off
	ZTest	Always
	Cull Off

	SubShader {

		Pass {
			Lighting Off
			SeparateSpecular Off
			SetTexture [_MainTex] {	Combine texture, texture }
			SetTexture [_DecalTex] {combine previous, texture}
		}
	}
}
}
