using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine.Experimental.Rendering.LightweightPipeline
{
    public class PrefilteredShadowMapsComponent : MonoBehaviour, IAfterDepthPrePass
    {
        [Range(1,45)]
        [SerializeField] private int _EVSMExponentPos = 10;
        [Range(1, 45)]
        [SerializeField] private int _EVSMExponentNeg = 20;
        [SerializeField] private ShadowMapsType _ShadowMapsType = ShadowMapsType.VSM;
        [SerializeField] private ShadowMapsPrecision _ShadowMapPrecision = ShadowMapsPrecision.Half;
        void OnEnable()
        {

        }

        public ScriptableRenderPass GetPassToEnqueue(RenderTextureDescriptor baseDescriptor, RenderTargetHandle depthAttachmentHandle)
        {
            PrefilterShadowMapsPass pass = new PrefilterShadowMapsPass();
            pass._Enabled = isActiveAndEnabled;
            pass._EVSMExponent = new Vector2(_EVSMExponentPos, _EVSMExponentNeg);
            pass._ShadowMapsType = _ShadowMapsType;
            pass._ShadowMapsPrecision = _ShadowMapPrecision;
            pass.Setup(baseDescriptor, depthAttachmentHandle);
            return pass;
        }
    }
}
