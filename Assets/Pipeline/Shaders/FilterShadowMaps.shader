Shader "Hidden/FilterShadowMaps"
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
		#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ImageBasedLighting.hlsl"
		#include "PrefilteredShadowMaps.hlsl"
		
		float4 _MainLightShadowmapTexture_TexelSize;

#ifdef _SHADOW_MAPS_FLOAT
		TEXTURE2D_FLOAT(_MainTex);
#else
		TEXTURE2D_HALF(_MainTex);
#endif
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

		static const float coeff7[7] = { 0.00598,0.060626,0.241843,0.383103,0.241843,0.060626,0.00598};

		float4 conv2Taps(float w0, float4 x, float w1, float4 y)
		{
			return (x + log(w0 + w1 * exp(y - x)));
		}

		float4 gaussian1D7Taps(float2 uv)
		{
#ifdef _FIRST_FILTERING
			float2 txs = _MainLightShadowmapTexture_TexelSize.xy * _HorizontalVertical;
			float s0 = SAMPLE_TEXTURE2D(_MainLightShadowmapTexture, sm_point_clamp_sampler, saturate(uv - 3 * txs)).r;
			float s1 = SAMPLE_TEXTURE2D(_MainLightShadowmapTexture, sm_point_clamp_sampler, saturate(uv - 2 * txs)).r;
			float s2 = SAMPLE_TEXTURE2D(_MainLightShadowmapTexture, sm_point_clamp_sampler, saturate(uv - 1 * txs)).r;
			float s3 = SAMPLE_TEXTURE2D(_MainLightShadowmapTexture, sm_point_clamp_sampler, uv).r;
			float s4 = SAMPLE_TEXTURE2D(_MainLightShadowmapTexture, sm_point_clamp_sampler, saturate(uv + 1 * txs)).r;
			float s5 = SAMPLE_TEXTURE2D(_MainLightShadowmapTexture, sm_point_clamp_sampler, saturate(uv + 2 * txs)).r;
			float s6 = SAMPLE_TEXTURE2D(_MainLightShadowmapTexture, sm_point_clamp_sampler, saturate(uv + 3 * txs)).r;
#if defined(_EXP_VARIANCE_SHADOW_MAPS) || defined(_EXPONENTIAL_SHADOW_MAPS)
			s0 = s0 * 2.0f - 1.0f;
			s1 = s1 * 2.0f - 1.0f;
			s2 = s2 * 2.0f - 1.0f;
			s3 = s3 * 2.0f - 1.0f;
			s4 = s4 * 2.0f - 1.0f;
			s5 = s5 * 2.0f - 1.0f;
			s6 = s6 * 2.0f - 1.0f;
#endif
#ifdef _EXP_VARIANCE_SHADOW_MAPS
			float4 v0 = exp(float4(s0, -s0, 2 * s0, -2 * s0) * _EVSMExponent.xyxy);
			float4 v1 = exp(float4(s1, -s1, 2 * s1, -2 * s1) * _EVSMExponent.xyxy);
			float4 v2 = exp(float4(s2, -s2, 2 * s2, -2 * s2) * _EVSMExponent.xyxy);
			float4 v3 = exp(float4(s3, -s3, 2 * s3, -2 * s3) * _EVSMExponent.xyxy);
			float4 v4 = exp(float4(s4, -s4, 2 * s4, -2 * s4) * _EVSMExponent.xyxy);
			float4 v5 = exp(float4(s5, -s5, 2 * s5, -2 * s5) * _EVSMExponent.xyxy);
			float4 v6 = exp(float4(s6, -s6, 2 * s6, -2 * s6) * _EVSMExponent.xyxy);
			float4 ev = v0 * coeff7[0] + v1 * coeff7[1] + v2 * coeff7[2] + v3 * coeff7[3] + v4 * coeff7[4] + v5 * coeff7[5] + v6 * coeff7[6];
			ev.y = -ev.y;
			return ev;
#elif defined(_EXPONENTIAL_SHADOW_MAPS)//_EXP_VARIANCE_SHADOW_MAPS
			s0 = s0 * _EVSMExponent.x;
			s1 = s1 * _EVSMExponent.x;
			s2 = s2 * _EVSMExponent.x;
			s3 = s3 * _EVSMExponent.x;
			s4 = s4 * _EVSMExponent.x;
			s5 = s5 * _EVSMExponent.x;
			s6 = s6 * _EVSMExponent.x;
#ifndef _ESM_LOG_FILTER
			s0 = exp(s0);
			s1 = exp(s1);
			s2 = exp(s2);
			s3 = exp(s3);
			s4 = exp(s4);
			s5 = exp(s5);
			s6 = exp(s6);
			return s0 * coeff7[0] + s1 * coeff7[1] + s2 * coeff7[2] + s3 * coeff7[3] + s4 * coeff7[4] + s5 * coeff7[5] + s6 * coeff7[6];;
#else //_ESM_LOG_FILTER
			float f0 = conv2Taps(coeff7[0], s0, coeff7[1], s1);
			float f1 = conv2Taps(coeff7[5], s5, coeff7[6], s6);
			f0 = conv2Taps(1.0, f0, coeff7[2], s2);
			f1 = conv2Taps(1.0, f1, coeff7[4], s4);
			f0 = conv2Taps(1.0, f1, coeff7[3], s3);
			f1 = conv2Taps(1.0, f0, 1.0, f1);
			return f1.xxxx;
#endif //_ESM_LOG_FILTER
#else //_EXP_VARIANCE_SHADOW_MAPS
			float e = s0 * coeff7[0] + s1 * coeff7[1] + s2 * coeff7[2] + s3 * coeff7[3] + s4 * coeff7[4] + s5 * coeff7[5] + s6 * coeff7[6];
			float v = s0 * s0 * coeff7[0] + s1 * s1 * coeff7[1] + s2 * s2 * coeff7[2] + s3 * s3 * coeff7[3] + s4 * s4 * coeff7[4] + s5 * s5 * coeff7[5] + s6 * s6 * coeff7[6];
			return float4(e, v, e, v);
#endif //_EXP_VARIANCE_SHADOW_MAPS
#else //_FIRST_FILTERING
			float2 txs = _MainTex_TexelSize.xy * _HorizontalVertical;
			float4 s0 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, saturate(uv - 3 * txs));
			float4 s1 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, saturate(uv - 2 * txs));
			float4 s2 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, saturate(uv - 1 * txs));
			float4 s3 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
			float4 s4 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, saturate(uv + 1 * txs));
			float4 s5 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, saturate(uv + 2 * txs));
			float4 s6 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, saturate(uv + 3 * txs));
#if defined(_EXPONENTIAL_SHADOW_MAPS) && defined(_ESM_LOG_FILTER)
			float4 f0 = conv2Taps(coeff7[0], s0, coeff7[1], s1);
			float4 f1 = conv2Taps(coeff7[5], s5, coeff7[6], s6);
			f0 = conv2Taps(1.0, f0, coeff7[2], s2);
			f1 = conv2Taps(1.0, f1, coeff7[4], s4);
			f0 = conv2Taps(1.0, f1, coeff7[3], s3);
			return conv2Taps(1.0, f0, 1.0, f1);
#else //defined(_EXPONENTIAL_SHADOW_MAPS) && defined(_ESM_LOG_FILTER)
			return s0 * coeff7[0] + s1 * coeff7[1] + s2 * coeff7[2] + s3 * coeff7[3] + s4 * coeff7[4] + s5 * coeff7[5] + s6 * coeff7[6];
#endif //defined(_EXPONENTIAL_SHADOW_MAPS) && defined(_ESM_LOG_FILTER)
#endif //_FIRST_FILTERING

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
				#pragma multi_compile _ _EXP_VARIANCE_SHADOW_MAPS _EXPONENTIAL_SHADOW_MAPS _VARIANCE_SHADOW_MAPS
				#pragma multi_compile _ _SHADOW_MAPS_FLOAT
				#pragma multi_compile _ _FIRST_FILTERING
				#pragma multi_compile _ _ESM_LOG_FILTER
				ENDHLSL
		}
	}
}
