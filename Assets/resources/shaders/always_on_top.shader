Shader "always_on_top_color" 
{
	Properties
	{
		_Color("Main Color", Color) = (1,1,1,1)
		_MainTex("Base (RGB)", 2D) = "white" {}
	}
		
	SubShader 
	{
		Cull Back
		Lighting Off
		ZWrite On
		ZTest Always

		Pass 
		{
			SetTexture[_MainTex] 
			{
				constantColor[_Color]
				Combine texture * constant
			}		
		}
	}
}