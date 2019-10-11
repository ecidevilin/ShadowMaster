using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine.Experimental.Rendering.LightweightPipeline.Extension
{
    public class ExponentialVarianceShadowMapsComponent : MonoBehaviour, IAfterDepthPrePass
    {
        [Range(1,80)]
        [SerializeField] private int _EVSMExponent;
        [SerializeField] private ShadowMapsType _ShadowMapsType;
        void OnEnable()
        {

        }

        public ScriptableRenderPass GetPassToEnqueue(RenderTextureDescriptor baseDescriptor, RenderTargetHandle depthAttachmentHandle)
        {
            ExponentialVarianceShadowMapsPass pass = new ExponentialVarianceShadowMapsPass();
            pass.Setup(baseDescriptor, depthAttachmentHandle, isActiveAndEnabled, _EVSMExponent, _ShadowMapsType);
            return pass;
        }
    }
}
