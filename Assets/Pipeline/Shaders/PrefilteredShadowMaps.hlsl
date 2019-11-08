#ifndef LIGHTWEIGHT_EXT_EVSM_INCLUDED
#define LIGHTWEIGHT_EXT_EVSM_INCLUDED

#include "Packages/com.unity.render-pipelines.lightweight/ShaderLibrary/Shadows.hlsl"

CBUFFER_START(_EVSM)
float2 _EVSMExponent;
CBUFFER_END
#ifdef _SHADOW_MAPS_FLOAT
TEXTURE2D_FLOAT(_FilteredMainLightSM);
#else
TEXTURE2D_HALF(_FilteredMainLightSM);
#endif
SAMPLER(sampler_FilteredMainLightSM);
SamplerState sm_point_clamp_sampler;


float ChebyshevUpperBound(float2 moments, float mean, float minV)
{
	float v = moments.y - moments.x * moments.x;
	v = max(v, minV);
	float d = mean - moments.x;
	float p = v / (v + d * d);
	return max(mean > moments.x, p);
}
real SampleFilteredSM(float4 shadowCoord, TEXTURE2D_SHADOW_ARGS(ShadowMap, sampler_ShadowMap), ShadowSamplingData samplingData, half shadowStrength, bool isPerspectiveProjection = true)
{
    // Compiler will optimize this branch away as long as isPerspectiveProjection is known at compile time
    if (isPerspectiveProjection)
        shadowCoord.xyz /= shadowCoord.w;
#ifdef _EXP_VARIANCE_SHADOW_MAPS
	float4 evsm = SAMPLE_TEXTURE2D(_FilteredMainLightSM, sampler_FilteredMainLightSM, shadowCoord.xy);

	float shadowDepth = shadowCoord.z;
	shadowDepth = shadowDepth * 2.0f - 1.0f;
	float2 warpedDepth = shadowDepth;
	warpedDepth = exp2(warpedDepth * _EVSMExponent);
	warpedDepth.y = -warpedDepth.y;

	float2 depthScale = 0.000001f * _EVSMExponent * warpedDepth;
	float2 minVariance = depthScale * depthScale;

	float pc = ChebyshevUpperBound(evsm.xz, warpedDepth.x, minVariance.x);
	float nc = ChebyshevUpperBound(evsm.yw, warpedDepth.y, minVariance.y);

	real attenuation = min(pc, nc);

	attenuation = LerpWhiteTo(attenuation, shadowStrength);

	// Shadow coords that fall out of the light frustum volume must always return attenuation 1.0
	return BEYOND_SHADOW_FAR(shadowCoord) ? 1.0 : attenuation;
#elif defined(_EXPONENTIAL_SHADOW_MAPS)

	float shadowDepth = shadowCoord.z;
	shadowDepth = shadowDepth * 2.0f - 1.0f;
	shadowDepth = shadowDepth * _EVSMExponent.x;
#ifdef _ESM_LOG_FILTER
	float esm = SAMPLE_TEXTURE2D(_FilteredMainLightSM, sm_point_clamp_sampler, shadowCoord.xy).r;
	real attenuation = exp(shadowDepth - esm);
#else
	float esm = SAMPLE_TEXTURE2D(_FilteredMainLightSM, sampler_FilteredMainLightSM, shadowCoord.xy).r;
	real attenuation = exp(shadowDepth) / esm;
#endif
	attenuation = LerpWhiteTo(attenuation, shadowStrength);

	// Shadow coords that fall out of the light frustum volume must always return attenuation 1.0
	return BEYOND_SHADOW_FAR(shadowCoord) ? 1.0 : attenuation;
#elif defined(_VARIANCE_SHADOW_MAPS)
	float2 vsm = SAMPLE_TEXTURE2D(_FilteredMainLightSM, sampler_FilteredMainLightSM, shadowCoord.xy).rg;
	float attenuation = ChebyshevUpperBound(vsm, shadowCoord.z, 0.000001f);
	
	attenuation = LerpWhiteTo(attenuation, shadowStrength);

	// Shadow coords that fall out of the light frustum volume must always return attenuation 1.0
	return BEYOND_SHADOW_FAR(shadowCoord) ? 1.0 : attenuation;
#else
    return SampleShadowmap(shadowCoord, TEXTURE2D_PARAM(ShadowMap, sampler_ShadowMap), samplingData, shadowStrength, false);
#endif
}

half MainLightRealtimeWithFilteredSM(float4 shadowCoord)
{
#if !defined(_MAIN_LIGHT_SHADOWS) || defined(_RECEIVE_SHADOWS_OFF)
    return 1.0h;
#endif

#if SHADOWS_SCREEN
    return SampleScreenSpaceShadowmap(shadowCoord);
#else
    ShadowSamplingData shadowSamplingData = GetMainLightShadowSamplingData();
    half shadowStrength = GetMainLightShadowStrength();
    return SampleFilteredSM(shadowCoord, TEXTURE2D_PARAM(_MainLightShadowmapTexture, sampler_MainLightShadowmapTexture), shadowSamplingData, shadowStrength, false);
#endif
}


#endif
