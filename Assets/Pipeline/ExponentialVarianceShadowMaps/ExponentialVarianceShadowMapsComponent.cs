using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine.Experimental.Rendering.LightweightPipeline.Extension
{
    public class ExponentialVarianceShadowMapsComponent : MonoBehaviour, IAfterMainLightShadowCasterPass
    {
        [Range(1, 45)]
        [SerializeField] private int _EVSMExponentPos = 10;
        [Range(1, 45)]
        [SerializeField] private int _EVSMExponentNeg = 20;
        [SerializeField] private ShadowMapsPrecision _ShadowMapPrecision = ShadowMapsPrecision.Half;
        [Header("Mipmaps are disable when cascade is larger than one.")]
        [SerializeField] private bool _UseMipmaps = false;
        [SerializeField] private ComputeShader _FilteringCompute = null;
        void OnEnable()
        {

        }

        public ScriptableRenderPass GetPassToEnqueue(RenderTextureDescriptor baseDescriptor, RenderTargetHandle mainLightShadowmapHandle)
        {
            ExponentialVarianceShadowMapsPass pass = new ExponentialVarianceShadowMapsPass();
            pass._Enabled = isActiveAndEnabled;
            pass._EVSMExponent = new Vector4(_EVSMExponentPos, -_EVSMExponentNeg, _EVSMExponentPos * 2, -_EVSMExponentNeg * 2);
            pass._ShadowMapsPrecision = _ShadowMapPrecision;
            pass._UseMipmaps = _UseMipmaps;
            pass._Compute = _FilteringCompute;
            pass.Setup(baseDescriptor, mainLightShadowmapHandle);
            return pass;
        }
    }
}