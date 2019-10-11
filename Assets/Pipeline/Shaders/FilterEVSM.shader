Shader "Hidden/FilterEVSM"
{
	Properties
	{
	}
	SubShader
	{
		Tags{ "RenderPipeline" = "LightweightPipeline" "IgnoreProjector" = "True"}

		HLSLINCLUDE

		#pragma prefer_hlslcc gles
		#pragma exclude_renderers d3d11_9x
		//Keep compiler quiet about Shadows.hlsl.
		#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
		#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"
		#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ImageBasedLighting.hlsl"
		#include "Packages/com.unity.render-pipelines.lightweight/ShaderLibrary/Core.hlsl"
		#include "Packages/com.unity.render-pipelines.lightweight/ShaderLibrary/Shadows.hlsl"

		#pragma multi_compile _ _FIRST_FILTERING

		SamplerState my_linear_clamp_sampler;
		float4 _MainLightShadowmapTexture_TexelSize;

		TEXTURE2D_FLOAT(_MainTex);
		SAMPLER(sampler_MainTex);
		float4 _MainTex_TexelSize;

		float2 _HorizontalVertical;

		struct Attributes
		{
			float4 positionOS   : POSITION;
			float2 texcoord : TEXCOORD0;
			UNITY_VERTEX_INPUT_INSTANCE_ID
		};

		struct Varyings
		{
			half4  positionCS   : SV_POSITION;
			half2  uv           : TEXCOORD0;
			UNITY_VERTEX_OUTPUT_STEREO
		};

		Varyings Vertex(Attributes input)
        {
			Varyings output;
			UNITY_SETUP_INSTANCE_ID(input);
			UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

			output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
			output.uv = input.texcoord;

            return output;
        }

		static const float coeff[7] = { 0.006, 0.061, 0.242, 0.382, 0.242, 0.061, 0.006 };
		
		float4 conv2Taps44(float w0, float4 x, float w1, float4 y)
		{
			return (x + log(w0 + w1 * exp(y - x)));
		}

		float4 conv2Taps11(float w0, float x, float w1, float y)
		{
			float4 xx = float4(x, -x, 2 * x, -2 * x);
			float4 yy = float4(y, -y, 2 * y, -2 * y);
			return conv2Taps44(w0, xx, w1, yy);
		}

		float4 conv2Taps41(float w0, float4 x, float w1, float y)
		{
			float4 yy = float4(y, -y, 2 * y, -2 * y);
			return conv2Taps44(w0, x, w1, yy);
		}

		float4 gaussian1D7Taps(float2 uv)
		{
#ifdef _EXP_VARIANCE_SHADOW_MAPS
#ifdef _FIRST_FILTERING
			float2 txs = _MainLightShadowmapTexture_TexelSize.xy * _HorizontalVertical;
			float s0 = SAMPLE_TEXTURE2D(_MainLightShadowmapTexture, my_linear_clamp_sampler, uv - 3 * txs).r;
			float s1 = SAMPLE_TEXTURE2D(_MainLightShadowmapTexture, my_linear_clamp_sampler, uv - 2 * txs).r;
			float s2 = SAMPLE_TEXTURE2D(_MainLightShadowmapTexture, my_linear_clamp_sampler, uv - 1 * txs).r;
			float s3 = SAMPLE_TEXTURE2D(_MainLightShadowmapTexture, my_linear_clamp_sampler, uv).r;
			float s4 = SAMPLE_TEXTURE2D(_MainLightShadowmapTexture, my_linear_clamp_sampler, uv + 1 * txs).r;
			float s5 = SAMPLE_TEXTURE2D(_MainLightShadowmapTexture, my_linear_clamp_sampler, uv + 2 * txs).r;
			float s6 = SAMPLE_TEXTURE2D(_MainLightShadowmapTexture, my_linear_clamp_sampler, uv + 3 * txs).r;
			s0 = s0 * 2.0f - 1.0f;
			s1 = s1 * 2.0f - 1.0f;
			s2 = s2 * 2.0f - 1.0f;
			s3 = s3 * 2.0f - 1.0f;
			s4 = s4 * 2.0f - 1.0f;
			s5 = s5 * 2.0f - 1.0f;
			s6 = s6 * 2.0f - 1.0f;
			s0 *= _EVSMExponent;
			s1 *= _EVSMExponent;
			s2 *= _EVSMExponent;
			s3 *= _EVSMExponent;
			s4 *= _EVSMExponent;
			s5 *= _EVSMExponent;
			s6 *= _EVSMExponent;
			float4 f0 = conv2Taps11(coeff[0], s0, coeff[1], s1);
			float4 f1 = conv2Taps11(coeff[5], s5, coeff[6], s6);
			f0 = conv2Taps41(1.0, f0, coeff[2], s2);
			f1 = conv2Taps41(1.0, f1, coeff[4], s4);
			f0 = conv2Taps41(1.0, f1, coeff[3], s3);
			return conv2Taps44(1.0, f0, 1.0, f1);
#else
			float2 txs = _MainTex_TexelSize.xy * _HorizontalVertical;
			float4 s0 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv - 3 * txs);
			float4 s1 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv - 2 * txs);
			float4 s2 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv - 1 * txs);
			float4 s3 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
			float4 s4 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + 1 * txs);
			float4 s5 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + 2 * txs);
			float4 s6 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + 3 * txs);
			float4 f0 = conv2Taps44(coeff[0], s0, coeff[1], s1);
			float4 f1 = conv2Taps44(coeff[5], s5, coeff[6], s6);
			f0 = conv2Taps44(1.0, f0, coeff[2], s2);
			f1 = conv2Taps44(1.0, f1, coeff[4], s4);
			f0 = conv2Taps44(1.0, f1, coeff[3], s3);
			return conv2Taps44(1.0, f0, 1.0, f1);
#endif //_FIRST_FILTERING
#else
#ifdef _FIRST_FILTERING
			float2 txs = _MainLightShadowmapTexture_TexelSize.xy * _HorizontalVertical;
			float s0 = SAMPLE_TEXTURE2D(_MainLightShadowmapTexture, my_linear_clamp_sampler, uv - 3 * txs).r;
			float s1 = SAMPLE_TEXTURE2D(_MainLightShadowmapTexture, my_linear_clamp_sampler, uv - 2 * txs).r;
			float s2 = SAMPLE_TEXTURE2D(_MainLightShadowmapTexture, my_linear_clamp_sampler, uv - 1 * txs).r;
			float s3 = SAMPLE_TEXTURE2D(_MainLightShadowmapTexture, my_linear_clamp_sampler, uv).r;
			float s4 = SAMPLE_TEXTURE2D(_MainLightShadowmapTexture, my_linear_clamp_sampler, uv + 1 * txs).r;
			float s5 = SAMPLE_TEXTURE2D(_MainLightShadowmapTexture, my_linear_clamp_sampler, uv + 2 * txs).r;
			float s6 = SAMPLE_TEXTURE2D(_MainLightShadowmapTexture, my_linear_clamp_sampler, uv + 3 * txs).r;
			float e = s0 * coeff[0] + s1 * coeff[1] + s2 * coeff[2] + s3 * coeff[3] + s4 * coeff[4] + s5 * coeff[5] + s6 * coeff[6];
			float v = s0 * s0 * coeff[0] + s1 * s1 * coeff[1] + s2 * s2 * coeff[2] + s3 * s3 * coeff[3] + s4 * s4 * coeff[4] + s5 * s5 * coeff[5] + s6 * s6 * coeff[6];
			return float4(e, v, e, v);
#else
			float2 txs = _MainTex_TexelSize.xy * _HorizontalVertical;
			float2 s0 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv - 3 * txs).rg;
			float2 s1 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv - 2 * txs).rg;
			float2 s2 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv - 1 * txs).rg;
			float2 s3 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).rg;
			float2 s4 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + 1 * txs).rg;
			float2 s5 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + 2 * txs).rg;
			float2 s6 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + 3 * txs).rg;
			float2 ev = s0 * coeff[0] + s1 * coeff[1] + s2 * coeff[2] + s3 * coeff[3] + s4 * coeff[4] + s5 * coeff[5] + s6 * coeff[6];
			return ev.xyxy;
#endif
#endif
			
		}

        half4 Fragment(Varyings input) : SV_Target
        {
			UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            return gaussian1D7Taps(input.uv);
        }
        ENDHLSL
		Pass
		{
				Name "FilterEVSM"
				ZTest Always
				ZWrite Off
				Cull Off

				HLSLPROGRAM

				#pragma vertex   Vertex
				#pragma fragment Fragment
				#pragma multi_compile _ _EXP_VARIANCE_SHADOW_MAPS _VARIANCE_SHADOW_MAPS
				ENDHLSL
		}
    }
}
