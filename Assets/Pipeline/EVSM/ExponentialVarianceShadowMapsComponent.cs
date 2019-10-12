using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine.Experimental.Rendering.LightweightPipeline.Extension
{
    public class ExponentialVarianceShadowMapsComponent : MonoBehaviour, IAfterDepthPrePass
    {
        [Range(1,45)]
        [SerializeField] private int _EVSMExponentPos = 10;
        [Range(1, 45)]
        [SerializeField] private int _EVSMExponentNeg = 20;
        [SerializeField] private ShadowMapsType _ShadowMapsType = ShadowMapsType.VSM;
        void OnEnable()
        {

        }

        public ScriptableRenderPass GetPassToEnqueue(RenderTextureDescriptor baseDescriptor, RenderTargetHandle depthAttachmentHandle)
        {
            ExponentialVarianceShadowMapsPass pass = new ExponentialVarianceShadowMapsPass();
            pass._Enabled = isActiveAndEnabled;
            pass._EVSMExponent = new Vector2(_EVSMExponentPos, _EVSMExponentNeg);
            pass._ShadowMapsType = _ShadowMapsType;
            pass.Setup(baseDescriptor, depthAttachmentHandle);
            return pass;
        }
    }
}
