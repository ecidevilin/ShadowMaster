namespace UnityEngine.Experimental.Rendering.LightweightPipeline.Extension
{
    public interface IAfterMainLightShadowCasterPass
    {
        ScriptableRenderPass GetPassToEnqueue(RenderTextureDescriptor baseDescriptor, RenderTargetHandle mainLightShadowmapHandle);
    }
}
