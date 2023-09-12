Shader "Hidden/Compositing"
{
	Properties
	{
	}

	HLSLINCLUDE

#pragma vertex Vert

#pragma target 4.5
#pragma only_renderers d3d11 ps4 xboxone vulkan metal switch

#include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/RenderPass/CustomPass/CustomPassCommon.hlsl"

	TEXTURE2D_X(_CustomBuffer);
	Texture2D _OverLayer;
	TEXTURE2D_X(_Background);

	float4 Compositing(Varyings varyings) : SV_Target
	{
		float depth = LoadCameraDepth(varyings.positionCS.xy);
		PositionInputs posInput = GetPositionInput(varyings.positionCS.xy, _ScreenSize.zw, depth, UNITY_MATRIX_I_VP, UNITY_MATRIX_V);

		float4 cg = SAMPLE_TEXTURE2D_X_LOD(_Background, s_point_clamp_sampler, posInput.positionNDC.xy, 0);
		float4 overPixel = _OverLayer.Sample(s_point_clamp_sampler, posInput.positionNDC.xy);

		float4 col = (overPixel.a) * overPixel + (1.f - overPixel.a) * cg;
		col.a = 0;
		return col;
	}

	// We need this copy because we can't sample and write to the same render target (Camera color buffer)
	float4 Copy(Varyings varyings) : SV_Target
	{
		float depth = LoadCameraDepth(varyings.positionCS.xy);
		PositionInputs posInput = GetPositionInput(varyings.positionCS.xy, _ScreenSize.zw, depth, UNITY_MATRIX_I_VP, UNITY_MATRIX_V);

		return float4(LOAD_TEXTURE2D_X_LOD(_CustomBuffer, posInput.positionSS.xy, 0).rgb, 1);
	}

	ENDHLSL

	SubShader
	{
		Pass
		{
			Name "Compositing"

			ZWrite Off
			ZTest Always
			Blend Off
			Cull Off

			HLSLPROGRAM
				#pragma fragment Compositing
			ENDHLSL
		}

		Pass
		{
			Name "Copy"

			ZWrite Off
			ZTest Always
			Blend Off
			Cull Off

			HLSLPROGRAM
				#pragma fragment Copy
			ENDHLSL
		}
	}
	Fallback Off
}
