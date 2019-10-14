#ifndef LIGHTWEIGHT_FORWARD_LIT_WITH_EVSM_PASS_INCLUDED
#define LIGHTWEIGHT_FORWARD_LIT_WITH_EVSM_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.lightweight/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.lightweight/Shaders/LitForwardPass.hlsl"
#include "PrefilteredShadowMaps.hlsl"


Light GetMainLightWithFilteredSM(float4 shadowCoord)
{
	Light light = GetMainLight();
	light.shadowAttenuation = MainLightRealtimeWithFilteredSM(shadowCoord);
	return light;
}

half4 LightweightFragmentPBRWithFilteredSM(InputData inputData, half3 albedo, half metallic, half3 specular,
	half smoothness, half occlusion, half3 emission, half alpha)
{
	BRDFData brdfData;
	InitializeBRDFData(albedo, metallic, specular, smoothness, alpha, brdfData);

	Light mainLight = GetMainLightWithFilteredSM(inputData.shadowCoord);
	MixRealtimeAndBakedGI(mainLight, inputData.normalWS, inputData.bakedGI, half4(0, 0, 0, 0));

	half3 color = GlobalIllumination(brdfData, inputData.bakedGI, occlusion, inputData.normalWS, inputData.viewDirectionWS);
	color += LightingPhysicallyBased(brdfData, mainLight, inputData.normalWS, inputData.viewDirectionWS);

#ifdef _ADDITIONAL_LIGHTS
	int pixelLightCount = GetAdditionalLightsCount();
	for (int i = 0; i < pixelLightCount; ++i)
	{
		Light light = GetAdditionalLight(i, inputData.positionWS);
		color += LightingPhysicallyBased(brdfData, light, inputData.normalWS, inputData.viewDirectionWS);
	}
#endif

#ifdef _ADDITIONAL_LIGHTS_VERTEX
	color += inputData.vertexLighting * brdfData.diffuse;
#endif

	color += emission;
	return half4(color, alpha);
}

half4 LitWithFilteredSMPassFragment(Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    SurfaceData surfaceData;
    InitializeStandardLitSurfaceData(input.uv, surfaceData);

    InputData inputData;
    InitializeInputData(input, surfaceData.normalTS, inputData);

    half4 color = LightweightFragmentPBRWithFilteredSM(inputData, surfaceData.albedo, surfaceData.metallic, surfaceData.specular, surfaceData.smoothness, surfaceData.occlusion, surfaceData.emission, surfaceData.alpha);

    color.rgb = MixFog(color.rgb, inputData.fogCoord);
    return color;
}

#endif
